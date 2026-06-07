import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, IHttpConnectionOptions } from '@microsoft/signalr';

/**
 * Builds a `@microsoft/signalr` {@link HubConnection} for the gateway's `/hubs/orders` hub (ADR-007).
 * Isolated behind an injectable factory so the {@link OrderTrackingStore} can be unit-tested with a
 * mocked connection (no real WebSocket) while production uses the real client with auto-reconnect.
 */
@Injectable({ providedIn: 'root' })
export class HubConnectionFactory {
  create(url: string, options?: IHttpConnectionOptions): HubConnection {
    return new HubConnectionBuilder()
      .withUrl(url, options ?? {})
      .withAutomaticReconnect()
      .build();
  }
}
