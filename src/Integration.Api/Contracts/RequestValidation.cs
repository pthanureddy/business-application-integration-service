namespace Integration.Api.Contracts;

public static class RequestValidation
{
    public static Dictionary<string, string[]> Validate(ShipmentIntegrationRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        AddRequired(errors, nameof(request.SourceSystem), request.SourceSystem, 100);
        AddRequired(errors, nameof(request.ShipmentId), request.ShipmentId, 100);
        AddRequired(errors, nameof(request.OrderReference), request.OrderReference, 100);
        AddRequired(errors, nameof(request.CarrierCode), request.CarrierCode, 40);
        AddRequired(errors, nameof(request.Status), request.Status, 40);

        if (request.OccurredAtUtc == default)
        {
            errors[nameof(request.OccurredAtUtc)] = ["A valid event timestamp is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DestinationCountry)
            || request.DestinationCountry.Trim().Length != 2)
        {
            errors[nameof(request.DestinationCountry)] = ["Use a two-letter ISO country code."];
        }

        if (request.TrackingNumber is { Length: > 120 })
        {
            errors[nameof(request.TrackingNumber)] = ["Tracking number must not exceed 120 characters."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(ElectronicInvoiceRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        AddRequired(errors, nameof(request.SourceSystem), request.SourceSystem, 100);
        AddRequired(errors, nameof(request.InvoiceNumber), request.InvoiceNumber, 100);
        AddRequired(errors, nameof(request.CustomerCode), request.CustomerCode, 100);

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            errors[nameof(request.Currency)] = ["Use a three-letter ISO currency code."];
        }

        if (request.IssuedOn == default)
        {
            errors[nameof(request.IssuedOn)] = ["An invoice date is required."];
        }

        if (request.TotalAmount < 0)
        {
            errors[nameof(request.TotalAmount)] = ["Total amount must be zero or greater."];
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            errors[nameof(request.Lines)] = ["At least one invoice line is required."];
            return errors;
        }

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            if (string.IsNullOrWhiteSpace(line.ItemCode))
            {
                errors[$"{nameof(request.Lines)}[{index}].{nameof(line.ItemCode)}"] =
                    ["Item code is required."];
            }

            if (line.Quantity <= 0)
            {
                errors[$"{nameof(request.Lines)}[{index}].{nameof(line.Quantity)}"] =
                    ["Quantity must be greater than zero."];
            }

            if (line.UnitPrice < 0)
            {
                errors[$"{nameof(request.Lines)}[{index}].{nameof(line.UnitPrice)}"] =
                    ["Unit price must be zero or greater."];
            }

            if (line.TaxRate is < 0 or > 100)
            {
                errors[$"{nameof(request.Lines)}[{index}].{nameof(line.TaxRate)}"] =
                    ["Tax rate must be between 0 and 100."];
            }
        }

        var calculatedTotal = request.Lines.Sum(
            line => decimal.Round(
                line.Quantity * line.UnitPrice * (1 + line.TaxRate / 100),
                2,
                MidpointRounding.AwayFromZero));

        if (Math.Abs(calculatedTotal - request.TotalAmount) > 0.01m)
        {
            errors[nameof(request.TotalAmount)] =
                [$"Total amount does not match the calculated line total {calculatedTotal:0.00}."];
        }

        return errors;
    }

    private static void AddRequired(
        IDictionary<string, string[]> errors,
        string field,
        string value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["A value is required."];
        }
        else if (value.Trim().Length > maximumLength)
        {
            errors[field] = [$"Value must not exceed {maximumLength} characters."];
        }
    }
}

