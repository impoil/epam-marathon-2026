import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { AuthenticationService } from '../../../auth/services/authentication.service';
import { SKIP_ERROR_TOAST } from '../../../auth/skip-error-toast.context';
import { tokenInterceptor } from '../../../auth/token.interceptor';
import { ToastService } from '../../../services/toast/toast.service';
import { createDemoTokens } from '../auth/demo-auth';
import { groupOrdersMockInterceptor, groupOrdersMockStore } from './group-orders-mock.interceptor';

const API = 'https://test.api/develop';
const DEMO_TOKENS = createDemoTokens();

describe('homework mock backend integration', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    groupOrdersMockStore.reset();
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor, groupOrdersMockInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthenticationService, useValue: { getTokens: () => DEMO_TOKENS } },
        { provide: ToastService, useValue: toast },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('serves the demo invite and sessions without reaching the real API', fakeAsync(() => {
    let invite: unknown;
    let sessions: { participants: unknown[]; total: number } | undefined;
    http
      .get(`${API}/reservations/demo-reservation/invite-token`)
      .subscribe((value) => (invite = value));
    http
      .get<{ participants: unknown[]; total: number }>(`${API}/guest/groups/demo-group/sessions`)
      .subscribe((value) => (sessions = value));
    tick(400);
    expect(invite).toEqual({ token: 'demo-invite-token' });
    expect(sessions?.participants.length).toBe(4);
    expect(sessions?.total).toBe(74.5);
    backend.expectNone(() => true);
  }));

  it('receives demo authorization after tokenInterceptor and returns an empty cart', fakeAsync(() => {
    let cart: unknown;
    http.get(`${API}/orders`).subscribe((value) => (cart = value));
    tick(400);
    expect(cart).toEqual([]);
    backend.expectNone(() => true);
  }));

  it('submits the demo order and exposes the updated status', fakeAsync(() => {
    let order: unknown;
    let status: { status: string } | undefined;
    http
      .post(`${API}/group-reservations/demo-group/submit`, { reservationId: 'demo-reservation' })
      .subscribe((value) => (order = value));
    tick(400);
    http
      .get<{ status: string }>(`${API}/guest/groups/demo-group/status`)
      .subscribe((value) => (status = value));
    tick(400);
    expect(order).toEqual([
      { dishId: 'dish-spring-salad', quantity: 3 },
      { dishId: 'dish-avocado-bowl', quantity: 1 },
    ]);
    expect(status?.status).toBe('Submitted');
  }));

  it('returns mock errors through the existing error toast', fakeAsync(() => {
    let status: number | undefined;
    http
      .post(`${API}/group-reservations/demo-group/submit`, { reservationId: 'wrong-id' })
      .subscribe({ error: (error) => (status = error.status) });
    tick(400);
    expect(status).toBe(400);
    expect(toast.error).toHaveBeenCalledWith(
      'Active order not found for this reservation.',
      'Error'
    );
  }));

  it('removes a guest and supports silent guest errors', fakeAsync(() => {
    let sessions: { participants: unknown[] } | undefined;
    http
      .delete<{ participants: unknown[] }>(`${API}/guest/groups/demo-group/sessions/gs-mariia`)
      .subscribe((value) => (sessions = value));
    tick(400);
    expect(sessions?.participants.length).toBe(3);
    let status: number | undefined;
    http
      .delete(`${API}/guest/groups/demo-group/sessions/unknown`, {
        context: new HttpContext().set(SKIP_ERROR_TOAST, true),
      })
      .subscribe({ error: (error) => (status = error.status) });
    tick(400);
    expect(status).toBe(404);
    expect(toast.error).not.toHaveBeenCalled();
  }));

  it('passes real reservation IDs through to the configured backend', () => {
    http.get(`${API}/reservations/real-reservation/invite-token`).subscribe();
    const request = backend.expectOne(`${API}/reservations/real-reservation/invite-token`);
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer ' + DEMO_TOKENS.idToken);
    request.flush({ token: 'real-invite' });
  });

  it('preserves the guest session token on requests for real groups', () => {
    http
      .get(`${API}/guest/groups/real-group/sessions`, {
        headers: { Authorization: 'Bearer guest-token' },
      })
      .subscribe();
    const request = backend.expectOne(`${API}/guest/groups/real-group/sessions`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer guest-token');
    request.flush({ participants: [], total: 0 });
  });
});
