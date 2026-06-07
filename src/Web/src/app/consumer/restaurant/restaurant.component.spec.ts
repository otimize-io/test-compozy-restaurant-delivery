import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { CartService } from '../cart.service';
import { RestaurantComponent } from './restaurant.component';

describe('RestaurantComponent', () => {
  let fixture: ComponentFixture<RestaurantComponent>;
  let component: RestaurantComponent;
  let http: HttpTestingController;
  let cart: CartService;
  const base = environment.apiBase;

  async function load(): Promise<void> {
    fixture = TestBed.createComponent(RestaurantComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('id', 'r1');
    fixture.detectChanges();

    http.expectOne(`${base}/api/restaurants/r1`).flush({ id: 'r1', name: 'Burger Barn', cuisine: 'American' });
    http.expectOne(`${base}/api/restaurants/r1/menu`).flush([
      { id: 'i1', name: 'Cheeseburger', price: 12, description: 'Juicy' },
      { id: 'i2', name: 'Fries', price: 5 },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [RestaurantComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), CartService],
    });
    http = TestBed.inject(HttpTestingController);
    cart = TestBed.inject(CartService);
  });

  afterEach(() => http.verify());

  it('loads and renders the restaurant detail and menu', async () => {
    await load();
    expect(fixture.nativeElement.textContent).toContain('Burger Barn');
    const items = fixture.nativeElement.querySelectorAll('[data-testid="menu-item"]');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Cheeseburger');
  });

  it('adding an item puts it in the cart with the right total', async () => {
    await load();
    const addButtons = fixture.nativeElement.querySelectorAll('[data-testid="add-item"]');
    addButtons[0].click();
    fixture.detectChanges();

    expect(cart.count()).toBe(1);
    expect(cart.total()).toBe(12);
    expect(cart.restaurant()?.id).toBe('r1');
    expect(component.quantityOf('i1')).toBe(1);
    // The add button reflects the quantity badge.
    expect(addButtons[0].textContent).toContain('1');
  });

  it('shows an error message when the load fails', async () => {
    fixture = TestBed.createComponent(RestaurantComponent);
    fixture.componentRef.setInput('id', 'rX');
    fixture.detectChanges();
    http.expectOne(`${base}/api/restaurants/rX`).flush('x', { status: 500, statusText: 'err' });
    http.expectOne(`${base}/api/restaurants/rX/menu`).flush('x', { status: 500, statusText: 'err' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="restaurant-error"]')).toBeTruthy();
  });
});
