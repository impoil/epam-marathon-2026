// Composition root for the deployed homework scenario. Handlers/services below are
// compiled from the real backend sources, with no report/upload/admin handlers.
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using APIHandler.Constants;
using APIHandler.Handlers;
using APIHandler.Handlers.Dishes;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.GuestReservations;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Handlers.Locations;
using APIHandler.Handlers.Order;
using APIHandler.Handlers.Reservations;
using APIHandler.Handlers.Tables;
using APIHandler.Handlers.TimeSlots;
using APIHandler.Handlers.Users;
using APIHandler.Records;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Locations;
using APIHandler.Repositories.Notifications;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Repositories.Waiters;
using APIHandler.Services.Auth;
using APIHandler.Services.Dishes;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Notifications;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using APIHandler.Services.Tables;
using APIHandler.Services.Waiters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace APIHandler.Routing;

public static class RouteRegistry
{
    private static readonly Lazy<List<RouteHandler>> CachedRoutes = new(() => BuildRoutes(CreateServiceProvider()));
    public static List<RouteHandler> GetAllRoutes() => CachedRoutes.Value;

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        var region = RegionEndpoint.GetBySystemName(ResourceNames.Region);
        var dynamo = new AmazonDynamoDBClient(region);
        services.AddSingleton(dynamo);
        services.AddSingleton<IAmazonDynamoDB>(dynamo);
        services.AddSingleton<IDynamoDBContext>(provider => new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => provider.GetRequiredService<IAmazonDynamoDB>()).Build());
        var cognito = new AmazonCognitoIdentityProviderClient(region);
        services.AddSingleton(cognito);
        services.AddSingleton<IAmazonCognitoIdentityProvider>(cognito);
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IDishRepository, DishRepository>();
        services.AddSingleton<ILocationRepository, LocationRepository>();
        services.AddSingleton<IWaiterRepository, WaiterRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<IReservationRepository, ReservationRepository>();
        services.AddSingleton<IGuestSessionRepository, GuestSessionRepository>();
        services.AddSingleton<IGroupReservationRepository, GroupReservationRepository>();
        services.AddSingleton<ITableRepository, TableRepository>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IDishService, DishService>();
        services.AddSingleton<IWaiterService, WaiterService>();
        services.AddSingleton<ILocationService, LocationService>();
        services.AddSingleton<ITableService, TableService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IGroupReservationService, GroupReservationService>();
        services.AddSingleton<IReservationService, ReservationService>();
        services.AddSingleton<IGuestSessionService, GuestSessionService>();
        services.AddSingleton<IGuestAuthorizationService, GuestAuthorizationService>();
        return services.BuildServiceProvider();
    }

    public static List<RouteHandler> BuildRoutes(IServiceProvider provider) =>
        [
            new RouteHandler(new Route(HttpMethods.POST, "/signin"), ActivatorUtilities.CreateInstance<SignInHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/me"), ActivatorUtilities.CreateInstance<GetAuthorizedUserHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations"), ActivatorUtilities.CreateInstance<GetLocationsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations/{id}/dishes"), ActivatorUtilities.CreateInstance<GetDishesByLocationIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations/{id}"), ActivatorUtilities.CreateInstance<GetLocationByIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/available-tables"), ActivatorUtilities.CreateInstance<GetAvailableTablesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/tables/{id}/available-time-slots"), ActivatorUtilities.CreateInstance<GetAvailableTimeSlotsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/dishes"), ActivatorUtilities.CreateInstance<GetAllDishesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/dishes/popular"), ActivatorUtilities.CreateInstance<GetPopularDishesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/dishes/{id}"), ActivatorUtilities.CreateInstance<GetDishByIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/reservations"), ActivatorUtilities.CreateInstance<CreateReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations"), ActivatorUtilities.CreateInstance<GetReservationsForUserHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}"), ActivatorUtilities.CreateInstance<GetReservationByIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/orders"), ActivatorUtilities.CreateInstance<GetOrdersByCustomerHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/orders/{reservationId}"), ActivatorUtilities.CreateInstance<GetOrderByReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/orders"), ActivatorUtilities.CreateInstance<CreateOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/orders/{reservationId}"), ActivatorUtilities.CreateInstance<EditPreOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/orders/{reservationId}"), ActivatorUtilities.CreateInstance<CancelPreOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/orders/{reservationId}/submit"), ActivatorUtilities.CreateInstance<SubmitOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/orders/{reservationId}/unsubmit"), ActivatorUtilities.CreateInstance<UnsubmitOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/groups/{groupReservationId}/sessions"), ActivatorUtilities.CreateInstance<GetAllGuestSessionsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}/guest-session-id"), ActivatorUtilities.CreateInstance<GetOwnerSessionIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/group-reservations/{groupReservationId}/submit"), ActivatorUtilities.CreateInstance<SubmitGroupReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/guest/sessions"), ActivatorUtilities.CreateInstance<CreateGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/sessions/{guestSessionId}"), ActivatorUtilities.CreateInstance<GetGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/guest/sessions/{guestSessionId}/dishes"), ActivatorUtilities.CreateInstance<AddDishToGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/guest/sessions/{guestSessionId}/dishes"), ActivatorUtilities.CreateInstance<RemoveDishFromGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/guest/sessions/{guestSessionId}/submit"), ActivatorUtilities.CreateInstance<SubmitGuestCartHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/sessions/{guestSessionId}/menu"), ActivatorUtilities.CreateInstance<GetGuestSessionMenuHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/groups/{groupReservationId}/status"), ActivatorUtilities.CreateInstance<GetGroupReservationStatusHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/invites/{token}"), ActivatorUtilities.CreateInstance<GetGroupReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}/invite-token"), ActivatorUtilities.CreateInstance<GetInviteTokenHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/guest/groups/{groupReservationId}/sessions/{guestSessionId}"), ActivatorUtilities.CreateInstance<DeleteGuestSessionHandler>(provider)),

        ];
}
