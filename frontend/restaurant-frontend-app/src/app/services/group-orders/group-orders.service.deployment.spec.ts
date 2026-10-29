import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from './group-orders.service';

describe('GroupOrdersService deployment', () => {
  it('loads protected owner metadata without reopening a full or submitted public invite', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: 'https://api.test' }] });
    const service = TestBed.inject(GroupOrdersService);
    const http = TestBed.inject(HttpTestingController);
    const invite: GuestInvite = { GroupReservationId: 'group', LocationId: 'location',
      RestaurantName: 'Green & Tasty', LocationAddress: 'Demo', StartTime: '', EndTime: '',
      MaxGuestCount: 3, TableNumber: 1, InviterName: 'Owner' };
    let result: GuestInvite | undefined;
    service.getGroupInvite$('reservation').subscribe((value) => result = value);
    http.expectOne('https://api.test/reservations/reservation/invite-token')
      .flush({ token: 'private-invite', invite });
    expect(result).toEqual(invite);
    http.expectNone('https://api.test/guest/invites/private-invite');
    http.verify();
  });
});
