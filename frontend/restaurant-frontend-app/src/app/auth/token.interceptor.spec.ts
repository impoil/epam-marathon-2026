import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    const authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('keeps guest authorization and payload unchanged', () => {
    const body = { guestName: 'Guest' };
    http.post('https://test.api/guest/sessions', body, {
      headers: { Authorization: 'Bearer guest-session-token' },
    }).subscribe();
    const request = httpMock.expectOne('https://test.api/guest/sessions');
    expect(request.request.headers.get('Authorization')).toBe('Bearer guest-session-token');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });

  for (const [method, path] of [
    ['GET', '/guest/groups/group-1/sessions'],
    ['GET', '/guest/groups/group-1/status'],
    ['DELETE', '/guest/groups/group-1/sessions/guest-1'],
    ['GET', '/reservations/res-1/guest-session-id'],
    ['POST', '/group-reservations/group-1/submit'],
  ]) {
    it(`attaches the Cognito ID token to ${method} ${path}`, () => {
      const authentication = TestBed.inject(AuthenticationService) as jasmine.SpyObj<AuthenticationService>;
      authentication.getTokens.and.returnValue({ idToken: 'owner-id-token', accessToken: 'owner-access-token' });
      const body = method === 'POST' ? { reservationId: 'res-1' } : null;
      http.request(method, `https://test.api${path}`, { body }).subscribe();
      const request = httpMock.expectOne(`https://test.api${path}`);
      expect(request.request.headers.get('Authorization')).toBe('Bearer owner-id-token');
      expect(request.request.body).toEqual(body);
      request.flush({});
    });
  }

  it('leaves anonymous invites and guest-session requests unchanged even when Owner is logged in', () => {
    const authentication = TestBed.inject(AuthenticationService) as jasmine.SpyObj<AuthenticationService>;
    authentication.getTokens.and.returnValue({ idToken: 'owner-id-token', accessToken: 'owner-access-token' });
    http.get('https://test.api/guest/invites/token').subscribe();
    http.post('https://test.api/guest/sessions/guest-1/submit', { guest: true }, {
      headers: { Authorization: 'Bearer guest-session-token' },
    }).subscribe();
    const invite = httpMock.expectOne('https://test.api/guest/invites/token');
    expect(invite.request.headers.has('Authorization')).toBeFalse();
    invite.flush({});
    const session = httpMock.expectOne('https://test.api/guest/sessions/guest-1/submit');
    expect(session.request.headers.get('Authorization')).toBe('Bearer guest-session-token');
    expect(session.request.body).toEqual({ guest: true });
    session.flush({});
  });

  it('supports silent errors on guest endpoints used for homework polling', () => {
    let status: number | undefined;
    http.get('https://test.api/guest/groups/real-group/status', {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    }).subscribe({ error: (error) => (status = error.status) });
    httpMock.expectOne('https://test.api/guest/groups/real-group/status')
      .flush({ error: 'Unavailable' }, { status: 503, statusText: 'Unavailable' });
    expect(status).toBe(503);
    expect(toastService.error).not.toHaveBeenCalled();
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });
});
