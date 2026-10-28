using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GroupReservations;
using APIHandler.Models.GroupReservation;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitGroupReservationHandler(IGroupReservationService service) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (userId is null)
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        if (!request.TryGetPathValue("groupReservationId", out var id) || string.IsNullOrWhiteSpace(id))
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger);
        try
        {
            if (string.IsNullOrWhiteSpace(request.Body))
                return ResponseBuilder.CreateErrorResponse(400, "Request body is required.", context.Logger);
            var body = JsonSerializer.Deserialize<SubmitGroupReservationRequest>(request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (string.IsNullOrWhiteSpace(body?.ReservationId))
                return ResponseBuilder.CreateErrorResponse(400, "reservationId is required.", context.Logger);

            var result = await service.SubmitPreOrderAsync(userId, id, body.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, result);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (GuestSessionNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorCodeResponse(ex);
        }
        catch (JsonException)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Invalid JSON request body.", context.Logger);
        }
    }
}
