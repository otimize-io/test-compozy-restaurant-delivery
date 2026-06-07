import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, of, startWith, switchMap, tap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Restaurant } from '../../core/models';

/**
 * Restaurant search & discovery (PRD F1). A typed reactive form drives a debounced query against the
 * gateway search endpoint; results render as cards linking to the restaurant/menu screen.
 */
@Component({
  selector: 'app-search',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './search.component.html',
  styleUrl: './search.component.scss',
})
export class SearchComponent {
  private readonly api = inject(ApiService);

  /** Typed search box. */
  readonly query = new FormControl<string>('', { nonNullable: true });
  readonly loading = signal(false);

  /** Debounced restaurant results from the gateway search endpoint. */
  readonly restaurants = toSignal(
    this.query.valueChanges.pipe(
      startWith(this.query.value),
      debounceTime(250),
      distinctUntilChanged(),
      tap(() => this.loading.set(true)),
      switchMap((q) =>
        this.api.searchRestaurants(q).pipe(
          catchError(() => of([] as Restaurant[])),
          tap(() => this.loading.set(false)),
        ),
      ),
    ),
    { initialValue: [] as Restaurant[] },
  );
}
