import { inject, Injectable, signal } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { ApiService } from '../api.service';
import { RoleService } from '../role.service';
import { OrderStatusChanged } from '../models';
import { HubConnectionFactory } from './hub-connection.factory';
import { STATUS_CHANGED_METHOD } from './order-tracking.store';

/** Connection state surfaced to the restaurant/driver views for a small live/offline indicator. */
export type QueueConnectionState = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/**
 * A reusable, many-order companion to {@link OrderTrackingStore} (ADR-007). Where the tracking store
 * follows a single order's 5-stage bar, the restaurant/driver views render a *list* of orders that any
 * role can advance, so they need a "something changed — refresh" signal rather than a single stage.
 *
 * This store reuses the exact same SignalR plumbing the tracking store does — the shared
 * {@link HubConnectionFactory}, the gateway `/hubs/orders` hub, the `X-Demo-Role` header, and the
 * `OrderStatusChanged` push method — but subscribes to each visible order's group and exposes:
 *  - {@link version}: a counter bumped on every push so a view can refresh its list from the gateway, and
 *  - {@link lastChange}: the latest pushed status (used by integration tests / live demos).
 *
 * The result is the "one order, three live views" behaviour: an order placed or advanced in the consumer
 * view pushes `OrderStatusChanged`, which bumps {@link version} here and re-fetches the queue/assignments
 * so the restaurant and driver views reflect it live. It is provided at the route/component level (like the
 * tracking store) so each view owns its own connection.
 */
@Injectable()
export class QueueLiveStore {
  private readonly api = inject(ApiService);
  private readonly roles = inject(RoleService);
  private readonly factory = inject(HubConnectionFactory);

  private connection?: HubConnection;
  private readonly subscribed = new Set<string>();

  private readonly _version = signal(0);
  private readonly _connection = signal<QueueConnectionState>('idle');
  private readonly _lastChange = signal<OrderStatusChanged | null>(null);

  /** Bumped on every live `OrderStatusChanged` push so a view can refresh its list. */
  readonly version = this._version.asReadonly();
  /** The hub connection state for a live/offline indicator. */
  readonly connectionState = this._connection.asReadonly();
  /** The most recent status change pushed by the gateway (or null before any). */
  readonly lastChange = this._lastChange.asReadonly();

  /**
   * Connects to the gateway hub and wires the push handler. The `onChange` callback fires on every
   * status change so the owning view can refresh its list; the store also keeps {@link version}/
   * {@link lastChange} updated for components that prefer to react to signals.
   */
  async connect(onChange?: (payload: OrderStatusChanged) => void): Promise<void> {
    const connection = this.factory.create(`${this.api.base}/hubs/orders`, {
      headers: { 'X-Demo-Role': this.roles.current() },
    });
    this.connection = connection;

    connection.on(STATUS_CHANGED_METHOD, (payload: OrderStatusChanged) => {
      this.apply(payload);
      onChange?.(payload);
    });
    connection.onreconnecting(() => this._connection.set('reconnecting'));
    connection.onreconnected(async () => {
      this._connection.set('connected');
      await this.resubscribe();
    });
    connection.onclose(() => this._connection.set('disconnected'));

    this._connection.set('connecting');
    try {
      await connection.start();
      this._connection.set('connected');
    } catch {
      this._connection.set('disconnected');
    }
  }

  /** Subscribes to the hub groups for the given order ids (deduped). Safe to call on every refresh. */
  async subscribeTo(orderIds: readonly string[]): Promise<void> {
    for (const id of orderIds) {
      if (!id || this.subscribed.has(id)) {
        continue;
      }
      this.subscribed.add(id);
      await this.invokeSubscribe(id);
    }
  }

  /** Applies a push to the local signals (also reachable directly for tests). */
  apply(payload: OrderStatusChanged): void {
    if (!payload) {
      return;
    }
    this._lastChange.set(payload);
    this._version.update((v) => v + 1);
  }

  /** Stops the hub connection and resets state. */
  async stop(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // Ignore stop failures during teardown.
      }
      this.connection = undefined;
    }
    this.subscribed.clear();
    this._connection.set('idle');
  }

  private async resubscribe(): Promise<void> {
    for (const id of this.subscribed) {
      await this.invokeSubscribe(id);
    }
  }

  private async invokeSubscribe(id: string): Promise<void> {
    if (this.connection && this.connection.state === 'Connected') {
      try {
        await this.connection.invoke('Subscribe', id);
      } catch {
        // A failed subscribe is recovered on the next refresh/reconnect.
      }
    }
  }
}
