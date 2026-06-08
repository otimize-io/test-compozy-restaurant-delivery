import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { defer, of, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';

/** An observable that errors asynchronously, mirroring a failed HTTP call without a sync throw. */
function asyncError(): ReturnType<typeof throwError> {
  return defer(() => throwError(() => new Error('down'))).pipe(delay(0));
}
import { ApiService } from '../../core/api.service';
import { ConsumerOrderItem, OrderStatusCode } from '../../core/models';
import { CONSUMER_ID } from '../checkout.service';
import { HubConnectionFactory } from '../../core/signalr/hub-connection.factory';
import { STATUS_CHANGED_METHOD } from '../../core/signalr/order-tracking.store';
import { OrdersComponent } from './orders.component';

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

function order(orderId: string, status: number, extra?: Partial<ConsumerOrderItem>): ConsumerOrderItem {
  return {
    orderId,
    status,
    total: 30,
    restaurantId: 'r1',
    createdAt: '2026-06-08T12:00:00Z',
    ...extra,
  };
}

describe('OrdersComponent', () => {
  let fixture: ComponentFixture<OrdersComponent>;
  let component: OrdersComponent;
  let fake: FakeHubConnection;
  let api: { base: string; getConsumerOrders: jest.Mock };

  async function create(initial: ConsumerOrderItem[] = [order('o1', OrderStatusCode.Paid)]): Promise<void> {
    fake = new FakeHubConnection();
    api = {
      base: 'http://gw',
      getConsumerOrders: jest.fn().mockReturnValue(of(initial)),
    };
    TestBed.configureTestingModule({
      imports: [OrdersComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ApiService, useValue: api },
        { provide: HubConnectionFactory, useValue: { create: () => fake } },
      ],
    });
    fixture = TestBed.createComponent(OrdersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await flush();
  }

  /** Drains the async ngOnInit chain (refresh + live.connect). */
  async function flush(): Promise<void> {
    for (let i = 0; i < 5; i++) {
      await Promise.resolve();
      await fixture.whenStable();
    }
    fixture.detectChanges();
  }

  function rows(): HTMLElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('[data-testid="order-row"]'));
  }

  it('loads the consumer orders from the gateway and renders a row per order', async () => {
    await create([order('o1', OrderStatusCode.Paid), order('o2', OrderStatusCode.Preparing)]);

    expect(api.getConsumerOrders).toHaveBeenCalledWith(CONSUMER_ID);
    expect(rows().length).toBe(2);
    const status = fixture.nativeElement.querySelector('[data-testid="order-status"]');
    expect(status.textContent).toContain('Paid');
    const track = fixture.nativeElement.querySelector('[data-testid="track-link"]');
    expect(track.getAttribute('href')).toContain('/consumer/track/o1');
  });

  it('shows the assigned driver and ETA on an order', async () => {
    await create([
      order('o3', OrderStatusCode.DriverAssigned, { driverName: 'Alice', etaMinutes: 7 }),
    ]);

    const line = fixture.nativeElement.querySelector('[data-testid="driver-line"]');
    expect(line).toBeTruthy();
    expect(line.textContent).toContain('Alice');
    expect(line.textContent).toContain('7');
  });

  it('refreshes the list live when the SignalR store reports a status change', async () => {
    await create([order('o1', OrderStatusCode.Paid)]);
    expect(api.getConsumerOrders).toHaveBeenCalledTimes(1);
    expect(component.connectionState()).toBe('connected');

    api.getConsumerOrders.mockReturnValue(of([order('o1', OrderStatusCode.Preparing)]));
    fake.push('o1', OrderStatusCode.Preparing, 'Preparing');
    await flush();

    expect(api.getConsumerOrders).toHaveBeenCalledTimes(2);
    expect(component.orders()[0].status).toBe(OrderStatusCode.Preparing);
  });

  it('shows an empty state when the consumer has no orders', async () => {
    await create([]);
    expect(component.isEmpty()).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="orders-empty"]')).toBeTruthy();
  });

  it('shows an error when the orders fail to load', async () => {
    await create();
    api.getConsumerOrders.mockReturnValue(asyncError());
    await component.refresh();
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="orders-error"]')).toBeTruthy();
  });
});
