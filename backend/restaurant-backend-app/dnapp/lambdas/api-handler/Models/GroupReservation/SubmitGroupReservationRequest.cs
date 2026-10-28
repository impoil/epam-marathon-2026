namespace APIHandler.Models.GroupReservation;

public record SubmitGroupReservationRequest(string ReservationId);
public record SubmittedDishResponse(string DishId, int Quantity);
