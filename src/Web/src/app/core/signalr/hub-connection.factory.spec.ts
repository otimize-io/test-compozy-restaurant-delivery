import { TestBed } from '@angular/core/testing';
import { HubConnection } from '@microsoft/signalr';
import { HubConnectionFactory } from './hub-connection.factory';

describe('HubConnectionFactory', () => {
  let factory: HubConnectionFactory;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [HubConnectionFactory] });
    factory = TestBed.inject(HubConnectionFactory);
  });

  it('builds a HubConnection for the given hub URL', () => {
    const connection = factory.create('http://localhost:5000/hubs/orders');
    expect(connection).toBeInstanceOf(HubConnection);
    expect(connection.baseUrl).toBe('http://localhost:5000/hubs/orders');
  });

  it('accepts connection options (e.g. headers)', () => {
    const connection = factory.create('http://gw/hubs/orders', { headers: { 'X-Demo-Role': 'consumer' } });
    expect(connection).toBeInstanceOf(HubConnection);
  });
});
