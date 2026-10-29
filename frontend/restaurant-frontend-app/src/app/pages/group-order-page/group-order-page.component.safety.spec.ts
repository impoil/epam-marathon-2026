import { HttpErrorResponse } from '@angular/common/http';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupOrderPageComponent } from './group-order-page.component';

const INVITE: GuestInvite = {
  GroupReservationId: 'group',
  LocationId: 'location',
  RestaurantName: 'Restaurant',
  LocationAddress: 'Address',
  StartTime: '2026-10-21T16:00:00Z',
  EndTime: '2026-10-21T18:00:00Z',
  MaxGuestCount: 5,
  TableNumber: 1,
  InviterName: 'Owner',
};
const SESSIONS: GroupSessionsResponse = {
  participants: [
    {
      id: 'owner',
      type: 'Owner',
      name: 'Owner',
      isSubmitted: true,
      total: 10,
      items: [{ dish: { id: 'dish', name: 'Salad', price: 10 }, quantity: 1 }],
    },
    {
      id: 'guest',
      type: 'Guest',
      name: 'Guest',
      isSubmitted: true,
      total: 20,
      items: [{ dish: { id: 'dish', name: 'Salad', price: 10 }, quantity: 2 }],
    },
  ],
  total: 30,
};
const OPEN: GroupStatusResponse = { status: 'Open', currentParticipants: 2, maxCapacity: 5 };

describe('Owner Review · concurrent requests and recovery', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$',
      'getOwnerSessionId$',
      'getSessions$',
      'getStatus$',
      'submit$',
      'removeSession$',
    ]);
    service.getGroupInvite$.and.returnValue(of(INVITE));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getSessions$.and.returnValue(of(structuredClone(SESSIONS)));
    service.getStatus$.and.returnValue(of(OPEN));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: toast },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'reservation' }) } },
        },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return {
      fixture,
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
    };
  }

  it('shows loading and ignores repeated load while the first request is pending', () => {
    const pending = new Subject<GuestInvite>();
    service.getGroupInvite$.and.returnValue(pending);
    const { fixture, page, element } = create();
    expect(element.textContent).toContain('Loading group pre-order');
    page.load();
    expect(service.getGroupInvite$).toHaveBeenCalledTimes(1);
    pending.next(INVITE);
    pending.complete();
    fixture.detectChanges();
    expect(element.textContent).toContain('My Pre-Order');
    expect(page.loading()).toBeFalse();
  });

  it('skips timed and manual refreshes during an outstanding request, then resumes', fakeAsync(() => {
    const { fixture, page } = create();
    const pending = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(pending);
    page.refresh();
    page.refresh();
    tick(45_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    expect(service.getStatus$).toHaveBeenCalledTimes(2);
    expect(page.participants()).toEqual(SESSIONS.participants);
    pending.next({ participants: [], total: 0 });
    pending.complete();
    service.getSessions$.and.returnValue(of(SESSIONS));
    tick(15_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(3);
    fixture.destroy();
  }));

  it('retry starts exactly one timer and subsequent reload replaces that timer', fakeAsync(() => {
    service.getGroupInvite$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 500 }))
    );
    const { fixture, page } = create();
    tick(30_000);
    expect(service.getSessions$).not.toHaveBeenCalled();
    service.getGroupInvite$.and.returnValue(of(INVITE));
    page.load();
    tick(15_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    page.load();
    tick(30_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(5);
    fixture.destroy();
  }));

  it('retains the entire last snapshot if status fails, then clears the refresh error on recovery', fakeAsync(() => {
    const { fixture, page } = create();
    const updatedAt = page.updatedAt();
    service.getSessions$.and.returnValue(of({ participants: [], total: 0 }));
    service.getStatus$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    tick(15_000);
    expect(page.participants()).toEqual(SESSIONS.participants);
    expect(page.groupStatus()).toEqual(OPEN);
    expect(page.updatedAt()).toBe(updatedAt);
    expect(page.refreshFailed()).toBeTrue();
    expect(toast.error).not.toHaveBeenCalled();
    service.getStatus$.and.returnValue(of(OPEN));
    tick(15_000);
    expect(page.refreshFailed()).toBeFalse();
    expect(page.participants()).toEqual([]);
    expect(page.updatedAt()!.getTime()).toBeGreaterThan(updatedAt!.getTime());
    fixture.destroy();
  }));

  for (const status of ['Submitted', 'Closed'] as const) {
    it(`stops polling and hides mutation controls when the remote group becomes ${status}`, fakeAsync(() => {
      const { fixture, page, element } = create();
      service.getStatus$.and.returnValue(of({ ...OPEN, status }));
      tick(15_000);
      fixture.detectChanges();
      expect(page.isOpen()).toBeFalse();
      expect(element.textContent).not.toContain('Remove Guest');
      expect(element.querySelector('.submit-order')).toBeNull();
      page.refresh();
      tick(45_000);
      expect(service.getSessions$).toHaveBeenCalledTimes(2);
      fixture.destroy();
    }));
  }

  it('prevents a mutation during refresh and prevents removing Owner even via a direct call', () => {
    const { page } = create();
    page.openRemove(SESSIONS.participants[0]);
    page.removeGuest();
    expect(service.removeSession$).not.toHaveBeenCalled();
    const pending = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(pending);
    page.refresh();
    page.openSubmit();
    page.openRemove(SESSIONS.participants[1]);
    expect(page.submitDialog()).toBeFalse();
    expect(page.removeTarget()).toBeNull();
  });

  it('sends DELETE once, keeps the dialog pending and updates the consolidated total from its response', fakeAsync(() => {
    const { fixture, page } = create();
    const pending = new Subject<GroupSessionsResponse>();
    service.removeSession$.and.returnValue(pending);
    page.openRemove(SESSIONS.participants[1]);
    page.removeGuest();
    page.removeGuest();
    page.closeRemove();
    tick(30_000);
    expect(service.removeSession$).toHaveBeenCalledOnceWith('group', 'guest');
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    expect(page.removeTarget()?.id).toBe('guest');
    pending.next({ participants: [SESSIONS.participants[0]], total: 10 });
    pending.complete();
    expect(page.participants().length).toBe(1);
    expect(page.consolidated().total).toBe(10);
    expect(page.groupStatus()?.currentParticipants).toBe(1);
    tick(15_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    fixture.destroy();
  }));

  it('allows retry after failed DELETE without changing participants or showing a duplicate toast', () => {
    const { page } = create();
    service.removeSession$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400 }))
    );
    page.openRemove(SESSIONS.participants[1]);
    page.removeGuest();
    expect(page.participants()).toEqual(SESSIONS.participants);
    expect(page.mutationError()).toContain('not removed');
    expect(toast.error).not.toHaveBeenCalled();
    service.removeSession$.and.returnValue(
      of({ participants: [SESSIONS.participants[0]], total: 10 })
    );
    page.removeGuest();
    expect(page.removeTarget()).toBeNull();
    expect(page.consolidated().total).toBe(10);
  });

  it('blocks closing or resubmitting an in-flight Submit and resumes safely after its error', fakeAsync(() => {
    const { fixture, page } = create();
    const pending = new Subject<{ dishId: string; quantity: number }[]>();
    service.submit$.and.returnValue(pending);
    page.openSubmit();
    page.submit();
    page.closeSubmit();
    page.submit();
    tick(30_000);
    expect(page.submitDialog()).toBeTrue();
    expect(service.submit$).toHaveBeenCalledOnceWith('group', 'reservation');
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    pending.error(new HttpErrorResponse({ status: 400 }));
    expect(page.submitting()).toBeFalse();
    expect(page.isOpen()).toBeTrue();
    expect(toast.error).not.toHaveBeenCalled();
    service.submit$.and.returnValue(of([{ dishId: 'dish', quantity: 3 }]));
    page.submit();
    expect(page.submitDialog()).toBeFalse();
    expect(toast.success).toHaveBeenCalledOnceWith('Pre-order sent to the restaurant');
    tick(45_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    fixture.destroy();
  }));

  it('unsubscribes an in-flight refresh on component destruction', fakeAsync(() => {
    const { fixture, page } = create();
    const pending = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(pending);
    page.refresh();
    fixture.destroy();
    pending.next({ participants: [], total: 0 });
    pending.complete();
    tick(45_000);
    expect(page.participants()).toEqual(SESSIONS.participants);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
  }));
});
