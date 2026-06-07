import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { DEMO_ROLE_HEADER, RoleService } from './role.service';

/**
 * Stamps the active demo role onto every outgoing gateway request as the `X-Demo-Role` header
 * (ADR-002 role switcher). The gateway resolves the pre-seeded identity from this header, so a single
 * app serves all three role views without authentication. The interceptor is the single place the
 * role header is applied, keeping role concerns out of the components and the ApiService.
 */
export const roleInterceptor: HttpInterceptorFn = (req, next) => {
  const role = inject(RoleService).current();
  return next(req.clone({ setHeaders: { [DEMO_ROLE_HEADER]: role } }));
};
