import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { TrackingStage } from '../../core/models';
import { HubConnectionFactory } from '../../core/signalr/hub-connection.factory';
import { STATUS_CHANGED_METHOD } from '../../core/signalr/order-tracking.store';
import { TrackComponent } from './track.component';

/** Minimal controllable fake hub connection for the tracking screen. */
class FakeHubConnection {
  state = 'Connected';
  handlers: Record<string, (p: unknown) => void> = {};
  on(method: string, handler: (p: unknown) => void): void {
    this.handlers[method] = handler;
  }
  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
  async start(): Promise<void> {
    this.state = 'Connected';
  }
  async stop(): Promise<void> {}
  async invoke(): Promise<void> {}
  push(stage: number, stageName: string): void {
    this.handlers[STATUS_CHANGED_METHOD]?.({ orderId: 'o1', stage, stageName });
  }
}

describe('TrackComponent', () => {
  let fixture: ComponentFixture<TrackComponent>;
  let component: TrackComponent;
  let fake: FakeHubConnection;

  async function create(initialStage = TrackingStage.OrderPlaced): Promise<void> {
    fake = new FakeHubConnection();
    const api = {
      base: 'http://gw',
      getOrderStatus: jest
        .fn()
        .mockReturnValue(of({ orderId: 'o1', stage: initialStage, stageName: 'OrderPlaced' })),
    };
    TestBed.configureTestingModule({
      imports: [TrackComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ApiService, useValue: api },
        { provide: HubConnectionFactory, useValue: { create: () => fake } },
      ],
    });
    fixture = TestBed.createComponent(TrackComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('orderId', 'o1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the 5-stage tracking bar', async () => {
    await create();
    const steps = fixture.nativeElement.querySelectorAll('[data-testid="tracking-step"]');
    expect(steps.length).toBe(5);
    expect(steps[0].textContent).toContain('Order placed');
    expect(steps[4].textContent).toContain('Delivered');
  });

  it('advances the bar to stage 2 on an OrderStatusChanged for Preparing', async () => {
    await create();
    fake.push(TrackingStage.Preparing, 'Preparing');
    fixture.detectChanges();

    expect(component.stage()).toBe(2);
    const steps = fixture.nativeElement.querySelectorAll('[data-testid="tracking-step"]');
    expect(steps[1].getAttribute('data-active')).toBe('true');
    expect(steps[0].getAttribute('data-complete')).toBe('true');
    expect(steps[2].getAttribute('data-complete')).toBe('false');
  });

  it('shows the delivered message when the hub reports delivery', async () => {
    await create();
    fake.push(TrackingStage.Delivered, 'Delivered');
    fixture.detectChanges();

    expect(component.isDelivered()).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="delivered-message"]')).toBeTruthy();
  });

  it('shows the refund banner on the compensation path', async () => {
    await create();
    fake.push(TrackingStage.Refunded, 'Refunded');
    fixture.detectChanges();

    expect(component.isRefunded()).toBe(true);
    expect(fixture.nativeElement.querySelector('[data-testid="refund-banner"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="tracking-bar"]')).toBeFalsy();
  });

  it('reflects the live connection state', async () => {
    await create();
    expect(component.connectionState()).toBe('connected');
    expect(fixture.nativeElement.querySelector('[data-testid="conn-state"]').textContent).toContain('Live');
  });
});
