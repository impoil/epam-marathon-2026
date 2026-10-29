import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, switchMap } from 'rxjs';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import {
  SubmittedDish,
  SubmitGroupPreOrderRequest,
} from '@app/models/group-orders/submit-group-pre-order.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInviteService } from '../reservation/guest-invite.service';

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly invites = inject(GuestInviteService);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.invites
      .getInviteToken$(reservationId)
      .pipe(switchMap(({ token }) => this.invites.getInvite$(token)));
  }

  getSessions$(
    groupId: string,
    options: { silent?: boolean } = {}
  ): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(`${this.groupUrl(groupId)}/sessions`, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, options.silent === true),
    });
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(
        `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`
      )
      .pipe(map(({ guestSessionId }) => guestSessionId));
  }

  getStatus$(groupId: string, options: { silent?: boolean } = {}): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(`${this.groupUrl(groupId)}/status`, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, options.silent === true),
    });
  }

  submit$(groupId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupId)}/submit`,
      body
    );
  }

  removeSession$(groupId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.groupUrl(groupId)}/sessions/${encodeURIComponent(guestSessionId)}`
    );
  }

  private groupUrl(groupId: string): string {
    return `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}`;
  }
}
