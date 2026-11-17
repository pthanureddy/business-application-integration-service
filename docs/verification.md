# Verification record

Verified locally on 17 November 2026 with .NET 8.

## Results

| Check | Result |
| --- | --- |
| `dotnet format BusinessApplicationIntegration.sln --verify-no-changes` | Passed |
| Release build with warnings as errors | Passed, 0 warnings and 0 errors |
| Automated test suite | 33 passed, 0 failed, 0 skipped |
| Coverage | 90.04% line, 69.08% branch |
| OpenAPI validation | Passed with Redocly CLI 1.34.5 |
| AsyncAPI validation | Passed with AsyncAPI CLI 3.4.0 |
| NuGet vulnerability audit | No vulnerable packages reported |

The GitHub Actions workflow additionally builds the Docker image and compiles both Azure Bicep templates. Hosted results must be checked against the commit containing this record.

## AP workflow checks

The automated suite verifies:

- supplier-master coding proposals;
- unknown-supplier exception routing;
- missing purchase-order and coding-deviation exception codes;
- natural-key duplicate rejection;
- amount-based approval roles;
- invalid-role and invalid-state protection;
- exception-queue retrieval;
- due-date validation; and
- deterministic exception and touchless-candidate analytics.

## Evidence boundary

The AP flow uses synthetic supplier and invoice data. It is not connected to Rillion, an accounting department, a bank, or a production ERP. No claim is made for tax calculation, payment execution, ledger posting, production scale, or user adoption.
