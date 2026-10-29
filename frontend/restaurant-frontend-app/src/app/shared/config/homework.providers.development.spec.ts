import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { AuthenticationService } from '../../auth/services/authentication.service';
import { API_BASE_URL } from './api-base-url.token';
import { homeworkInterceptors, homeworkProviders } from './homework.providers.development';

describe('development homework providers', () => {
  it('initializes the demo session before authentication and handles demo HTTP locally', fakeAsync(() => {
    const originalUrl = location.href;
    const savedAuth = localStorage.getItem('auth');
    const savedUser = localStorage.getItem('user');
    try {
      localStorage.removeItem('auth');
      localStorage.removeItem('user');
      history.replaceState(history.state, '', location.pathname + '?demo');
      TestBed.configureTestingModule({
        providers: [
          ...homeworkProviders,
          provideHttpClient(withInterceptors(homeworkInterceptors)),
          provideHttpClientTesting(),
          { provide: API_BASE_URL, useValue: 'https://test.api' },
        ],
      });
      const auth = TestBed.inject(AuthenticationService);
      expect(auth.getUser()?.id).toBe('demo-owner');
      expect(auth.getTokens()).not.toBeNull();
      expect(new URL(location.href).searchParams.has('demo')).toBeFalse();
      let status: { status: string } | undefined;
      TestBed.inject(HttpClient)
        .get<{ status: string }>('https://test.api/guest/groups/demo-group/status')
        .subscribe((value) => (status = value));
      tick(400);
      expect(status?.status).toBeDefined();
      TestBed.inject(HttpTestingController).expectNone(() => true);
      TestBed.inject(HttpTestingController).verify();
    } finally {
      history.replaceState(history.state, '', originalUrl);
      if (savedAuth === null) localStorage.removeItem('auth');
      else localStorage.setItem('auth', savedAuth);
      if (savedUser === null) localStorage.removeItem('user');
      else localStorage.setItem('user', savedUser);
    }
  }));
});
