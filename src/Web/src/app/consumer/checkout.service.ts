import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { GeoPoint, PlaceOrderResponse } from '../core/models';
import { CartService } from './cart.service';

/** The pre-seeded consumer id (matches the gateway's DemoIdentities consumer). */
export const CONSUMER_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

/** A fixed fallback restaurant location used when the catalog omits coordinates (mock PoC). */
const DEFAULT_LOCATION: GeoPoint = { lat: 0, lng: 0 };

/**
 * Drives checkout (PRD F3/F4): places the order, then settles the payment to simulate the PSP
 * confirming (payment-before-dispatch). The order proceeds only after this settle call. Returns the
 * created order id so the caller can navigate to the live tracking screen.
 */
@Injectable({ providedIn: 'root' })
export class CheckoutService {
  private readonly api = inject(ApiService);
  private readonly cart = inject(CartService);

  /**
   * Places the current cart as an order, then posts the mock payment settlement. The cart is cleared
   * on success. Throws if the cart is empty or any gateway call fails.
   */
  async placeAndPay(): Promise<PlaceOrderResponse> {
    const restaurant = this.cart.restaurant();
    if (!restaurant || !this.cart.hasItems()) {
      throw new Error('Cart is empty');
    }

    const order = await firstValueFrom(
      this.api.placeOrder({
        consumerId: CONSUMER_ID,
        restaurantId: restaurant.id,
        items: this.cart.toOrderItems(),
        restaurantLocation: restaurant.location ?? DEFAULT_LOCATION,
      }),
    );

    // Simulate the PSP settling the payment so the saga can proceed past AwaitingPayment (PRD F4).
    await firstValueFrom(this.api.settlePayment({ orderId: order.orderId, outcome: 'settle' }));

    this.cart.clear();
    return order;
  }
}
