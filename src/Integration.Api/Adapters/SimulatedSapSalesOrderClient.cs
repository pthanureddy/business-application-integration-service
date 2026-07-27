using Integration.Api.Contracts;

namespace Integration.Api.Adapters;

public sealed class SimulatedSapSalesOrderClient(TimeProvider timeProvider) : ISapSalesOrderClient
{
    public Task<IReadOnlyList<SapSalesOrderResponse>> FetchUpdatedAsync(
        DateTimeOffset updatedSinceUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var updatedAt = timeProvider.GetUtcNow();
        IReadOnlyList<SapSalesOrderResponse> orders =
        [
            new SapSalesOrderResponse(
                DocEntry: 4102,
                DocNum: 202604102,
                CardCode: "C-SYNTHETIC-01",
                DocTotal: 18450.75m,
                Currency: "EUR",
                UpdatedAtUtc: updatedAt)
        ];
        return Task.FromResult(orders);
    }
}

