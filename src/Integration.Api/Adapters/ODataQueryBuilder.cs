namespace Integration.Api.Adapters;

public static class ODataQueryBuilder
{
    public static string BuildUpdatedSalesOrdersPath(DateTimeOffset updatedSinceUtc)
    {
        var select = "DocEntry,DocNum,CardCode,DocTotal,DocCurrency,UpdateDate";
        var filter = $"UpdateDate ge '{updatedSinceUtc.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}'";
        var orderBy = "UpdateDate asc";
        return "/b1s/v2/Orders"
            + "?$select=" + Uri.EscapeDataString(select)
            + "&$filter=" + Uri.EscapeDataString(filter)
            + "&$orderby=" + Uri.EscapeDataString(orderBy);
    }
}

