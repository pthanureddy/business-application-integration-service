namespace Integration.Api.Domain;

public sealed class SupplierMasterRecord
{
    private SupplierMasterRecord()
    {
    }

    public SupplierMasterRecord(
        string supplierNumber,
        string legalName,
        string defaultAccount,
        string defaultCostCenter,
        int paymentTermsDays,
        bool isActive = true)
    {
        SupplierNumber = supplierNumber;
        LegalName = legalName;
        DefaultAccount = defaultAccount;
        DefaultCostCenter = defaultCostCenter;
        PaymentTermsDays = paymentTermsDays;
        IsActive = isActive;
    }

    public string SupplierNumber { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string DefaultAccount { get; private set; } = string.Empty;

    public string DefaultCostCenter { get; private set; } = string.Empty;

    public int PaymentTermsDays { get; private set; }

    public bool IsActive { get; private set; }
}
