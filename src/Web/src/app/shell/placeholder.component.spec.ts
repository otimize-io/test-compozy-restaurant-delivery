import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PlaceholderComponent } from './placeholder.component';

describe('PlaceholderComponent', () => {
  let fixture: ComponentFixture<PlaceholderComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PlaceholderComponent],
      providers: [provideRouter([])],
    });
    fixture = TestBed.createComponent(PlaceholderComponent);
  });

  it('renders the deferred role title', () => {
    fixture.componentRef.setInput('title', 'restaurant');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('restaurant view');
    expect(fixture.nativeElement.textContent).toContain('coming soon');
  });

  it('defaults the title when none is provided', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Coming soon');
  });
});
