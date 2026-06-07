import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ApiService } from './api.service';
import { roleInterceptor } from './role.interceptor';
import { RoleService } from './role.service';

describe('ApiService', () => {
  let api: ApiService;
  let http: HttpTestingController;
  let roles: RoleService;
  const base = environment.apiBase;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ApiService,
        RoleService,
        provideHttpClient(withInterceptors([roleInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
    roles = TestBed.inject(RoleService);
  });

  afterEach(() => http.verify());

  it('searchRestaurants calls the gateway search endpoint with the query and returns results', () => {
    const restaurants = [{ id: 'r1', name: 'Pizza Place', cuisine: 'Italian' }];
    let result: unknown;
    api.searchRestaurants('pizza').subscribe((r) => (result = r));

    const req = http.expectOne((r) => r.url === `${base}/api/restaurants`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('q')).toBe('pizza');
    req.flush(restaurants);

    expect(result).toEqual(restaurants);
  });

  it('getRestaurant hits the detail endpoint', () => {
    api.getRestaurant('r1').subscribe();
    const req = http.expectOne(`${base}/api/restaurants/r1`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'r1', name: 'X', cuisine: 'Y' });
  });

  it('getMenu hits the menu endpoint', () => {
    api.getMenu('r1').subscribe();
    const req = http.expectOne(`${base}/api/restaurants/r1/menu`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('placeOrder POSTs the order body and returns the created ids', () => {
    const body = {
      consumerId: 'c1',
      restaurantId: 'r1',
      items: [{ itemId: 'i1', name: 'Burger', quantity: 2, unitPrice: 9.5 }],
      restaurantLocation: { lat: 1, lng: 2 },
    };
    let result: unknown;
    api.placeOrder(body).subscribe((r) => (result = r));

    const req = http.expectOne(`${base}/api/orders`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ orderId: 'o1', correlationId: 'corr1' });

    expect(result).toEqual({ orderId: 'o1', correlationId: 'corr1' });
  });

  it('settlePayment POSTs the settle callback', () => {
    api.settlePayment({ orderId: 'o1', outcome: 'settle' }).subscribe();
    const req = http.expectOne(`${base}/api/payments/callback`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ orderId: 'o1', outcome: 'settle' });
    req.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('getOrder hits the order summary endpoint', () => {
    api.getOrder('o1').subscribe();
    const req = http.expectOne(`${base}/api/orders/o1`);
    expect(req.request.method).toBe('GET');
    req.flush({ orderId: 'o1', status: 1, total: 19 });
  });

  it('getOrderStatus hits the status endpoint used for resync', () => {
    let result: unknown;
    api.getOrderStatus('o1').subscribe((r) => (result = r));
    const req = http.expectOne(`${base}/api/orders/o1/status`);
    expect(req.request.method).toBe('GET');
    req.flush({ orderId: 'o1', stage: 2, stageName: 'Preparing' });
    expect(result).toEqual({ orderId: 'o1', stage: 2, stageName: 'Preparing' });
  });

  it('getRoles hits the demo roles endpoint', () => {
    api.getRoles().subscribe();
    const req = http.expectOne(`${base}/api/demo/roles`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('getRestaurantQueue hits the restaurant queue endpoint and returns the grouped queue', () => {
    const queue = {
      new: [{ orderId: 'o1', status: 2, total: 19, correlationId: 'c1' }],
      inProgress: [],
      ready: [],
    };
    let result: unknown;
    api.getRestaurantQueue().subscribe((r) => (result = r));
    const req = http.expectOne(`${base}/api/restaurant/orders`);
    expect(req.request.method).toBe('GET');
    req.flush(queue);
    expect(result).toEqual(queue);
  });

  it('acceptOrder POSTs to the accept endpoint (202 async)', () => {
    api.acceptOrder('o1').subscribe();
    const req = http.expectOne(`${base}/api/orders/o1/accept`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('markOrderReady POSTs to the ready endpoint (202 async)', () => {
    api.markOrderReady('o1').subscribe();
    const req = http.expectOne(`${base}/api/orders/o1/ready`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('getDriverAssignments hits the assignments endpoint and returns the list', () => {
    const assignments = [
      { orderId: 'o1', status: 6, driverId: 'd1', driverName: 'Alice', etaMinutes: 12, correlationId: 'c1' },
    ];
    let result: unknown;
    api.getDriverAssignments().subscribe((r) => (result = r));
    const req = http.expectOne(`${base}/api/driver/assignments`);
    expect(req.request.method).toBe('GET');
    req.flush(assignments);
    expect(result).toEqual(assignments);
  });

  it('pickupOrder POSTs to the pickup endpoint (202 async)', () => {
    api.pickupOrder('o1').subscribe();
    const req = http.expectOne(`${base}/api/orders/o1/pickup`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('deliverOrder POSTs to the deliver endpoint (202 async)', () => {
    api.deliverOrder('o1').subscribe();
    const req = http.expectOne(`${base}/api/orders/o1/deliver`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('stamps the X-Demo-Role header from the RoleService on outgoing requests', () => {
    roles.setRole('driver');
    api.searchRestaurants('x').subscribe();
    const req = http.expectOne((r) => r.url === `${base}/api/restaurants`);
    expect(req.request.headers.get('X-Demo-Role')).toBe('driver');
    req.flush([]);
  });
});
