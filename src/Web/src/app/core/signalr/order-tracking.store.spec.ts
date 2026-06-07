import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from '../api.service';
import { RoleService } from '../role.service';
import { TrackingStage } from '../models';
import { HubConnectionFactory } from './hub-connection.factory';
import { OrderTrackingStore, STATUS_CHANGED_METHOD } from './order-tracking.store';

/** A controllable fake HubConnection that records handlers and lets tests fire pushes/lifecycle. */
class FakeHubConnection {
  state = 'Disconnected';
  handlers: Record<string, (payload: unknown) => void> = {};
  reconnecting?: () => void;
  reconnected?: () => void | Promise<void>;
  closed?: () => void;
  subscribed: string[] = [];
  startCalls = 0;
  stopCalls = 0;
  startShouldFail = false;

  on(method: string, handler: (payload: unknown) => void): void {
    this.handlers[method] = handler;
  }
  onreconnecting(cb: () => void): void {
    this.reconnecting = cb;
  }
  onreconnected(cb: () => void | Promise<void>): void {
    this.reconnected = cb;
  }
  onclose(cb: () => void): void {
    this.closed = cb;
  }
  async start(): Promise<void> {
    this.startCalls++;
    if (this.startShouldFail) {
      throw new Error('boom');
    }
    this.state = 'Connected';
  }
  async stop(): Promise<void> {
    this.stopCalls++;
    this.state = 'Disconnected';
  }
  async invoke(method: string, arg: string): Promise<void> {
    if (method === 'Subscribe') {
      this.subscribed.push(arg);
    }
  }
  /** Test helper: fire an OrderStatusChanged push. */
  push(stage: number, stageName: string): void {
    this.handlers[STATUS_CHANGED_METHOD]?.({ orderId: 'o1', stage, stageName });
  }
}

describe('OrderTrackingStore', () => {
  let store: OrderTrackingStore;
  let fake: FakeHubConnection;
  let api: jest.Mocked<Pick<ApiService, 'getOrderStatus' | 'base'>>;

  beforeEach(() => {
    fake = new FakeHubConnection();
    const factory = { create: jest.fn().mockReturnValue(fake) };
    api = {
      base: 'http://gw',
      getOrderStatus: jest.fn().mockReturnValue(of({ orderId: 'o1', stage: 1, stageName: 'OrderPlaced' })),
    } as unknown as jest.Mocked<Pick<ApiService, 'getOrderStatus' | 'base'>>;

    TestBed.configureTestingModule({
      providers: [
        OrderTrackingStore,
        { provide: ApiService, useValue: api },
        { provide: HubConnectionFactory, useValue: factory },
        RoleService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    store = TestBed.inject(OrderTrackingStore);
  });

  it('connects, subscribes to the order group, and resyncs from REST', async () => {
    await store.track('o1');
    expect(fake.startCalls).toBe(1);
    expect(fake.subscribed).toEqual(['o1']);
    expect(api.getOrderStatus).toHaveBeenCalledWith('o1');
    expect(store.connectionState()).toBe('connected');
    expect(store.stage()).toBe(TrackingStage.OrderPlaced);
  });

  it('advances the bar to stage 2 on an OrderStatusChanged for Preparing', async () => {
    await store.track('o1');
    fake.push(TrackingStage.Preparing, 'Preparing');
    expect(store.stage()).toBe(2);
    expect(store.stageName()).toBe('Preparing');
  });

  it('advances through all five stages to Delivered (terminal)', async () => {
    await store.track('o1');
    fake.push(TrackingStage.Preparing, 'Preparing');
    fake.push(TrackingStage.DriverAssigned, 'DriverAssigned');
    fake.push(TrackingStage.OutForDelivery, 'OutForDelivery');
    fake.push(TrackingStage.Delivered, 'Delivered');
    expect(store.stage()).toBe(TrackingStage.Delivered);
    expect(store.isTerminal()).toBe(true);
    expect(store.isRefunded()).toBe(false);
  });

  it('never moves the bar backwards on an out-of-order push', async () => {
    await store.track('o1');
    fake.push(TrackingStage.DriverAssigned, 'DriverAssigned');
    fake.push(TrackingStage.Preparing, 'Preparing');
    expect(store.stage()).toBe(TrackingStage.DriverAssigned);
  });

  it('treats the refunded stage as terminal compensation', async () => {
    await store.track('o1');
    fake.push(TrackingStage.DriverAssigned, 'DriverAssigned');
    fake.push(TrackingStage.Refunded, 'Refunded');
    expect(store.stage()).toBe(TrackingStage.Refunded);
    expect(store.isRefunded()).toBe(true);
    expect(store.isTerminal()).toBe(true);
  });

  it('re-subscribes and resyncs on reconnect', async () => {
    await store.track('o1');
    api.getOrderStatus.mockReturnValue(of({ orderId: 'o1', stage: 3, stageName: 'DriverAssigned' }));
    await fake.reconnected?.();
    expect(fake.subscribed).toEqual(['o1', 'o1']);
    expect(store.stage()).toBe(TrackingStage.DriverAssigned);
    expect(store.connectionState()).toBe('connected');
  });

  it('marks reconnecting and disconnected lifecycle states', async () => {
    await store.track('o1');
    fake.reconnecting?.();
    expect(store.connectionState()).toBe('reconnecting');
    fake.closed?.();
    expect(store.connectionState()).toBe('disconnected');
  });

  it('still resyncs from REST when the hub fails to start', async () => {
    fake.startShouldFail = true;
    await store.track('o1');
    expect(store.connectionState()).toBe('disconnected');
    expect(api.getOrderStatus).toHaveBeenCalledWith('o1');
    expect(store.stage()).toBe(TrackingStage.OrderPlaced);
  });

  it('tolerates a failing resync (keeps live pushes working)', async () => {
    api.getOrderStatus.mockReturnValue(throwError(() => new Error('down')));
    await store.track('o1');
    expect(store.stage()).toBe(TrackingStage.Unknown);
    fake.push(TrackingStage.Preparing, 'Preparing');
    expect(store.stage()).toBe(TrackingStage.Preparing);
  });

  it('ignores a null status payload', async () => {
    await store.track('o1');
    store.applyStatus(null as never);
    expect(store.stage()).toBe(TrackingStage.OrderPlaced);
  });

  it('stops the connection on teardown', async () => {
    await store.track('o1');
    await store.stop();
    expect(fake.stopCalls).toBe(1);
    expect(store.connectionState()).toBe('idle');
  });
});
