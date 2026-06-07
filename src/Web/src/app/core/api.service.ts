import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  DemoIdentity,
  MenuItem,
  OrderStatus,
  OrderSummary,
  PaymentCallbackRequest,
  PlaceOrderRequest,
  PlaceOrderResponse,
  Restaurant,
} from './models';

/**
 * The single HTTP client for the API Gateway / BFF. Every gateway REST call goes through here so the
 * base URL (from {@link environment}) and the contract live in one place; the `X-Demo-Role` header is
 * added separately by the {@link roleInterceptor}. The app NEVER calls backend services directly — only
 * the gateway endpoints below (ADR-005).
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  /** The gateway base URL (configurable per environment). */
  readonly base = environment.apiBase;

  /** `GET /api/demo/roles` — the pre-seeded demo identities for the role switcher. */
  getRoles(): Observable<DemoIdentity[]> {
    return this.http.get<DemoIdentity[]>(`${this.base}/api/demo/roles`);
  }

  /** `GET /api/restaurants?q=` — search/browse restaurants (PRD F1). */
  searchRestaurants(query: string): Observable<Restaurant[]> {
    const params = new HttpParams().set('q', query ?? '');
    return this.http.get<Restaurant[]>(`${this.base}/api/restaurants`, { params });
  }

  /** `GET /api/restaurants/{id}` — restaurant detail (PRD F2). */
  getRestaurant(id: string): Observable<Restaurant> {
    return this.http.get<Restaurant>(`${this.base}/api/restaurants/${id}`);
  }

  /** `GET /api/restaurants/{id}/menu` — the restaurant's menu items (PRD F2). */
  getMenu(restaurantId: string): Observable<MenuItem[]> {
    return this.http.get<MenuItem[]>(`${this.base}/api/restaurants/${restaurantId}/menu`);
  }

  /** `POST /api/orders` — places the order and starts the saga (PRD F3). Returns 201. */
  placeOrder(body: PlaceOrderRequest): Observable<PlaceOrderResponse> {
    return this.http.post<PlaceOrderResponse>(`${this.base}/api/orders`, body);
  }

  /** `POST /api/payments/callback` — simulate the PSP settling the payment (PRD F4). Returns 202. */
  settlePayment(body: PaymentCallbackRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/api/payments/callback`, body);
  }

  /** `GET /api/orders/{id}` — current order summary (status int enum + total). */
  getOrder(orderId: string): Observable<OrderSummary> {
    return this.http.get<OrderSummary>(`${this.base}/api/orders/${orderId}`);
  }

  /** `GET /api/orders/{id}/status` — the 5-stage tracking view; used to resync on (re)connect (ADR-007). */
  getOrderStatus(orderId: string): Observable<OrderStatus> {
    return this.http.get<OrderStatus>(`${this.base}/api/orders/${orderId}/status`);
  }
}
