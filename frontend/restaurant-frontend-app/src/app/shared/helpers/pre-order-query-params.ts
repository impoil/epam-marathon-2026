import { Params } from '@angular/router';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';

type PreOrderReservation = Pick<ReservationCustomer,
  'id' | 'tableId' | 'tableName' | 'locationAddress' | 'startTime' | 'endTime'>;

export function buildPreOrderQueryParams(reservation: PreOrderReservation): Params {
  const startTime = new Date(reservation.startTime);
  const endTime = new Date(reservation.endTime);

  return {
    preOrder: 'true',
    reservationId: reservation.id,
    reservationLabel: [
      reservation.locationAddress,
      reservation.tableName || `Table ${reservation.tableId}`,
      startTime.toDateString(),
      `${startTime.toLocaleTimeString()} - ${endTime.toLocaleTimeString()}`,
    ].join(', '),
  };
}
