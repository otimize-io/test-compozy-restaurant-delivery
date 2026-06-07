import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { Router, RouterOutlet, provideRouter, withComponentInputBinding } from '@angular/router';
import { environment } from '../environments/environment';
import { roleInterceptor } from './core/role.interceptor';
import { OrderStatusCode, TrackingStage } from './core/models';
import { HubConnectionFactory } from './core/signalr/hub-connection.factory';
import { STATUS_CHANGED_METHOD } from './core/signalr/order-tracking.store';
import { routes } from './app.routes';

/**
 * A single gateway hub shared by every connection the views open. Like the real gateway, a status change
 * is broadcast to all subscribers, so one order advancing is reflected in every role view — the
 * "one order, three live views" guarantee (PRD F8 / ADR-007). Each `create()` returns a connection that
 * registers itself with this hub.
 */
class SharedFakeHub {
  readonly connections = new Set<FakeConnection>();

  create(): FakeConnection {
    const conn = new FakeConnection(() => this.connections.delete(conn));
    this.connections.add(conn);
    return conn;
  }

  /** Broadcast an OrderStatusChanged to every open connection (as the gateway does to all groups). */
  broadcast(orderId: string, stage: number, stageName: string): void {
    for (const conn of this.connections) {
      conn.receive({ orderId, stage, stageName });
    }
  }
}

class FakeConnection {
  state = 'Connected';
  private handlers: Record<string, (p: unknown) => void> = {};
  constructor(private readonly onStop: () => void) {}
  on(method: string, handler: (p: unknown) => void): void {
    this.handlers[method] = handler;
  }
  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
  async start(): Promise<void> {
    this.state = 'Connected';
  }
  async stop(): Promise<void> {
    // A stopped connection leaves the hub and receives no further broadcasts (as in production).
    this.state = 'Disconnected';
    this.onStop();
  }
  async invoke(): Promise<void> {}
  receive(payload: unknown): void {
    this.handlers[STATUS_CHANGED_METHOD]?.(payload);
  }
}

@Component({ selector: 'app-host', standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class HostComponent {}

describe('Three live views (integration)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let http: HttpTestingController;
  let router: Router;
  let hub: SharedFakeHub;
  const base = environment.apiBase;

  beforeEach(() => {
    localStorage.clear();
    hub = new SharedFakeHub();
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        provideHttpClient(withInterceptors([roleInterceptor])),
        provideHttpClientTesting(),
        provideRouter(routes, withComponentInputBinding()),
        { provide: HubConnectionFactory, useValue: { create: () => hub.create() } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(HostComponent);
  });

  function el(selector: string): HTMLElement {
    return fixture.nativeElement.querySelector(selector) as HTMLElement;
  }

  async function settle(): Promise<void> {
    for (let i = 0; i < 12; i++) {
      await Promise.resolve();
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  async function navigate(commands: string[]): Promise<void> {
    await router.navigate(commands);
    await settle();
  }

  it('restaurant accepts an order and the gateway push advances the consumer tracking bar live', async () => {
    // --- Restaurant view: load the queue and accept the New order. ---
    await navigate(['/restaurant']);

    http.expectOne(`${base}/api/restaurant/orders`).flush({
      new: [{ orderId: 'o1', status: OrderStatusCode.Paid, total: 20, correlationId: 'c1' }],
      inProgress: [],
      ready: [],
    });
    await settle();
    expect(el('[data-testid="accept-btn"]')).toBeTruthy();

    el('[data-testid="accept-btn"]').click();
    await settle();

    // The accept POST (202) fires, then the queue refreshes with the order now In-Progress.
    http.expectOne(`${base}/api/orders/o1/accept`).flush(null, { status: 202, statusText: 'Accepted' });
    await settle();
    http.expectOne(`${base}/api/restaurant/orders`).flush({
      new: [],
      inProgress: [{ orderId: 'o1', status: OrderStatusCode.Preparing, total: 20, correlationId: 'c1' }],
      ready: [],
    });
    await settle();

    const inProgress = el('[data-testid="column-in-progress"]');
    expect(inProgress.querySelector('[data-testid="order-card"]')).toBeTruthy();

    // --- Consumer view: the same order, tracked live. ---
    await navigate(['/consumer/track', 'o1']);
    // The tracking store resyncs the current stage over REST on connect.
    http.expectOne(`${base}/api/orders/o1/status`).flush({ orderId: 'o1', stage: TrackingStage.OrderPlaced, stageName: 'OrderPlaced' });
    await settle();
    expect(el('[data-testid="tracking-bar"]')).toBeTruthy();

    // The gateway broadcasts the "Preparing" change (driven by the restaurant accept) to all views.
    hub.broadcast('o1', TrackingStage.Preparing, 'Preparing');
    await settle();

    const steps = fixture.nativeElement.querySelectorAll('[data-testid="tracking-step"]');
    expect(steps[1].getAttribute('data-active')).toBe('true');
    expect(steps[0].getAttribute('data-complete')).toBe('true');

    // Drive the order to delivered live.
    hub.broadcast('o1', TrackingStage.Delivered, 'Delivered');
    await settle();
    expect(el('[data-testid="delivered-message"]')).toBeTruthy();

    http.verify();
  });
});
