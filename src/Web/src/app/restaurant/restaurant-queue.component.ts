import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { ORDER_STATUS_LABELS, RestaurantQueueItem, RestaurantQueueResponse } from '../core/models';
import { QueueLiveStore } from '../core/signalr/queue-live.store';

/** An empty queue used before the first load completes. */
const EMPTY_QUEUE: RestaurantQueueResponse = { new: [], inProgress: [], ready: [] };

/**
 * The functional-clean restaurant order queue (PRD F5 / ADR-002). It renders the three columns the
 * gateway groups orders into — New (Paid, awaiting accept), In-Progress (Accepted/Preparing), and Ready
 * (ReadyForPickup) — and exposes the two actions that advance the shared order: <b>Accept</b> on a New
 * order and <b>Mark ready</b> on an In-Progress order. Both call the gateway (202 async) and then refresh
 * the queue. A {@link QueueLiveStore} (reusing the SignalR plumbing from task_15) refreshes the queue
 * live whenever any role advances an order, so this view stays in sync with the consumer and driver views.
 */
@Component({
  selector: 'app-restaurant-queue',
  standalone: true,
  imports: [CurrencyPipe],
  providers: [QueueLiveStore],
  templateUrl: './restaurant-queue.component.html',
  styleUrl: './restaurant-queue.component.scss',
})
export class RestaurantQueueComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly live = inject(QueueLiveStore);

  readonly queue = signal<RestaurantQueueResponse>(EMPTY_QUEUE);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  /** Order ids with an action in flight, so their buttons disable until the refresh completes. */
  readonly pending = signal<ReadonlySet<string>>(new Set());

  readonly connectionState = this.live.connectionState;
  readonly statusLabels = ORDER_STATUS_LABELS;

  /** True when every column is empty (drives the empty-state message). */
  readonly isEmpty = computed(() => {
    const q = this.queue();
    return q.new.length === 0 && q.inProgress.length === 0 && q.ready.length === 0;
  });

  async ngOnInit(): Promise<void> {
    await this.refresh();
    // Reuse the SignalR store for live updates: refresh whenever any order advances.
    await this.live.connect(() => void this.refresh());
  }

  ngOnDestroy(): void {
    void this.live.stop();
  }

  /** Re-fetches the queue from the gateway and (re)subscribes the live store to the visible orders. */
  async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const queue = await firstValueFrom(this.api.getRestaurantQueue());
      this.queue.set(queue);
      await this.live.subscribeTo(this.allOrderIds(queue));
    } catch {
      this.error.set('Could not load the order queue. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  /** Accept a New order, then refresh so it moves to the In-Progress column. */
  accept(item: RestaurantQueueItem): void {
    void this.act(item.orderId, () => firstValueFrom(this.api.acceptOrder(item.orderId)));
  }

  /** Mark an In-Progress order ready, then refresh so it moves to the Ready column. */
  markReady(item: RestaurantQueueItem): void {
    void this.act(item.orderId, () => firstValueFrom(this.api.markOrderReady(item.orderId)));
  }

  /** True while an action on this order is in flight. */
  isPending(orderId: string): boolean {
    return this.pending().has(orderId);
  }

  private async act(orderId: string, call: () => Promise<unknown>): Promise<void> {
    if (this.isPending(orderId)) {
      return;
    }
    this.setPending(orderId, true);
    try {
      await call();
      await this.refresh();
    } catch {
      this.error.set('That action could not be completed. Please try again.');
    } finally {
      this.setPending(orderId, false);
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

  private allOrderIds(queue: RestaurantQueueResponse): string[] {
    return [...queue.new, ...queue.inProgress, ...queue.ready].map((i) => i.orderId);
  }
}
