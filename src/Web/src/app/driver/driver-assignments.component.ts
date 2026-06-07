import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { DriverAssignmentItem, ORDER_STATUS_LABELS, OrderStatusCode } from '../core/models';
import { QueueLiveStore } from '../core/signalr/queue-live.store';

/**
 * The functional-clean driver assignment view (PRD F7 / ADR-002). It lists the driver's current
 * assignments — each showing the driver name, ETA and order — and exposes the two actions that advance
 * the shared order: <b>Pick up</b> (DriverAssigned → PickedUp) and <b>Deliver</b> (PickedUp → Delivered,
 * the terminal state which clears the assignment). Both call the gateway (202 async) and refresh the list.
 * A {@link QueueLiveStore} (reusing the SignalR plumbing from task_15) refreshes the list live when any
 * role advances an order, so the assignment appears as soon as the restaurant readies an order and the
 * dispatch saga assigns a driver, and disappears once delivered.
 */
@Component({
  selector: 'app-driver-assignments',
  standalone: true,
  imports: [],
  providers: [QueueLiveStore],
  templateUrl: './driver-assignments.component.html',
  styleUrl: './driver-assignments.component.scss',
})
export class DriverAssignmentsComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly live = inject(QueueLiveStore);

  readonly assignments = signal<DriverAssignmentItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  /** Order ids with an action in flight, so their buttons disable until the refresh completes. */
  readonly pending = signal<ReadonlySet<string>>(new Set());

  readonly connectionState = this.live.connectionState;
  readonly statusLabels = ORDER_STATUS_LABELS;

  readonly isEmpty = computed(() => this.assignments().length === 0);

  async ngOnInit(): Promise<void> {
    await this.refresh();
    await this.live.connect(() => void this.refresh());
  }

  ngOnDestroy(): void {
    void this.live.stop();
  }

  /** Re-fetches assignments from the gateway and (re)subscribes the live store to those orders. */
  async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const assignments = await firstValueFrom(this.api.getDriverAssignments());
      this.assignments.set(assignments);
      await this.live.subscribeTo(assignments.map((a) => a.orderId));
    } catch {
      this.error.set('Could not load your assignments. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  /** Confirm pickup for an assigned order, then refresh so it advances to "picked up". */
  pickup(item: DriverAssignmentItem): void {
    void this.act(item.orderId, () => firstValueFrom(this.api.pickupOrder(item.orderId)));
  }

  /** Confirm delivery (terminal); after refresh the assignment clears from the list. */
  deliver(item: DriverAssignmentItem): void {
    void this.act(item.orderId, () => firstValueFrom(this.api.deliverOrder(item.orderId)));
  }

  /** True when an assignment is still awaiting pickup (Accept/ready done, driver assigned). */
  canPickup(item: DriverAssignmentItem): boolean {
    return item.status === OrderStatusCode.DriverAssigned;
  }

  /** True once the order has been picked up and is ready to be delivered. */
  canDeliver(item: DriverAssignmentItem): boolean {
    return item.status === OrderStatusCode.PickedUp;
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
}
