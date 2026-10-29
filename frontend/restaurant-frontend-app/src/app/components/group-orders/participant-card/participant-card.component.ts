import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  imports: [CurrencyPipe],
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  readonly participant = input.required<Participant>();
  readonly isMe = input(false);
  readonly canRemove = input(false);
  readonly removeClick = output<Participant>();
  readonly title = computed(() => {
    const person = this.participant();
    return this.isMe() ? 'My Pre-Order' : `${person.type}: ${person.name}`;
  });

  remove(): void {
    if (this.canRemove() && this.participant().type === 'Guest' && !this.isMe()) {
      this.removeClick.emit(this.participant());
    }
  }
}
