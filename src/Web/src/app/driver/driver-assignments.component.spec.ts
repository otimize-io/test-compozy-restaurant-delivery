import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { defer, of, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';

/** An observable that errors asynchronously, mirroring a failed HTTP call without a sync throw. */
function asyncError(): ReturnType<typeof throwError> {
  return defer(() => throwError(() => new Error('down'))).pipe(delay(0));
}
import { ApiService } from '../core/api.service';
import { DriverAssignmentItem, OrderStatusCode } from '../core/models';
import { HubConnectionFactory } from '../core/signalr/hub-connection.factory';
import { STATUS_CHANGED_METHOD } from '../core/signalr/order-tracking.store';
import { DriverAssignmentsComponent } from './driver-assignments.component';

class FakeHubConnection {
  state = 'Connected';
  handlers: Record<string, (p: unknown) => void> = {};
  on(method: string, handler: (p: unknown) => void): void {
    this.handlers[method] = handler;
  }
  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
  async start(): Promise<void> {
    this.state = 'Connected';
  }
  async stop(): Promise<void> {}
  async invoke(): Promise<void> {}
  push(orderId: string, stage: number, stageName: string): void {
    this.handlers[STATUS_CHANGED_METHOD]?.({ orderId, stage, stageName });
  }
}

function assignment(orderId: string, status: number): DriverAssignmentItem {
  return {
    orderId,
    status,
    driverId: `d-${orderId}`,
    driverName: 'Alice',
    etaMinutes: 12,
    correlationId: `c-${orderId}`,
  };
}

describe('DriverAssignmentsComponent', () => {
  let fixture: ComponentFixture<DriverAssignmentsComponent>;
  let component: DriverAssignmentsComponent;
  let fake: FakeHubConnection;
  let api: {
    base: string;
    getDriverAssignments: jest.Mock;
    pickupOrder: jest.Mock;
    deliverOrder: jest.Mock;
  };

  async function create(initial: DriverAssignmentItem[]): Promise<void> {
    fake = new FakeHubConnection();
    api = {
      base: 'http://gw',
      getDriverAssignments: jest.fn().mockReturnValue(of(initial)),
      pickupOrder: jest.fn().mockReturnValue(of(undefined)),
      deliverOrder: jest.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [DriverAssignmentsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ApiService, useValue: api },
        { provide: HubConnectionFactory, useValue: { create: () => fake } },
      ],
    });
    fixture = TestBed.createComponent(DriverAssignmentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await flush();
  }

  /** Drains the async ngOnInit chain (refresh + live.connect) and the pending action chains. */
  async function flush(): Promise<void> {
    for (let i = 0; i < 5; i++) {
      await Promise.resolve();
      await fixture.whenStable();
    }
    fixture.detectChanges();
  }

  it('renders the assignment list with driver name and ETA', async () => {
    await create([assignment('o1', OrderStatusCode.DriverAssigned)]);
    expect(api.getDriverAssignments).toHaveBeenCalled();
    const card = fixture.nativeElement.querySelector('[data-testid="assignment-card"]');
    expect(card).toBeTruthy();
    expect(card.textContent).toContain('Alice');
    expect(card.textContent).toContain('12 min');
  });

  it('shows Pick up for an assigned order and Deliver for a picked-up order', async () => {
    await create([assignment('o1', OrderStatusCode.DriverAssigned)]);
    expect(fixture.nativeElement.querySelector('[data-testid="pickup-btn"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="deliver-btn"]')).toBeFalsy();
  });

  it('clicking Pick up then Deliver calls the respective endpoints and clears the assignment', async () => {
    await create([assignment('o1', OrderStatusCode.DriverAssigned)]);

    // After pickup the refreshed list returns the order in the picked-up state.
    api.getDriverAssignments.mockReturnValue(of([assignment('o1', OrderStatusCode.PickedUp)]));
    fixture.nativeElement.querySelector('[data-testid="pickup-btn"]').click();
    await flush();

    expect(api.pickupOrder).toHaveBeenCalledWith('o1');
    expect(fixture.nativeElement.querySelector('[data-testid="deliver-btn"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="pickup-btn"]')).toBeFalsy();

    // After delivery the refreshed list is empty (terminal state clears the assignment).
    api.getDriverAssignments.mockReturnValue(of([]));
    fixture.nativeElement.querySelector('[data-testid="deliver-btn"]').click();
    await flush();

    expect(api.deliverOrder).toHaveBeenCalledWith('o1');
    expect(component.isEmpty()).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="empty-assignments"]')).toBeTruthy();
  });

  it('refreshes the list live when the shared SignalR store reports a status change', async () => {
    await create([]);
    expect(api.getDriverAssignments).toHaveBeenCalledTimes(1);
    expect(component.connectionState()).toBe('connected');

    // A driver-assigned push (driven by the saga) makes the assignment appear live.
    api.getDriverAssignments.mockReturnValue(of([assignment('o1', OrderStatusCode.DriverAssigned)]));
    fake.push('o1', OrderStatusCode.DriverAssigned, 'DriverAssigned');
    await flush();

    expect(api.getDriverAssignments).toHaveBeenCalledTimes(2);
    expect(component.assignments().length).toBe(1);
  });

  it('manual refresh re-fetches assignments', async () => {
    await create([assignment('o1', OrderStatusCode.DriverAssigned)]);
    fixture.nativeElement.querySelector('[data-testid="refresh"]').click();
    await fixture.whenStable();
    expect(api.getDriverAssignments).toHaveBeenCalledTimes(2);
  });

  it('shows an error when assignments fail to load', async () => {
    await create([assignment('o1', OrderStatusCode.DriverAssigned)]);
    api.getDriverAssignments.mockReturnValue(asyncError());
    await component.refresh();
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="assignments-error"]')).toBeTruthy();
  });

  it('shows an error when a pickup action fails and clears the pending flag', async () => {
    await create([assignment('o1', OrderStatusCode.DriverAssigned)]);
    api.pickupOrder.mockReturnValue(asyncError());

    component.pickup(assignment('o1', OrderStatusCode.DriverAssigned));
    await flushMacro();
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    expect(component.isPending('o1')).toBe(false);
  });

  /** Waits for a real macrotask so a delay(0) error settles, then drains microtasks. */
  async function flushMacro(): Promise<void> {
    await new Promise((r) => setTimeout(r, 1));
    for (let i = 0; i < 5; i++) {
      await Promise.resolve();
    }
  }
});
