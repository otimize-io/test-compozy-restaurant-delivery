/**
 * Production environment.
 *
 * `apiBase` is the API Gateway / BFF base URL. The Angular app talks ONLY to the gateway
 * (REST + the SignalR hub at `${apiBase}/hubs/orders`) — never to the backend services directly
 * (ADR-005). Override this value per deployment.
 */
export const environment = {
  production: true,
  // The browser reaches the YARP gateway on its HOST-published port. In docker-compose the
  // gateway is published as 8080:8080, so the containerised web app (served by nginx on
  // host port 4200) calls the gateway at http://localhost:8080 (REST + SignalR /hubs/orders).
  apiBase: 'http://localhost:8080',
};
