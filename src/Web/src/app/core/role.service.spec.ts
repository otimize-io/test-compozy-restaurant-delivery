import { TestBed } from '@angular/core/testing';
import { RoleService } from './role.service';

describe('RoleService', () => {
  let service: RoleService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [RoleService] });
    service = TestBed.inject(RoleService);
  });

  it('defaults to the consumer role', () => {
    expect(service.current()).toBe('consumer');
    expect(service.role()).toBe('consumer');
  });

  it('exposes the three switchable roles', () => {
    expect(service.roles).toEqual(['consumer', 'restaurant', 'driver']);
  });

  it('switches the role and updates the signal', () => {
    service.setRole('restaurant');
    expect(service.current()).toBe('restaurant');
    expect(service.role()).toBe('restaurant');
  });

  it('persists the role to localStorage', () => {
    service.setRole('driver');
    expect(localStorage.getItem('demo-role')).toBe('driver');
  });

  it('restores a persisted role on construction', () => {
    localStorage.setItem('demo-role', 'driver');
    const fresh = new RoleService();
    expect(fresh.current()).toBe('driver');
  });

  it('falls back to consumer when the stored value is invalid', () => {
    localStorage.setItem('demo-role', 'bogus');
    const fresh = new RoleService();
    expect(fresh.current()).toBe('consumer');
  });
});
