import { Routes } from '@angular/router';
import { ShellComponent } from './shell/shell.component';

/**
 * Top-level routes. Everything renders inside the {@link ShellComponent} (top bar + role switcher).
 * Only the consumer journey is implemented in this task; restaurant/driver land on clean placeholders
 * that task_16 fills in (reusing the shell + SignalR store).
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
        loadComponent: () => import('./shell/placeholder.component').then((m) => m.PlaceholderComponent),
        data: { title: 'restaurant' },
        title: 'Restaurant',
      },
      {
        path: 'driver',
        loadComponent: () => import('./shell/placeholder.component').then((m) => m.PlaceholderComponent),
        data: { title: 'driver' },
        title: 'Driver',
      },
      { path: '**', redirectTo: 'consumer' },
    ],
  },
];
