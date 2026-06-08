import { Routes } from '@angular/router';

/**
 * Consumer journey routes (PRD F1–F4, F8): search → restaurant/menu → cart → my orders → live tracking.
 * Lazy-loaded standalone components keep each screen in its own chunk.
 */
export const consumerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./search/search.component').then((m) => m.SearchComponent),
    title: 'Search restaurants',
  },
  {
    path: 'restaurant/:id',
    loadComponent: () => import('./restaurant/restaurant.component').then((m) => m.RestaurantComponent),
    title: 'Restaurant menu',
  },
  {
    path: 'cart',
    loadComponent: () => import('./cart/cart.component').then((m) => m.CartComponent),
    title: 'Your cart',
  },
  {
    path: 'orders',
    loadComponent: () => import('./orders/orders.component').then((m) => m.OrdersComponent),
    title: 'My orders',
  },
  {
    path: 'track/:orderId',
    loadComponent: () => import('./track/track.component').then((m) => m.TrackComponent),
    title: 'Track your order',
  },
];
