import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from '../api.service';
import { RoleService } from '../role.service';
import { HubConnectionFactory } from './hub-connection.factory';
import { STATUS_CHANGED_METHOD } from './order-tracking.store';
import { QueueLiveStore } from './queue-live.store';

/** A controllable fake HubConnection mirroring the one used by the tracking store tests. */
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
  push(orderId: string, stage: number, stageName: string): void {
    this.handlers[STATUS_CHANGED_METHOD]?.({ orderId, stage, stageName });
  }
}

describe('QueueLiveStore', () => {
  let store: QueueLiveStore;
  let fake: FakeHubConnection;

  beforeEach(() => {
    fake = new FakeHubConnection();
    const factory = { create: jest.fn().mockReturnValue(fake) };
    const api = { base: 'http://gw' };

    TestBed.configureTestingModule({
      providers: [
        QueueLiveStore,
        { provide: ApiService, useValue: api },
        { provide: HubConnectionFactory, useValue: factory },
        RoleService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    store = TestBed.inject(QueueLiveStore);
  });

  it('connects to the hub and reports the connected state', async () => {
    await store.connect();
    expect(fake.startCalls).toBe(1);
    expect(store.connectionState()).toBe('connected');
  });

  it('bumps the version and records the last change, and invokes the onChange callback, on a push', async () => {
    const onChange = jest.fn();
    await store.connect(onChange);
    expect(store.version()).toBe(0);

    fake.push('o1', 4, 'Preparing');

    expect(store.version()).toBe(1);
    expect(store.lastChange()).toEqual({ orderId: 'o1', stage: 4, stageName: 'Preparing' });
    expect(onChange).toHaveBeenCalledWith({ orderId: 'o1', stage: 4, stageName: 'Preparing' });
  });

  it('subscribes to the order groups (deduped) once connected', async () => {
    await store.connect();
    await store.subscribeTo(['o1', 'o2', 'o1']);
    expect(fake.subscribed).toEqual(['o1', 'o2']);

    // Subscribing again to a known id does not re-invoke.
    await store.subscribeTo(['o1']);
    expect(fake.subscribed).toEqual(['o1', 'o2']);
  });

  it('re-subscribes to known orders on reconnect', async () => {
    await store.connect();
    await store.subscribeTo(['o1', 'o2']);
    fake.subscribed = [];

    await fake.reconnected?.();

    expect(store.connectionState()).toBe('connected');
    expect(fake.subscribed.sort()).toEqual(['o1', 'o2']);
  });

  it('marks reconnecting and disconnected lifecycle states', async () => {
    await store.connect();
    fake.reconnecting?.();
    expect(store.connectionState()).toBe('reconnecting');
    fake.closed?.();
    expect(store.connectionState()).toBe('disconnected');
  });

  it('reports disconnected when the hub fails to start (and tolerates later subscribe)', async () => {
    fake.startShouldFail = true;
    await store.connect();
    expect(store.connectionState()).toBe('disconnected');
    // subscribeTo must not throw even though the connection is not Connected.
    await store.subscribeTo(['o1']);
    expect(fake.subscribed).toEqual([]);
  });

  it('ignores a null push payload', async () => {
    await store.connect();
    store.apply(null as never);
    expect(store.version()).toBe(0);
    expect(store.lastChange()).toBeNull();
  });

  it('stops the connection and clears subscriptions on teardown', async () => {
    await store.connect();
    await store.subscribeTo(['o1']);
    await store.stop();
    expect(fake.stopCalls).toBe(1);
    expect(store.connectionState()).toBe('idle');

    // After stop, a fresh connect + subscribe starts clean.
    fake.subscribed = [];
    await store.connect();
    await store.subscribeTo(['o1']);
    expect(fake.subscribed).toEqual(['o1']);
  });
});
