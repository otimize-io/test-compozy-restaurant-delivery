import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CheckoutService } from '../checkout.service';
import { CartService } from '../cart.service';

/**
 * Cart review + checkout/payment (PRD F3/F4). Lets the consumer adjust quantities with a live running
 * total, then "Place order & pay" which places the order and settles the mock payment, before
 * navigating to the live tracking screen.
 */
@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss',
})
export class CartComponent {
  private readonly cart = inject(CartService);
  private readonly checkout = inject(CheckoutService);
  private readonly router = inject(Router);

  readonly lines = this.cart.lines;
  readonly total = this.cart.total;
  readonly count = this.cart.count;
  readonly hasItems = this.cart.hasItems;
  readonly restaurant = this.cart.restaurant;

  readonly placing = signal(false);
  readonly error = signal<string | null>(null);

  increment(itemId: string): void {
    this.cart.increment(itemId);
  }

  decrement(itemId: string): void {
    this.cart.decrement(itemId);
  }

  remove(itemId: string): void {
    this.cart.remove(itemId);
  }

  async placeAndPay(): Promise<void> {
    if (!this.hasItems() || this.placing()) {
      return;
    }
    this.placing.set(true);
    this.error.set(null);
    try {
      const order = await this.checkout.placeAndPay();
      await this.router.navigate(['/consumer/track', order.orderId]);
    } catch {
      this.error.set('Something went wrong placing your order. Please try again.');
      this.placing.set(false);
    }
  }
}
