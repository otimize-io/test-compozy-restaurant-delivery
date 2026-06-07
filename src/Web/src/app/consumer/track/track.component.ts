import { Component, computed, inject, input, OnDestroy, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TRACKING_STEPS, TrackingStage } from '../../core/models';
import { OrderTrackingStore } from '../../core/signalr/order-tracking.store';

/**
 * The live 5-stage tracking screen (PRD F8 / ADR-007). It owns an {@link OrderTrackingStore} (provided
 * at the component level so each tracked order gets its own hub connection), starts tracking the order
 * id from the route on init, and renders the 5-stage bar advancing as `OrderStatusChanged` pushes
 * arrive. A refunded order (compensation path, PRD F9) is shown distinctly.
 */
@Component({
  selector: 'app-track',
  standalone: true,
  imports: [RouterLink],
  providers: [OrderTrackingStore],
  templateUrl: './track.component.html',
  styleUrl: './track.component.scss',
})
export class TrackComponent implements OnInit, OnDestroy {
  private readonly store = inject(OrderTrackingStore);

  /** Route param `:orderId` bound via component input binding. */
  readonly orderId = input.required<string>();

  readonly steps = TRACKING_STEPS;
  readonly stage = this.store.stage;
  readonly stageName = this.store.stageName;
  readonly connectionState = this.store.connectionState;
  readonly isTerminal = this.store.isTerminal;
  readonly isRefunded = this.store.isRefunded;

  /** True once the order is delivered (the happy-path terminal state). */
  readonly isDelivered = computed(() => this.stage() === TrackingStage.Delivered);

  ngOnInit(): void {
    void this.store.track(this.orderId());
  }

  ngOnDestroy(): void {
    void this.store.stop();
  }

  /** A step is complete when the current stage has reached or passed it. */
  isComplete(stepStage: number): boolean {
    return !this.isRefunded() && this.stage() >= stepStage;
  }

  /** The active step is the highest completed one (the current stage). */
  isActive(stepStage: number): boolean {
    return !this.isRefunded() && this.stage() === stepStage;
  }
}
