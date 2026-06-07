import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { DemoRole } from '../core/models';
import { RoleService } from '../core/role.service';
import { CartService } from '../consumer/cart.service';

/**
 * The app shell: a top bar with the role switcher (consumer/restaurant/driver) and a cart indicator,
 * plus the router outlet. Switching role updates {@link RoleService} (so the role interceptor stamps
 * `X-Demo-Role` on every gateway call, ADR-002) and navigates to that role's view, so one app serves
 * all three views: consumer journey, restaurant order queue, and driver assignments.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, CurrencyPipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly roleService = inject(RoleService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);

  readonly roles = this.roleService.roles;
  readonly role = this.roleService.role;
  readonly cartCount = this.cart.count;
  readonly cartTotal = this.cart.total;
  readonly isConsumer = computed(() => this.role() === 'consumer');

  readonly roleLabels: Record<DemoRole, string> = {
    consumer: 'Consumer',
    restaurant: 'Restaurant',
    driver: 'Driver',
  };

  /** The landing route for each role view (selecting a chip navigates here). */
  private readonly roleRoutes: Record<DemoRole, string> = {
    consumer: '/consumer',
    restaurant: '/restaurant',
    driver: '/driver',
  };

  switchRole(role: DemoRole): void {
    this.roleService.setRole(role);
    void this.router.navigate([this.roleRoutes[role]]);
  }
}
