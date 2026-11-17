namespace Integration.Api.Domain;

public enum AccountsPayableInvoiceStatus
{
    Exception,
    AwaitingApproval,
    ReadyForPayment,
    Rejected
}
