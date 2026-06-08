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
import { OrderStatusCode, RestaurantQueueItem, RestaurantQueueResponse } from '../core/models';
import { HubConnectionFactory } from '../core/signalr/hub-connection.factory';
import { STATUS_CHANGED_METHOD } from '../core/signalr/order-tracking.store';
import { RestaurantQueueComponent } from './restaurant-queue.component';

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

function item(orderId: string, status: number, driver?: Partial<RestaurantQueueItem>): RestaurantQueueItem {
  return { orderId, status, total: 25, correlationId: `c-${orderId}`, ...driver };
}

describe('RestaurantQueueComponent', () => {
  let fixture: ComponentFixture<RestaurantQueueComponent>;
  let component: RestaurantQueueComponent;
  let fake: FakeHubConnection;
  let api: {
    base: string;
    getRestaurantQueue: jest.Mock;
    acceptOrder: jest.Mock;
    markOrderReady: jest.Mock;
  };

  /** Builds a board with the given columns, defaulting to one New order and the rest empty. */
  function queue(overrides: Partial<RestaurantQueueResponse> = {}): RestaurantQueueResponse {
    return {
      new: [item('o1', OrderStatusCode.Paid)],
      cooking: [],
      awaitingDriver: [],
      outForDelivery: [],
      delivered: [],
      ...overrides,
    };
  }

  async function create(initial: RestaurantQueueResponse = queue()): Promise<void> {
    fake = new FakeHubConnection();
    api = {
      base: 'http://gw',
      getRestaurantQueue: jest.fn().mockReturnValue(of(initial)),
      acceptOrder: jest.fn().mockReturnValue(of(undefined)),
      markOrderReady: jest.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [RestaurantQueueComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ApiService, useValue: api },
        { provide: HubConnectionFactory, useValue: { create: () => fake } },
      ],
    });
    fixture = TestBed.createComponent(RestaurantQueueComponent);
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

  function cards(testid: string): HTMLElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll(`[data-testid="${testid}"]`));
  }

  it('renders the five lifecycle columns and loads the board from the gateway', async () => {
    await create(
      queue({
        new: [item('o1', OrderStatusCode.Paid)],
        cooking: [item('o2', OrderStatusCode.Accepted)],
        awaitingDriver: [item('o3', OrderStatusCode.ReadyForPickup)],
        outForDelivery: [item('o4', OrderStatusCode.PickedUp)],
        delivered: [item('o5', OrderStatusCode.Delivered)],
      }),
    );

    expect(api.getRestaurantQueue).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-testid="column-new"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="column-cooking"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="column-awaiting-driver"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="column-out-for-delivery"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="column-delivered"]')).toBeTruthy();
    expect(cards('order-card').length).toBe(5);
  });

  it('clicking Accept on a New order calls the accept endpoint and moves the card to Cooking', async () => {
    await create(queue({ new: [item('o1', OrderStatusCode.Paid)] }));

    // After acceptance the refreshed board returns the order in the Cooking column.
    api.getRestaurantQueue.mockReturnValue(
      of(queue({ new: [], cooking: [item('o1', OrderStatusCode.Accepted)] })),
    );

    fixture.nativeElement.querySelector('[data-testid="accept-btn"]').click();
    await flush();

    expect(api.acceptOrder).toHaveBeenCalledWith('o1');
    // Two board loads: initial + post-accept refresh.
    expect(api.getRestaurantQueue).toHaveBeenCalledTimes(2);

    const newColumn = fixture.nativeElement.querySelector('[data-testid="column-new"]');
    const cookingColumn = fixture.nativeElement.querySelector('[data-testid="column-cooking"]');
    expect(newColumn.querySelector('[data-testid="order-card"]')).toBeFalsy();
    expect(cookingColumn.querySelector('[data-testid="order-card"]')).toBeTruthy();
    expect(component.queue().new.length).toBe(0);
    expect(component.queue().cooking.length).toBe(1);
  });

  it('clicking Mark ready calls the ready endpoint and moves the card to Awaiting driver', async () => {
    await create(queue({ new: [], cooking: [item('o2', OrderStatusCode.Preparing)] }));

    api.getRestaurantQueue.mockReturnValue(
      of(queue({ new: [], awaitingDriver: [item('o2', OrderStatusCode.ReadyForPickup)] })),
    );

    fixture.nativeElement.querySelector('[data-testid="ready-btn"]').click();
    await flush();

    expect(api.markOrderReady).toHaveBeenCalledWith('o2');
    const column = fixture.nativeElement.querySelector('[data-testid="column-awaiting-driver"]');
    expect(column.querySelector('[data-testid="order-card"]')).toBeTruthy();
    expect(component.queue().awaitingDriver.length).toBe(1);
  });

  it('shows the assigned driver and ETA on an awaiting-driver card', async () => {
    await create(
      queue({
        new: [],
        awaitingDriver: [
          item('o7', OrderStatusCode.DriverAssigned, { driverName: 'Alice', etaMinutes: 7 }),
        ],
      }),
    );

    const line = fixture.nativeElement.querySelector('[data-testid="driver-line"]');
    expect(line).toBeTruthy();
    expect(line.textContent).toContain('Alice');
    expect(line.textContent).toContain('7');
  });

  it('refreshes the board live when the shared SignalR store reports a status change', async () => {
    await create(queue({ new: [item('o1', OrderStatusCode.Paid)] }));
    expect(api.getRestaurantQueue).toHaveBeenCalledTimes(1);
    expect(component.connectionState()).toBe('connected');

    // A status change pushed by another role (e.g. the consumer/driver) triggers a refresh.
    api.getRestaurantQueue.mockReturnValue(
      of(queue({ new: [item('o1', OrderStatusCode.Paid), item('o9', OrderStatusCode.Paid)] })),
    );
    fake.push('o9', OrderStatusCode.Paid, 'Paid');
    await flush();

    expect(api.getRestaurantQueue).toHaveBeenCalledTimes(2);
    expect(component.queue().new.length).toBe(2);
  });

  it('shows an empty state when there are no orders', async () => {
    await create(
      queue({ new: [], cooking: [], awaitingDriver: [], outForDelivery: [], delivered: [] }),
    );
    expect(component.isEmpty()).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="empty-new"]')).toBeTruthy();
  });

  it('manual refresh re-fetches the board', async () => {
    await create();
    fixture.nativeElement.querySelector('[data-testid="refresh"]').click();
    await fixture.whenStable();
    expect(api.getRestaurantQueue).toHaveBeenCalledTimes(2);
  });

  it('shows an error when the board fails to load', async () => {
    await create();
    // The next load fails; refresh is awaited directly so the catch runs in-band.
    api.getRestaurantQueue.mockReturnValue(asyncError());
    await component.refresh();
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="queue-error"]')).toBeTruthy();
  });

  it('shows an error when an accept action fails and does not re-enter while pending', async () => {
    await create(queue({ new: [item('o1', OrderStatusCode.Paid)] }));
    api.acceptOrder.mockReturnValue(asyncError());

    component.accept(item('o1', OrderStatusCode.Paid));
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
