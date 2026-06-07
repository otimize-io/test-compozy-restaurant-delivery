import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { DemoRole } from '../core/models';
import { RoleService } from '../core/role.service';
import { CartService } from '../consumer/cart.service';

/**
 * The app shell: a top bar with the role switcher (consumer/restaurant/driver) and a cart indicator,
 * plus the router outlet. Switching role updates {@link RoleService}, which the role interceptor reads
 * to stamp `X-Demo-Role` on every gateway call (ADR-002). Only the consumer view is implemented in this
 * task; restaurant/driver route to placeholders filled by task_16.
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

  switchRole(role: DemoRole): void {
    this.roleService.setRole(role);
  }
}
