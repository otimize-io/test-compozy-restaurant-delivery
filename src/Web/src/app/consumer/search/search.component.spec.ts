import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { SearchComponent } from './search.component';

describe('SearchComponent', () => {
  let fixture: ComponentFixture<SearchComponent>;
  let component: SearchComponent;
  let http: HttpTestingController;
  const base = environment.apiBase;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SearchComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Creates the component inside the fake zone so its rxjs debounce timers are flushable. */
  function create(): void {
    fixture = TestBed.createComponent(SearchComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    tick(250); // flush the startWith('') debounce
  }

  it('searches with the empty query on init and renders returned restaurants', fakeAsync(() => {
    create();

    const req = http.expectOne((r) => r.url === `${base}/api/restaurants`);
    expect(req.request.params.get('q')).toBe('');
    req.flush([
      { id: 'r1', name: 'Sushi Spot', cuisine: 'Japanese' },
      { id: 'r2', name: 'Taco Town', cuisine: 'Mexican' },
    ]);
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('[data-testid="restaurant-card"]');
    expect(cards.length).toBe(2);
    expect(cards[0].textContent).toContain('Sushi Spot');
    expect(component.restaurants().length).toBe(2);
  }));

  it('calls the gateway search endpoint with the typed term and renders the result', fakeAsync(() => {
    create();
    http.expectOne((r) => r.url === `${base}/api/restaurants`).flush([]);
    fixture.detectChanges();

    component.query.setValue('pizza');
    tick(250);

    const req = http.expectOne((r) => r.url === `${base}/api/restaurants`);
    expect(req.request.params.get('q')).toBe('pizza');
    req.flush([{ id: 'r9', name: 'Pizza Planet', cuisine: 'Italian' }]);
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('[data-testid="restaurant-card"]');
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Pizza Planet');
  }));

  it('shows the empty-state message when no restaurants match', fakeAsync(() => {
    create();
    http.expectOne((r) => r.url === `${base}/api/restaurants`).flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="empty"]')).toBeTruthy();
  }));

  it('recovers from a failed search without crashing', fakeAsync(() => {
    create();
    http
      .expectOne((r) => r.url === `${base}/api/restaurants`)
      .flush('err', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.restaurants()).toEqual([]);
    expect(component.loading()).toBe(false);
  }));
});
