import { Injectable, signal } from '@angular/core';
import { DemoRole } from './models';

/** The header the gateway reads to select the active demo identity (ADR-002). */
export const DEMO_ROLE_HEADER = 'X-Demo-Role';

const STORAGE_KEY = 'demo-role';

/**
 * Holds the active demo role for the role switcher (ADR-002). The value is exposed as a signal so the
 * shell can render the current role reactively, and is read by the {@link roleInterceptor} to stamp the
 * `X-Demo-Role` header on every outgoing gateway request. The selection is persisted to localStorage so
 * a reload keeps the chosen role.
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  readonly roles: readonly DemoRole[] = ['consumer', 'restaurant', 'driver'];

  private readonly _role = signal<DemoRole>(this.readInitial());
  /** The current active role (read-only signal for the shell). */
  readonly role = this._role.asReadonly();

  /** Switches the active role and persists it. */
  setRole(role: DemoRole): void {
    this._role.set(role);
    this.persist(role);
  }

  /** The current role value (used by the interceptor). */
  current(): DemoRole {
    return this._role();
  }

  private readInitial(): DemoRole {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'consumer' || stored === 'restaurant' || stored === 'driver') {
        return stored;
      }
    } catch {
      // localStorage may be unavailable (e.g. SSR/tests); fall back to the default.
    }
    return 'consumer';
  }

  private persist(role: DemoRole): void {
    try {
      localStorage.setItem(STORAGE_KEY, role);
    } catch {
      // Ignore persistence failures — the in-memory signal is the source of truth.
    }
  }
}
