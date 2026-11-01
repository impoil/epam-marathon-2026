import { buildPreOrderQueryParams } from './pre-order-query-params';

describe('buildPreOrderQueryParams', () => {
  const reservation = {
    id: 'reservation-42', tableId: 'table-6', locationAddress: 'Demo location',
    startTime: new Date(2026, 9, 31, 18), endTime: new Date(2026, 9, 31, 19, 30),
  };

  it('keeps the card navigation format, including the table id fallback', () => {
    expect(buildPreOrderQueryParams(reservation)).toEqual({
      preOrder: 'true', reservationId: 'reservation-42',
      reservationLabel: [
        'Demo location', 'Table table-6', reservation.startTime.toDateString(),
        `${reservation.startTime.toLocaleTimeString()} - ${reservation.endTime.toLocaleTimeString()}`,
      ].join(', '),
    });
  });

  it('preserves a named table label used by reservation cards', () => {
    expect(buildPreOrderQueryParams({ ...reservation, tableName: 'Window table' })['reservationLabel'])
      .toContain('Demo location, Window table, ');
  });

  it('keeps distinct reservations separate without mutating the input', () => {
    const next = { ...reservation, id: 'another-reservation' };
    expect(buildPreOrderQueryParams(next)['reservationId']).toBe('another-reservation');
    expect(reservation.id).toBe('reservation-42');
  });
});
