import { TestBed } from '@angular/core/testing';
import { MenuItem, Restaurant } from '../core/models';
import { CartService } from './cart.service';

const restA: Restaurant = { id: 'rA', name: 'A', cuisine: 'X', location: { lat: 1, lng: 2 } };
const restB: Restaurant = { id: 'rB', name: 'B', cuisine: 'Y' };
const burger: MenuItem = { id: 'i1', name: 'Burger', price: 10 };
const fries: MenuItem = { id: 'i2', name: 'Fries', price: 4.5 };

describe('CartService', () => {
  let cart: CartService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [CartService] });
    cart = TestBed.inject(CartService);
  });

  it('starts empty', () => {
    expect(cart.hasItems()).toBe(false);
    expect(cart.total()).toBe(0);
    expect(cart.count()).toBe(0);
    expect(cart.restaurant()).toBeNull();
  });

  it('adding items updates the running total and count', () => {
    cart.add(restA, burger);
    cart.add(restA, fries);
    expect(cart.count()).toBe(2);
    expect(cart.total()).toBe(14.5);
    expect(cart.restaurant()).toEqual(restA);
  });

  it('adding the same item twice increments its quantity (not a duplicate line)', () => {
    cart.add(restA, burger);
    cart.add(restA, burger);
    expect(cart.lines().length).toBe(1);
    expect(cart.lines()[0].quantity).toBe(2);
    expect(cart.total()).toBe(20);
    expect(cart.count()).toBe(2);
  });

  it('adding from a different restaurant clears the previous cart', () => {
    cart.add(restA, burger);
    cart.add(restB, fries);
    expect(cart.restaurant()).toEqual(restB);
    expect(cart.lines().length).toBe(1);
    expect(cart.total()).toBe(4.5);
  });

  it('increment and decrement adjust quantity and total', () => {
    cart.add(restA, burger);
    cart.increment('i1');
    expect(cart.lines()[0].quantity).toBe(2);
    expect(cart.total()).toBe(20);
    cart.decrement('i1');
    expect(cart.lines()[0].quantity).toBe(1);
    expect(cart.total()).toBe(10);
  });

  it('decrementing to zero removes the line and clears the restaurant when empty', () => {
    cart.add(restA, burger);
    cart.decrement('i1');
    expect(cart.hasItems()).toBe(false);
    expect(cart.restaurant()).toBeNull();
  });

  it('setQuantity sets an absolute quantity', () => {
    cart.add(restA, burger);
    cart.setQuantity('i1', 5);
    expect(cart.lines()[0].quantity).toBe(5);
    expect(cart.total()).toBe(50);
  });

  it('setQuantity to zero removes the line', () => {
    cart.add(restA, burger);
    cart.add(restA, fries);
    cart.setQuantity('i1', 0);
    expect(cart.lines().length).toBe(1);
    expect(cart.lines()[0].item.id).toBe('i2');
  });

  it('remove deletes a line', () => {
    cart.add(restA, burger);
    cart.add(restA, fries);
    cart.remove('i1');
    expect(cart.lines().length).toBe(1);
    expect(cart.total()).toBe(4.5);
  });

  it('clear empties the cart', () => {
    cart.add(restA, burger);
    cart.clear();
    expect(cart.hasItems()).toBe(false);
    expect(cart.restaurant()).toBeNull();
  });

  it('decrement on a missing item is a no-op', () => {
    cart.decrement('nope');
    expect(cart.hasItems()).toBe(false);
  });

  it('toOrderItems maps lines to the gateway order DTO', () => {
    cart.add(restA, burger);
    cart.increment('i1');
    cart.add(restA, fries);
    expect(cart.toOrderItems()).toEqual([
      { itemId: 'i1', name: 'Burger', quantity: 2, unitPrice: 10 },
      { itemId: 'i2', name: 'Fries', quantity: 1, unitPrice: 4.5 },
    ]);
  });
});
