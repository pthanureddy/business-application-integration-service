# Accounts-payable workflow

## Purpose

This module demonstrates how supplier-invoice intake can be made observable and repeatable without hiding business exceptions. It is a synthetic portfolio implementation, not an accounting product and not a Rillion integration.

The workflow covers:

- supplier master lookup;
- duplicate detection by supplier number and invoice number;
- invoice-date, due-date, currency, and amount validation;
- account and cost-centre proposals from supplier defaults;
- exception routing for missing or inconsistent information;
- amount-based approval roles;
- controlled approval and rejection transitions; and
- process analytics for exception and automation-candidate follow-up.

## Process

1. An operator or upstream invoice-capture service submits a supplier invoice.
2. The API validates required fields and rejects structurally invalid requests.
3. The workflow checks the synthetic supplier master.
4. Known active suppliers receive default account and cost-centre proposals.
5. Unknown or inactive suppliers, missing purchase-order references, and coding deviations enter the exception queue.
6. Invoices without exceptions wait for an amount-based approval role.
7. A matching approver can move the invoice to `ReadyForPayment`; a decision can also reject it.
8. Analytics expose counts for exceptions, approvals, payment-ready invoices, and touchless candidates.

## Rules

| Condition | Result |
| --- | --- |
| Supplier is unknown or inactive | `SUPPLIER_NOT_FOUND_OR_INACTIVE` |
| Purchase-order reference is missing | `PURCHASE_ORDER_REFERENCE_MISSING` |
| Requested account differs from supplier default | `ACCOUNT_DEVIATION` |
| Requested cost centre differs from supplier default | `COST_CENTER_DEVIATION` |
| Amount below SEK 25,000 | `CostCenterOwner` approval |
| Amount from SEK 25,000 to 99,999.99 | `FinanceManager` approval |
| Amount at or above SEK 100,000 | `FinanceManagerAndCfo` approval |

Exception codes are sorted before storage to make responses and tests deterministic. An invoice is a touchless candidate only when no exception rule fires. This label does not mean that payment is executed automatically.

## Local example

Start the API as described in the main README, then submit an invoice for the seeded synthetic supplier `SUP-100`:

```powershell
$headers = @{ "X-Integration-Key" = "local-development-key" }
$invoice = @{
  supplierNumber = "SUP-100"
  invoiceNumber = "AP-2026-001"
  issuedOn = "2026-11-01"
  dueOn = "2026-12-01"
  currency = "SEK"
  grossAmount = 12500
  purchaseOrderReference = "PO-9001"
  requestedAccount = "5400"
  requestedCostCenter = "LOG-01"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8080/api/v1/accounts-payable/invoices" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $invoice
```

Read the exception queue and analytics:

```powershell
Invoke-RestMethod -Headers $headers `
  -Uri "http://localhost:8080/api/v1/accounts-payable/exceptions"

Invoke-RestMethod -Headers $headers `
  -Uri "http://localhost:8080/api/v1/accounts-payable/analytics"
```

## Operating notes

- Review exception codes before changing supplier master data or coding rules.
- Treat duplicate conflicts as a control signal; do not bypass them by changing invoice numbers.
- Compare the suggested and requested coding fields when investigating deviations.
- Review exception and touchless-candidate rates together. A high automation-candidate rate is not useful if supplier data or coding rules are wrong.
- Changes to approval thresholds or supplier defaults require regression tests and a documented review.

## Limitations

- Supplier records are seeded synthetic data.
- No OCR, bank payment, tax determination, general-ledger posting, Rillion API, or live ERP write is implemented.
- `FinanceManagerAndCfo` is represented as one composite required role; a production system would record two separate approvals.
- Exceptions are immutable in this reference flow. A production design would add assignment, comments, correction, resubmission, and audit history.
- Analytics are current-state counts, not a historical reporting model.
