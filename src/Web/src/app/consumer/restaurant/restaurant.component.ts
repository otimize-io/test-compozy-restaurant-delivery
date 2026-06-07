import { CurrencyPipe } from '@angular/common';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { MenuItem, Restaurant } from '../../core/models';
import { CartService } from '../cart.service';

/**
 * Restaurant detail + menu browsing (PRD F2). Loads the restaurant and its menu from the gateway and
 * lets the consumer add items to the cart (running total maintained by {@link CartService}). The route
 * id is bound via component input binding (`withComponentInputBinding`).
 */
@Component({
  selector: 'app-restaurant',
  standalone: true,
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './restaurant.component.html',
  styleUrl: './restaurant.component.scss',
})
export class RestaurantComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly cartService = inject(CartService);

  /** Route param `:id` bound via component input binding. */
  readonly id = input.required<string>();

  readonly restaurant = signal<Restaurant | null>(null);
  readonly menu = signal<MenuItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly cartCount = this.cartService.count;
  readonly cartTotal = this.cartService.total;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [restaurant, menu] = await Promise.all([
        firstValueFrom(this.api.getRestaurant(this.id())),
        firstValueFrom(this.api.getMenu(this.id())),
      ]);
      this.restaurant.set(restaurant);
      this.menu.set(menu);
    } catch {
      this.error.set('Could not load this restaurant. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  add(item: MenuItem): void {
    const restaurant = this.restaurant();
    if (restaurant) {
      this.cartService.add(restaurant, item);
    }
  }

  /** Current quantity of an item in the cart (for the badge). */
  quantityOf(itemId: string): number {
    return this.cartService.lines().find((l) => l.item.id === itemId)?.quantity ?? 0;
  }
}
