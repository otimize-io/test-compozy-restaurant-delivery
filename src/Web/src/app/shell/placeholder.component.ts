import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * A clean placeholder for the restaurant and driver views (ADR-002 functional-clean sides). These are
 * deferred to task_16, which fills them in reusing the shell and the SignalR tracking store. The route
 * is wired now so the role switcher has somewhere to land.
 */
@Component({
  selector: 'app-placeholder',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="container">
      <div class="card placeholder">
        <h1>{{ title() }} view</h1>
        <p class="muted">
          This view is coming soon (task_16). The shell, role switcher, and the SignalR tracking store
          are ready for it to reuse.
        </p>
        <a class="btn btn-primary" routerLink="/consumer">Go to the consumer app</a>
      </div>
    </section>
  `,
  styles: [
    `
      .placeholder {
        padding: 2.5rem;
        text-align: center;
        margin-top: 2rem;
      }
      .placeholder h1 {
        margin-top: 0;
        text-transform: capitalize;
      }
      .btn {
        margin-top: 1rem;
      }
    `,
  ],
})
export class PlaceholderComponent {
  /** The role name to display (restaurant or driver). */
  readonly title = input<string>('Coming soon');
}
