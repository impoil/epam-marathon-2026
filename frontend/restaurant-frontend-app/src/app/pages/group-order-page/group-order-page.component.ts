import { A11yModule } from '@angular/cdk/a11y';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { finalize, forkJoin, interval, map, Subscription, switchMap } from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

@Component({
  selector: 'app-group-order-page',
  imports: [A11yModule, CurrencyPipe, DatePipe, ModalComponent, ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  private syncTimer?: Subscription;

  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly groupStatus = signal<GroupStatusResponse | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal('');
  readonly refreshing = signal(false);
  readonly refreshFailed = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly submitting = signal(false);
  readonly removing = signal(false);
  readonly submitDialog = signal(false);
  readonly removeTarget = signal<Participant | null>(null);
  readonly mutationError = signal('');

  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  readonly unsubmittedCount = computed(
    () => this.participants().filter((person) => !person.isSubmitted).length
  );
  readonly hasDishes = computed(() =>
    this.participants().some((person) => person.items.length > 0)
  );
  readonly isOpen = computed(() => this.groupStatus()?.status === 'Open');
  readonly statusLabel = computed(() => {
    switch (this.groupStatus()?.status) {
      case 'Open':
        return 'Collecting pre-orders';
      case 'Submitted':
        return 'Sent to Restaurant';
      default:
        return 'Closed';
    }
  });
  readonly busy = computed(
    () => this.loading() || this.refreshing() || this.submitting() || this.removing()
  );
  readonly canSubmit = computed(
    () => this.isOpen() && !this.busy() && this.consolidated().items.length > 0
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.busy()) return;
    this.stopSync();
    this.loading.set(true);
    this.loadError.set('');
    this.service
      .getGroupInvite$(this.reservationId)
      .pipe(
        switchMap((invite) =>
          forkJoin({
            sessions: this.service.getSessions$(invite.GroupReservationId),
            ownerSessionId: this.service.getOwnerSessionId$(this.reservationId),
            status: this.service.getStatus$(invite.GroupReservationId),
          }).pipe(map((snapshot) => ({ invite, ...snapshot })))
        ),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: ({ invite, sessions, ownerSessionId, status }) => {
          this.invite.set(invite);
          this.participants.set(sessions.participants);
          this.ownerSessionId.set(ownerSessionId);
          this.applyStatus(status);
          this.markUpdated();
          if (this.isOpen()) this.startSync();
        },
        error: (error: unknown) => {
          this.loadError.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'Group reservation not found'
              : 'Could not load the group pre-order. Please try again.'
          );
        },
      });
  }

  refresh(): void {
    const groupId = this.invite()?.GroupReservationId;
    // One shared gate for manual/timed refreshes and mutations prevents stale responses
    // from overwriting a successful DELETE or Submit.
    if (!groupId || !this.isOpen() || this.busy() || this.submitDialog() || this.removeTarget())
      return;
    this.refreshing.set(true);
    forkJoin({
      sessions: this.service.getSessions$(groupId, { silent: true }),
      status: this.service.getStatus$(groupId, { silent: true }),
    })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.refreshing.set(false))
      )
      .subscribe({
        next: ({ sessions, status }) => {
          // Apply a complete snapshot; a partial refresh never discards the last good data.
          this.participants.set(sessions.participants);
          this.applyStatus(status);
          this.markUpdated();
        },
        error: () => this.refreshFailed.set(true),
      });
  }

  openSubmit(): void {
    if (!this.canSubmit()) return;
    this.mutationError.set('');
    this.submitDialog.set(true);
  }

  closeSubmit(): void {
    if (!this.submitting()) this.submitDialog.set(false);
  }

  submit(): void {
    const groupId = this.invite()?.GroupReservationId;
    if (!groupId || !this.submitDialog() || !this.canSubmit()) return;
    this.submitting.set(true);
    this.mutationError.set('');
    this.service
      .submit$(groupId, this.reservationId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.submitting.set(false))
      )
      .subscribe({
        next: () => {
          this.applyStatus({ ...this.groupStatus()!, status: 'Submitted' });
          this.markUpdated();
          this.toast.success('Pre-order sent to the restaurant');
        },
        // tokenInterceptor displays the API error once; keep the dialog usable for retry.
        error: () => this.mutationError.set('Pre-order was not sent. Please try again.'),
      });
  }

  openRemove(person: Participant): void {
    if (
      !this.isOpen() ||
      this.busy() ||
      person.type !== 'Guest' ||
      person.id === this.ownerSessionId()
    )
      return;
    const current = this.participants().find((participant) => participant.id === person.id);
    if (!current || current.type !== 'Guest') return;
    this.mutationError.set('');
    this.removeTarget.set(current);
  }

  closeRemove(): void {
    if (!this.removing()) this.removeTarget.set(null);
  }

  removeGuest(): void {
    const person = this.removeTarget();
    const groupId = this.invite()?.GroupReservationId;
    if (
      !person ||
      !groupId ||
      !this.isOpen() ||
      this.busy() ||
      person.type !== 'Guest' ||
      person.id === this.ownerSessionId()
    )
      return;
    this.removing.set(true);
    this.mutationError.set('');
    this.service
      .removeSession$(groupId, person.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.removing.set(false))
      )
      .subscribe({
        next: (sessions) => {
          this.participants.set(sessions.participants);
          this.groupStatus.update(
            (status) => status && { ...status, currentParticipants: sessions.participants.length }
          );
          this.removeTarget.set(null);
          this.markUpdated();
        },
        error: () => this.mutationError.set('Guest was not removed. Please try again.'),
      });
  }

  private applyStatus(status: GroupStatusResponse): void {
    this.groupStatus.set(status);
    if (status.status !== 'Open') {
      this.stopSync();
      this.submitDialog.set(false);
      this.removeTarget.set(null);
    }
  }

  private markUpdated(): void {
    this.updatedAt.set(new Date());
    this.refreshFailed.set(false);
  }

  private startSync(): void {
    this.stopSync();
    this.syncTimer = interval(15_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refresh());
  }

  private stopSync(): void {
    this.syncTimer?.unsubscribe();
    this.syncTimer = undefined;
  }
}
