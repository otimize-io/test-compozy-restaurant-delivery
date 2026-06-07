import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MenuItem, Restaurant } from '../core/models';
import { RoleService } from '../core/role.service';
import { CartService } from '../consumer/cart.service';
import { ShellComponent } from './shell.component';

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;
  let component: ShellComponent;
  let roles: RoleService;
  let cart: CartService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [RoleService, CartService, provideRouter([])],
    });
    roles = TestBed.inject(RoleService);
    cart = TestBed.inject(CartService);
    fixture = TestBed.createComponent(ShellComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the three role chips with consumer active by default', () => {
    const chips = fixture.nativeElement.querySelectorAll('.role-chip');
    expect(chips.length).toBe(3);
    const active = fixture.nativeElement.querySelector('.role-chip--active');
    expect(active.getAttribute('data-role')).toBe('consumer');
  });

  it('clicking a role chip switches the active role (drives the X-Demo-Role header)', () => {
    const driverChip: HTMLButtonElement = fixture.nativeElement.querySelector('[data-role="driver"]');
    driverChip.click();
    fixture.detectChanges();

    expect(roles.current()).toBe('driver');
    expect(component.role()).toBe('driver');
    expect(fixture.nativeElement.querySelector('.role-chip--active').getAttribute('data-role')).toBe('driver');
  });

  it('shows the cart pill with the live count for the consumer role', () => {
    const rest: Restaurant = { id: 'r', name: 'R', cuisine: 'C' };
    const item: MenuItem = { id: 'i', name: 'I', price: 8 };
    cart.add(rest, item);
    cart.add(rest, item);
    fixture.detectChanges();

    const count = fixture.nativeElement.querySelector('[data-testid="cart-count"]');
    expect(count.textContent).toContain('2');
  });

  it('hides the cart pill when not the consumer role', () => {
    roles.setRole('restaurant');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.cart-pill')).toBeFalsy();
  });
});
