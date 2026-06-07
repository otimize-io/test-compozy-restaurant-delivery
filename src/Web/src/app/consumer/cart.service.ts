import { computed, Injectable, signal } from '@angular/core';
import { CartLine, MenuItem, OrderItemDto, Restaurant } from '../core/models';

/**
 * Holds the consumer's cart (PRD F3). Tracks the restaurant being ordered from (single-restaurant cart —
 * multi-restaurant is a Non-Goal), the lines with quantities, and a reactive running total. Exposed via
 * signals so the menu, cart, and checkout screens stay in sync.
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly _restaurant = signal<Restaurant | null>(null);
  private readonly _lines = signal<CartLine[]>([]);

  /** The restaurant the current cart belongs to (null when empty). */
  readonly restaurant = this._restaurant.asReadonly();
  /** The cart lines (item + quantity). */
  readonly lines = this._lines.asReadonly();
  /** Total quantity of items across all lines. */
  readonly count = computed(() => this._lines().reduce((sum, l) => sum + l.quantity, 0));
  /** Running cart total. */
  readonly total = computed(() => this._lines().reduce((sum, l) => sum + l.quantity * l.item.price, 0));
  /** True when the cart has at least one item. */
  readonly hasItems = computed(() => this._lines().length > 0);

  /** Adds one unit of an item, switching restaurant (clearing the cart) if it belongs to another. */
  add(restaurant: Restaurant, item: MenuItem): void {
    if (this._restaurant()?.id !== restaurant.id) {
      this._restaurant.set(restaurant);
      this._lines.set([]);
    }
    const lines = [...this._lines()];
    const existing = lines.find((l) => l.item.id === item.id);
    if (existing) {
      existing.quantity += 1;
      this._lines.set([...lines]);
    } else {
      this._lines.set([...lines, { item, quantity: 1 }]);
    }
  }

  /** Sets the absolute quantity for an item; removing it when quantity <= 0. */
  setQuantity(itemId: string, quantity: number): void {
    if (quantity <= 0) {
      this.remove(itemId);
      return;
    }
    this._lines.set(this._lines().map((l) => (l.item.id === itemId ? { ...l, quantity } : l)));
  }

  /** Increments an item's quantity by one. */
  increment(itemId: string): void {
    this._lines.set(this._lines().map((l) => (l.item.id === itemId ? { ...l, quantity: l.quantity + 1 } : l)));
  }

  /** Decrements an item's quantity by one, removing it at zero. */
  decrement(itemId: string): void {
    const line = this._lines().find((l) => l.item.id === itemId);
    if (!line) {
      return;
    }
    this.setQuantity(itemId, line.quantity - 1);
  }

  /** Removes an item line entirely. */
  remove(itemId: string): void {
    const lines = this._lines().filter((l) => l.item.id !== itemId);
    this._lines.set(lines);
    if (lines.length === 0) {
      this._restaurant.set(null);
    }
  }

  /** Empties the cart. */
  clear(): void {
    this._lines.set([]);
    this._restaurant.set(null);
  }

  /** Maps the cart lines to the order-item DTOs the gateway expects. */
  toOrderItems(): OrderItemDto[] {
    return this._lines().map((l) => ({
      itemId: l.item.id,
      name: l.item.name,
      quantity: l.quantity,
      unitPrice: l.item.price,
    }));
  }
}
