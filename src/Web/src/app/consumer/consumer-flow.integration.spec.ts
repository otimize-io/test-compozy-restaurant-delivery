import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { Router, RouterOutlet, provideRouter, withComponentInputBinding } from '@angular/router';
import { environment } from '../../environments/environment';
import { roleInterceptor } from '../core/role.interceptor';
import { TrackingStage } from '../core/models';
import { HubConnectionFactory } from '../core/signalr/hub-connection.factory';
import { STATUS_CHANGED_METHOD } from '../core/signalr/order-tracking.store';
import { routes } from '../app.routes';

/** Controllable fake hub used to push the live "delivered" status during the flow. */
class FakeHubConnection {
  state = 'Connected';
  handlers: Record<string, (p: unknown) => void> = {};
  on(method: string, handler: (p: unknown) => void): void {
    this.handlers[method] = handler;
  }
  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
  async start(): Promise<void> {}
  async stop(): Promise<void> {}
  async invoke(): Promise<void> {}
  push(stage: number, stageName: string): void {
    this.handlers[STATUS_CHANGED_METHOD]?.({ orderId: 'o1', stage, stageName });
  }
}

@Component({ selector: 'app-host', standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class HostComponent {}

describe('Consumer flow (integration)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let http: HttpTestingController;
  let router: Router;
  let fake: FakeHubConnection;
  const base = environment.apiBase;

  beforeEach(async () => {
    localStorage.clear();
    fake = new FakeHubConnection();
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        provideHttpClient(withInterceptors([roleInterceptor])),
        provideHttpClientTesting(),
        provideRouter(routes, withComponentInputBinding()),
        { provide: HubConnectionFactory, useValue: { create: () => fake } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(HostComponent);
  });

  function el(selector: string): HTMLElement {
    return fixture.nativeElement.querySelector(selector) as HTMLElement;
  }

  /** Settles pending microtasks/timers (debounce) and renders. */
  async function settle(ms = 0): Promise<void> {
    if (ms) {
      await new Promise((r) => setTimeout(r, ms));
    }
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function navigate(commands: unknown[]): Promise<void> {
    await router.navigate(commands as string[]);
    await settle();
  }

  it('search → add to cart → checkout → pay → tracking reflects a delivered status from the hub', async () => {
    // 1. Land on the consumer search screen.
    await navigate(['/consumer']);
    await settle(300); // search debounce (startWith '')

    http.expectOne((r) => r.url === `${base}/api/restaurants`).flush([
      { id: 'r1', name: 'Burger Barn', cuisine: 'American' },
    ]);
    await settle();
    expect(el('[data-testid="restaurant-card"]').textContent).toContain('Burger Barn');

    // 2. Open the restaurant + menu.
    await navigate(['/consumer/restaurant', 'r1']);
    http.expectOne(`${base}/api/restaurants/r1`).flush({ id: 'r1', name: 'Burger Barn', cuisine: 'American' });
    http
      .expectOne(`${base}/api/restaurants/r1/menu`)
      .flush([{ id: 'i1', name: 'Cheeseburger', price: 12 }]);
    await settle();
    expect(el('[data-testid="menu-item"]')).toBeTruthy();

    // 3. Add an item to the cart.
    el('[data-testid="add-item"]').click();
    await settle();

    // 4. Go to the cart and place + pay.
    await navigate(['/consumer/cart']);
    expect(el('[data-testid="cart-total"]').textContent).toContain('12');

    el('[data-testid="place-pay"]').click();
    await settle();

    http.expectOne(`${base}/api/orders`).flush({ orderId: 'o1', correlationId: 'c1' });
    await settle();
    http.expectOne(`${base}/api/payments/callback`).flush(null, { status: 202, statusText: 'Accepted' });
    await settle();
    // The cart navigates to the tracking route; let it load and start the hub (which resyncs over REST).
    await settle();
    await settle();

    // 5. Now on the tracking screen — the hub starts and resyncs over REST.
    http.expectOne(`${base}/api/orders/o1/status`).flush({ orderId: 'o1', stage: 1, stageName: 'OrderPlaced' });
    await settle();
    expect(el('[data-testid="tracking-bar"]')).toBeTruthy();

    // 6. The hub pushes the delivered status — the bar reflects it.
    fake.push(TrackingStage.Delivered, 'Delivered');
    await settle();
    expect(el('[data-testid="delivered-message"]')).toBeTruthy();

    http.verify();
  });
});
