import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, UrlTree } from '@angular/router';
import { AuthGuard } from '../../auth/guards/auth.guard';
import { API_BASE_URL } from './api-base-url.token';
import { homeworkInterceptors, homeworkProviders } from './homework.providers';

describe('production/default homework providers', () => {
  let originalUrl: string;
  let savedAuth: string | null;
  let savedUser: string | null;

  beforeEach(() => {
    originalUrl = location.href;
    savedAuth = localStorage.getItem('auth');
    savedUser = localStorage.getItem('user');
    localStorage.removeItem('auth');
    localStorage.removeItem('user');
    history.replaceState(history.state, '', location.pathname + '?demo');
  });

  afterEach(() => {
    history.replaceState(history.state, '', originalUrl);
    for (const [key, value] of [
      ['auth', savedAuth],
      ['user', savedUser],
    ] as const) {
      if (value === null) localStorage.removeItem(key);
      else localStorage.setItem(key, value);
    }
  });

  it('registers neither demo login nor the mock backend', () => {
    expect(homeworkProviders).toEqual([]);
    expect(homeworkInterceptors).toEqual([]);
  });

  it('does not authorize a protected route from ?demo', () => {
    TestBed.configureTestingModule({
      providers: [
        ...homeworkProviders,
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    const route = new ActivatedRouteSnapshot();
    route.data = { authorized: true };
    TestBed.inject(AuthGuard)
      .canActivate(route)
      .subscribe((result) => {
        expect(result instanceof UrlTree).toBeTrue();
        expect(result.toString()).toBe('/login');
      });
    expect(localStorage.getItem('auth')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();
  });
});
