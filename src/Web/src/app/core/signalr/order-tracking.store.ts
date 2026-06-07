import { computed, inject, Injectable, signal } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../api.service';
import { RoleService } from '../role.service';
import { OrderStatusChanged, TrackingStage } from '../models';
import { HubConnectionFactory } from './hub-connection.factory';

/** The SignalR client method the gateway pushes status changes with (must match OrdersHub.StatusChangedMethod). */
export const STATUS_CHANGED_METHOD = 'OrderStatusChanged';

/** Connection state surfaced to the UI for a small live/offline indicator. */
export type TrackingConnectionState = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/**
 * The reusable SignalR client + status store (ADR-007). It owns one hub connection to the gateway's
 * `/hubs/orders`, subscribes to a specific order's group via `Subscribe(orderId)`, and advances the
 * current stage as `OrderStatusChanged` pushes arrive. On (re)connect it resyncs the current stage over
 * REST (`GET /api/orders/{id}/status`) so any event missed while disconnected is recovered.
 *
 * The store is provided at the route level (not root) so each tracking screen gets its own connection,
 * and it is reusable by the restaurant/driver views added in task_16.
 */
@Injectable()
export class OrderTrackingStore {
  private readonly api = inject(ApiService);
  private readonly roles = inject(RoleService);
  private readonly factory = inject(HubConnectionFactory);

  private connection?: HubConnection;
  private orderId?: string;

  private readonly _stage = signal<number>(TrackingStage.Unknown);
  private readonly _stageName = signal<string>('');
  private readonly _connection = signal<TrackingConnectionState>('idle');

  /** The current numeric tracking stage (drives the 5-stage bar). */
  readonly stage = this._stage.asReadonly();
  /** The current stage display name as pushed by the gateway. */
  readonly stageName = this._stageName.asReadonly();
  /** The hub connection state for a live/offline indicator. */
  readonly connectionState = this._connection.asReadonly();
  /** True once the order reaches a terminal stage (delivered or refunded). */
  readonly isTerminal = computed(
    () => this._stage() === TrackingStage.Delivered || this._stage() === TrackingStage.Refunded,
  );
  /** True when the order ended on the refund/cancel compensation path (PRD F9). */
  readonly isRefunded = computed(() => this._stage() === TrackingStage.Refunded);

  /**
   * Connects to the hub, subscribes to the order's group, and wires the push handler. The current
   * stage is resynced from REST immediately and again on every (re)connect (ADR-007 mitigation).
   */
  async track(orderId: string): Promise<void> {
    this.orderId = orderId;
    const connection = this.factory.create(`${this.api.base}/hubs/orders`, {
      headers: { 'X-Demo-Role': this.roles.current() },
    });
    this.connection = connection;

    connection.on(STATUS_CHANGED_METHOD, (payload: OrderStatusChanged) => this.applyStatus(payload));
    connection.onreconnecting(() => this._connection.set('reconnecting'));
    connection.onreconnected(async () => {
      this._connection.set('connected');
      await this.subscribe();
      await this.resync();
    });
    connection.onclose(() => this._connection.set('disconnected'));

    this._connection.set('connecting');
    try {
      await connection.start();
      this._connection.set('connected');
      await this.subscribe();
    } catch {
      this._connection.set('disconnected');
    }

    // Always resync from REST so the bar reflects the true current stage even if the connection
    // failed or events were missed before subscribing.
    await this.resync();
  }

  /** Applies a status change (from a push or a REST resync), never moving the bar backwards. */
  applyStatus(payload: OrderStatusChanged): void {
    if (!payload) {
      return;
    }
    // The refunded terminal stage (99) always wins; otherwise advance monotonically.
    if (payload.stage === TrackingStage.Refunded || payload.stage >= this._stage()) {
      this._stage.set(payload.stage);
      this._stageName.set(payload.stageName ?? '');
    }
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
    this._connection.set('idle');
  }

  private async subscribe(): Promise<void> {
    if (this.connection && this.orderId && this.connection.state === 'Connected') {
      try {
        await this.connection.invoke('Subscribe', this.orderId);
      } catch {
        // A failed subscribe is recovered by the REST resync below.
      }
    }
  }

  private async resync(): Promise<void> {
    if (!this.orderId) {
      return;
    }
    try {
      const status = await firstValueFrom(this.api.getOrderStatus(this.orderId));
      this.applyStatus(status);
    } catch {
      // Resync best-effort; live pushes will still advance the bar.
    }
  }
}
