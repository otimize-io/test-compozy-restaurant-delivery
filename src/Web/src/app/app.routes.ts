import { Routes } from '@angular/router';
import { ShellComponent } from './shell/shell.component';

/**
 * Top-level routes. Everything renders inside the {@link ShellComponent} (top bar + role switcher).
 * The consumer journey, the restaurant order queue, and the driver assignment view are all implemented;
 * the restaurant/driver views are lazy-loaded standalone components that reuse the shell + SignalR store.
 */
export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'consumer' },
      {
        path: 'consumer',
        loadChildren: () => import('./consumer/consumer.routes').then((m) => m.consumerRoutes),
      },
      {
        path: 'restaurant',
        loadComponent: () =>
          import('./restaurant/restaurant-queue.component').then((m) => m.RestaurantQueueComponent),
        title: 'Restaurant',
      },
      {
        path: 'driver',
        loadComponent: () =>
          import('./driver/driver-assignments.component').then((m) => m.DriverAssignmentsComponent),
        title: 'Driver',
      },
      { path: '**', redirectTo: 'consumer' },
    ],
  },
];
