using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GroupReservations;

public class GroupReservationRepository : IGroupReservationRepository
{
    private static readonly string GroupReservationsTableName = ResourceNames.GroupReservationsTableName;
    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonDynamoDB _client;

    public GroupReservationRepository(IDynamoDBContext dynamoDbContext, IAmazonDynamoDB client)
    {
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task SubmitPreOrderAsync(GroupReservation groupReservation, Order order)
    {
        var groupConfig = new ToDocumentConfig { OverrideTableName = GroupReservationsTableName };
        var orderConfig = new ToDocumentConfig { OverrideTableName = ResourceNames.OrdersTableName };
        var groupItem = _dynamoDbContext.ToDocument(groupReservation, groupConfig).ToAttributeMap();
        var orderItem = _dynamoDbContext.ToDocument(order, orderConfig).ToAttributeMap();
        var openGroup = new GroupReservation
        {
            GroupReservationId = groupReservation.GroupReservationId,
            ReservationId = groupReservation.ReservationId,
            ReservationToken = groupReservation.ReservationToken,
            Status = GroupReservationStatus.Open
        };
        var openStatus = _dynamoDbContext.ToDocument(openGroup, groupConfig).ToAttributeMap()["status"];
        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>
                {
                    new() { Put = new Put
                    {
                        TableName = GroupReservationsTableName, Item = groupItem,
                        ConditionExpression = "attribute_exists(groupReservationId) AND #status = :open AND reservationId = :reservation",
                        ExpressionAttributeNames = new() { ["#status"] = "status" },
                        ExpressionAttributeValues = new()
                        {
                            [":open"] = openStatus, [":reservation"] = groupItem["reservationId"]
                        }
                    } },
                    new() { Put = new Put
                    {
                        TableName = ResourceNames.OrdersTableName, Item = orderItem,
                        ConditionExpression = "attribute_exists(orderId) AND #status = :created AND reservationId = :reservation",
                        ExpressionAttributeNames = new() { ["#status"] = "status" },
                        ExpressionAttributeValues = new()
                        {
                            [":created"] = new AttributeValue { N = ((int)OrderStatus.Created).ToString() },
                            [":reservation"] = orderItem["reservationId"]
                        }
                    } }
                }
            });
        }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons?.Any(r => r.Code == "ConditionalCheckFailed") == true)
        {
            throw new APIHandler.Exceptions.ValidationException("The group or order has changed; refresh before submitting again.");
        }
    }

    public async Task<GroupReservation?> GetGroupReservationByIdAsync(string groupReservationId)
    {
        return await _dynamoDbContext.LoadAsync<GroupReservation>(groupReservationId, DynamoConfig.LoadConfig(GroupReservationsTableName));
    }

    public async Task DeleteGroupReservationAsync(GroupReservation groupReservation)
    {
        await _dynamoDbContext.DeleteAsync(groupReservation, DynamoConfig.DeleteConfig(GroupReservationsTableName));
    }

    public async Task SaveGroupReservationAsync(GroupReservation groupReservation)
    {
        await _dynamoDbContext.SaveAsync(groupReservation, DynamoConfig.SaveConfig(GroupReservationsTableName));
    }

    public async Task<GroupReservation?> GetGroupReservationByTokenAsync(string token)
    {
        var results = await _dynamoDbContext.QueryAsync<GroupReservation?>(token, new QueryConfig
        {
            OverrideTableName = GroupReservationsTableName,
            IndexName = GlobalSecondaryIndexes.GroupReservation.ReservationToken,
        }).GetRemainingAsync();

        return results.FirstOrDefault();
    }

    public async Task<GroupReservation?> GetByReservationIdAsync(string reservationId)
    {
        var results = await _dynamoDbContext.QueryAsync<GroupReservation?>(reservationId, new QueryConfig
        {
            OverrideTableName = GroupReservationsTableName,
            IndexName = GlobalSecondaryIndexes.GroupReservation.ReservationId,
        }).GetRemainingAsync();

        return results.FirstOrDefault();
    }
}
