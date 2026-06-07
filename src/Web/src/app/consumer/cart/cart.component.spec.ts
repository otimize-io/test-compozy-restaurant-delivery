import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { MenuItem, Restaurant } from '../../core/models';
import { CartService } from '../cart.service';
import { CheckoutService } from '../checkout.service';
import { CartComponent } from './cart.component';

const rest: Restaurant = { id: 'rA', name: 'Burger Barn', cuisine: 'American' };
const burger: MenuItem = { id: 'i1', name: 'Burger', price: 10 };

describe('CartComponent', () => {
  let fixture: ComponentFixture<CartComponent>;
  let component: CartComponent;
  let cart: CartService;
  let checkout: { placeAndPay: jest.Mock };
  let router: Router;

  beforeEach(() => {
    checkout = { placeAndPay: jest.fn() };
    TestBed.configureTestingModule({
      imports: [CartComponent],
      providers: [
        CartService,
        { provide: CheckoutService, useValue: checkout },
        provideRouter([]),
      ],
    });
    cart = TestBed.inject(CartService);
    router = TestBed.inject(Router);
    jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(CartComponent);
    component = fixture.componentInstance;
  });

  it('shows the empty state with no items', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="cart-empty"]')).toBeTruthy();
  });

  it('renders lines and the running total', () => {
    cart.add(rest, burger);
    cart.increment('i1');
    fixture.detectChanges();

    const lines = fixture.nativeElement.querySelectorAll('[data-testid="cart-line"]');
    expect(lines.length).toBe(1);
    expect(fixture.nativeElement.querySelector('[data-testid="qty"]').textContent).toContain('2');
    expect(fixture.nativeElement.querySelector('[data-testid="cart-total"]').textContent).toContain('20');
  });

  it('increment and decrement buttons adjust the quantity', () => {
    cart.add(rest, burger);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="inc"]').click();
    fixture.detectChanges();
    expect(cart.lines()[0].quantity).toBe(2);

    fixture.nativeElement.querySelector('[data-testid="dec"]').click();
    fixture.detectChanges();
    expect(cart.lines()[0].quantity).toBe(1);
  });

  it('removing the last line shows the empty state', () => {
    cart.add(rest, burger);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('[data-testid="remove"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="cart-empty"]')).toBeTruthy();
  });

  it('place & pay places the order and navigates to tracking', async () => {
    checkout.placeAndPay.mockResolvedValue({ orderId: 'o1', correlationId: 'c1' });
    cart.add(rest, burger);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="place-pay"]').click();
    await fixture.whenStable();

    expect(checkout.placeAndPay).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/consumer/track', 'o1']);
  });

  it('shows an error when checkout fails', async () => {
    checkout.placeAndPay.mockRejectedValue(new Error('nope'));
    cart.add(rest, burger);
    fixture.detectChanges();

    await component.placeAndPay();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="checkout-error"]')).toBeTruthy();
    expect(component.placing()).toBe(false);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does nothing on place & pay with an empty cart', async () => {
    fixture.detectChanges();
    await component.placeAndPay();
    expect(checkout.placeAndPay).not.toHaveBeenCalled();
  });
});
