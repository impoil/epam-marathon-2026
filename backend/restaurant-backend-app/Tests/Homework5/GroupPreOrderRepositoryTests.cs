using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Entities;
using APIHandler.Repositories.GroupReservations;
using Moq;

namespace APIHandler.Tests.Homework5;

public class GroupPreOrderRepositoryTests
{
    private readonly Mock<IAmazonDynamoDB> client = new(MockBehavior.Strict);
    private readonly IDynamoDBContext context;
    private readonly GroupReservationRepository repository;
    private readonly GroupReservation group = new()
    {
        GroupReservationId = "g", ReservationId = "r", ReservationToken = "token", Status = GroupReservationStatus.Submitted
    };
    private readonly Order order = new()
    {
        OrderId = "o", CustomerId = "owner", ReservationId = "r", ReservationAddress = "address",
        ReservationTime = "time", Status = OrderStatus.SubmittedByCustomer,
        Dishes = [new() { DishId = "d", Quantity = 3 }]
    };

    public GroupPreOrderRepositoryTests()
    {
        TestEnvironment.Initialize();
        client.SetupGet(c => c.Config).Returns(new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.EUNorth1 });
        context = new DynamoDBContextBuilder().WithDynamoDBClient(() => client.Object)
            .ConfigureContext(c => c.DisableFetchingTableMetadata = true).Build();
        repository = new(context, client.Object);
    }

    [Fact]
    public async Task Submit_WritesBothEntitiesInOneConditionalTransactionUsingExistingDynamoMapping()
    {
        TransactWriteItemsRequest? request = null;
        client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), default))
            .Callback<TransactWriteItemsRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new TransactWriteItemsResponse());
        await repository.SubmitPreOrderAsync(group, order);
        Assert.Equal(2, request!.TransactItems.Count);
        var groupPut = request.TransactItems[0].Put;
        var orderPut = request.TransactItems[1].Put;
        Assert.Equal("hw5-GroupReservations", groupPut.TableName);
        Assert.Equal("hw5-Orders", orderPut.TableName);
        Assert.Equal("g", groupPut.Item["groupReservationId"].S);
        Assert.Equal("r", groupPut.Item["reservationId"].S);
        Assert.Equal("owner", orderPut.Item["customerId"].S);
        Assert.Equal("3", orderPut.Item["dishes"].L[0].M["quantity"].N);
        Assert.Equal(((int)OrderStatus.SubmittedByCustomer).ToString(), orderPut.Item["status"].N);
        Assert.Contains("#status = :open", groupPut.ConditionExpression);
        Assert.Contains("#status = :created", orderPut.ConditionExpression);
        Assert.Equal("0", orderPut.ExpressionAttributeValues[":created"].N);
        client.Verify(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), default), Times.Once);
        client.Verify(c => c.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), default), Times.Never);
    }

    [Theory]
    [InlineData("ConditionalCheckFailed", true)]
    [InlineData("ProvisionedThroughputExceeded", false)]
    public async Task Submit_OnlyConditionalFailuresBecomeBadRequests(string reason, bool validation)
    {
        client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), default))
            .ThrowsAsync(new TransactionCanceledException("transaction cancelled")
            {
                CancellationReasons = [new CancellationReason { Code = reason }]
            });
        if (validation)
            await Assert.ThrowsAsync<APIHandler.Exceptions.ValidationException>(() => repository.SubmitPreOrderAsync(group, order));
        else
            await Assert.ThrowsAsync<TransactionCanceledException>(() => repository.SubmitPreOrderAsync(group, order));
    }
}
