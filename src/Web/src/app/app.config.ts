import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { roleInterceptor } from './core/role.interceptor';
import { routes } from './app.routes';

/**
 * App-wide providers: the router (with component input binding so route params/data bind to component
 * inputs), and the HttpClient with the {@link roleInterceptor} that stamps `X-Demo-Role` on every
 * gateway request (ADR-002).
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([roleInterceptor])),
  ],
};
