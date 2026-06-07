/**
 * Production environment.
 *
 * `apiBase` is the API Gateway / BFF base URL. The Angular app talks ONLY to the gateway
 * (REST + the SignalR hub at `${apiBase}/hubs/orders`) — never to the backend services directly
 * (ADR-005). Override this value per deployment.
 */
export const environment = {
  production: true,
  apiBase: 'http://localhost:5000',
};
