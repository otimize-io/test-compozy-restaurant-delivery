import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { roleInterceptor } from './role.interceptor';
import { RoleService } from './role.service';

describe('roleInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let roles: RoleService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        RoleService,
        provideHttpClient(withInterceptors([roleInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    roles = TestBed.inject(RoleService);
  });

  afterEach(() => controller.verify());

  it('adds the X-Demo-Role header with the default consumer role', () => {
    http.get('/x').subscribe();
    const req = controller.expectOne('/x');
    expect(req.request.headers.get('X-Demo-Role')).toBe('consumer');
    req.flush({});
  });

  it('reflects the active role chosen in the switcher', () => {
    roles.setRole('restaurant');
    http.get('/y').subscribe();
    const req = controller.expectOne('/y');
    expect(req.request.headers.get('X-Demo-Role')).toBe('restaurant');
    req.flush({});
  });
});
