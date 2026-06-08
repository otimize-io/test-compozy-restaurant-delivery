import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ConsumerOrderItem, ORDER_STATUS_LABELS, OrderStatusCode } from '../../core/models';
import { QueueLiveStore } from '../../core/signalr/queue-live.store';
import { CONSUMER_ID } from '../checkout.service';

/**
 * The consumer's order-tracking area (PRD F8): a live list of the consumer's orders with their current
 * status, the assigned driver/ETA once matched, and a link into the detailed 5-stage tracking screen. It
 * reuses the same SignalR plumbing as the restaurant/driver boards via {@link QueueLiveStore}: it subscribes
 * to each order's hub group and re-fetches the list on every `OrderStatusChanged` push, so statuses update
 * live as the kitchen, dispatch, and driver advance the order.
 */
@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe],
  providers: [QueueLiveStore],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.scss',
})
export class OrdersComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly live = inject(QueueLiveStore);

  readonly orders = signal<ConsumerOrderItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  /** Order ids with a pay action in flight, so their button disables until the refresh completes. */
  readonly pending = signal<ReadonlySet<string>>(new Set());

  readonly connectionState = this.live.connectionState;
  readonly statusLabels = ORDER_STATUS_LABELS;
  readonly isEmpty = computed(() => this.orders().length === 0);

  async ngOnInit(): Promise<void> {
    await this.refresh();
    // Reuse the shared SignalR store for live updates: refresh whenever any of the orders advances.
    await this.live.connect(() => void this.refresh());
  }

  ngOnDestroy(): void {
    void this.live.stop();
  }

  /** Re-fetches the consumer's orders and (re)subscribes the live store to them. */
  async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const orders = await firstValueFrom(this.api.getConsumerOrders(CONSUMER_ID));
      this.orders.set(orders);
      await this.live.subscribeTo(orders.map((o) => o.orderId));
    } catch {
      this.error.set('Could not load your orders. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  /** True while an order still needs payment (its checkout settle did not complete). */
  isAwaitingPayment(status: number): boolean {
    return status === OrderStatusCode.AwaitingPayment;
  }

  /** True while a pay action on this order is in flight. */
  isPending(orderId: string): boolean {
    return this.pending().has(orderId);
  }

  /** Settle the mock payment for an order stuck at Awaiting payment, then refresh so it moves to Paid. */
  async pay(order: ConsumerOrderItem): Promise<void> {
    if (this.isPending(order.orderId)) {
      return;
    }
    this.setPending(order.orderId, true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.settlePayment({ orderId: order.orderId, outcome: 'settle' }));
      await this.refresh();
    } catch {
      this.error.set('We could not complete the payment. Please try again.');
    } finally {
      this.setPending(order.orderId, false);
    }
  }

  private setPending(orderId: string, on: boolean): void {
    const next = new Set(this.pending());
    if (on) {
      next.add(orderId);
    } else {
      next.delete(orderId);
    }
    this.pending.set(next);
  }

  /** The assigned driver line for an order ("🛵 Alice • ETA 7 min"), or null before a driver is matched. */
  driverLine(order: ConsumerOrderItem): string | null {
    if (!order.driverName) {
      return null;
    }
    return order.etaMinutes != null
      ? `🛵 ${order.driverName} • ETA ${order.etaMinutes} min`
      : `🛵 ${order.driverName}`;
  }

  /** True once an order has reached a terminal state (delivered or refunded), shown distinctly. */
  isDone(status: number): boolean {
    return status === OrderStatusCode.Delivered || status === OrderStatusCode.NoDriverRefunded;
  }
}
