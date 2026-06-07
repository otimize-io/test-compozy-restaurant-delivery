import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { MenuItem, Restaurant } from '../core/models';
import { CartService } from './cart.service';
import { CheckoutService, CONSUMER_ID } from './checkout.service';

const rest: Restaurant = { id: 'rA', name: 'A', cuisine: 'X', location: { lat: 1, lng: 2 } };
const burger: MenuItem = { id: 'i1', name: 'Burger', price: 10 };

describe('CheckoutService', () => {
  let checkout: CheckoutService;
  let cart: CartService;
  let http: HttpTestingController;
  const base = environment.apiBase;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CheckoutService, CartService, provideHttpClient(), provideHttpClientTesting()],
    });
    checkout = TestBed.inject(CheckoutService);
    cart = TestBed.inject(CartService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('places the order then settles payment and clears the cart', async () => {
    cart.add(rest, burger);
    cart.increment('i1');

    const promise = checkout.placeAndPay();

    const placeReq = http.expectOne(`${base}/api/orders`);
    expect(placeReq.request.method).toBe('POST');
    expect(placeReq.request.body).toEqual({
      consumerId: CONSUMER_ID,
      restaurantId: 'rA',
      items: [{ itemId: 'i1', name: 'Burger', quantity: 2, unitPrice: 10 }],
      restaurantLocation: { lat: 1, lng: 2 },
    });
    placeReq.flush({ orderId: 'o1', correlationId: 'c1' });

    // Let the place observable resolve so the settle request is issued.
    await Promise.resolve();
    const payReq = http.expectOne(`${base}/api/payments/callback`);
    expect(payReq.request.method).toBe('POST');
    expect(payReq.request.body).toEqual({ orderId: 'o1', outcome: 'settle' });
    payReq.flush(null, { status: 202, statusText: 'Accepted' });

    const result = await promise;
    expect(result.orderId).toBe('o1');
    expect(cart.hasItems()).toBe(false);
  });

  it('uses a default location when the restaurant has none', async () => {
    cart.add({ id: 'rB', name: 'B', cuisine: 'Y' }, burger);
    const promise = checkout.placeAndPay();

    const placeReq = http.expectOne(`${base}/api/orders`);
    expect(placeReq.request.body.restaurantLocation).toEqual({ lat: 0, lng: 0 });
    placeReq.flush({ orderId: 'o2', correlationId: 'c2' });

    await Promise.resolve();
    http.expectOne(`${base}/api/payments/callback`).flush(null, { status: 202, statusText: 'Accepted' });

    await promise;
  });

  it('rejects when the cart is empty', async () => {
    await expect(checkout.placeAndPay()).rejects.toThrow('Cart is empty');
    http.expectNone(`${base}/api/orders`);
  });
});
