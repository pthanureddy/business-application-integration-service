using Integration.Api.Contracts;

namespace Integration.Api.Adapters;

public interface ISapSalesOrderClient
{
    Task<IReadOnlyList<SapSalesOrderResponse>> FetchUpdatedAsync(
        DateTimeOffset updatedSinceUtc,
        CancellationToken cancellationToken);
}

