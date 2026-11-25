# DMS Compare (.NET)

[![CI](https://github.com/21walel-DATA/dms-compare-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/21walel-DATA/dms-compare-dotnet/actions/workflows/ci.yml)

> **Synthetic data only. Not for clinical use.**
> This API makes no clinical decisions. It only labels differences between two medicine lists so that a
> pharmacist or prescriber can review them. All example medicines are made up; no real patient data is used.

A small ASP.NET Core Web API (C#, .NET 10) that compares a patient's medicines **before hospital admission** with
their medicines **on discharge**, and labels each one **STARTED**, **STOPPED**, **CHANGED** or **UNCHANGED**.
This is the core comparison engine of [DischargeIQ](https://github.com/21walel-DATA/dischargeiq), my full-stack
TypeScript app for the NHS Discharge Medicines Service, rebuilt in C# as a way to learn .NET. I spent 15 years in
UK pharmacy, and checking these lists by hand takes 15–20 minutes per discharge.

## How to run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/DmsCompare.Api
```

Then open the Swagger UI at **http://localhost:5086/swagger** to try the endpoint in your browser
(Swagger is enabled in the Development environment, which `dotnet run` uses by default).

## How to test

```bash
dotnet test
```

There are 23 tests:
- **Unit tests** (`ReconciliationServiceTests`) call the comparison logic directly, with no HTTP.
- **Integration tests** (`ReconcileEndpointTests`) start the whole app in memory with `WebApplicationFactory`
  and send real HTTP requests (200, 400 and 500 cases).

## Endpoints

| Method | Path             | Purpose                                |
|--------|------------------|----------------------------------------|
| POST   | `/api/reconcile` | Compare two medicine lists             |
| GET    | `/api/health`    | Returns `{ "status": "ok" }`           |

### Example request

```json
{
  "beforeAdmission": [
    { "name": "Ramipril", "dose": "5mg", "frequency": "once daily" },
    { "name": "Aspirin", "dose": "75mg", "frequency": "once daily" }
  ],
  "onDischarge": [
    { "name": "Ramipril", "dose": "10mg", "frequency": "once daily" },
    { "name": "Atorvastatin", "dose": "20mg", "frequency": "at night" }
  ]
}
```

### Example response (200 OK)

```json
{
  "summary": { "started": 1, "stopped": 1, "changed": 1, "unchanged": 0 },
  "lines": [
    { "name": "Ramipril", "status": "CHANGED", "before": { "dose": "5mg", "frequency": "once daily" }, "after": { "dose": "10mg", "frequency": "once daily" } },
    { "name": "Aspirin", "status": "STOPPED", "before": { "dose": "75mg", "frequency": "once daily" }, "after": null },
    { "name": "Atorvastatin", "status": "STARTED", "before": null, "after": { "dose": "20mg", "frequency": "at night" } }
  ]
}
```

### curl

```bash
curl -X POST http://localhost:5086/api/reconcile \
  -H "Content-Type: application/json" \
  -d '{"beforeAdmission":[{"name":"Ramipril","dose":"5mg","frequency":"once daily"},{"name":"Aspirin","dose":"75mg","frequency":"once daily"}],"onDischarge":[{"name":"Ramipril","dose":"10mg","frequency":"once daily"},{"name":"Atorvastatin","dose":"20mg","frequency":"at night"}]}'
```

## Comparison rules

- Medicines are matched by **name**, ignoring case and surrounding spaces (`"ramipril "` = `"Ramipril"`).
- Only on discharge → **STARTED**. Only before admission → **STOPPED**.
- In both lists with a different dose or frequency (ignoring case and surrounding spaces) → **CHANGED**; otherwise **UNCHANGED**.
  A missing dose/frequency counts the same as a blank one.
- Order: CHANGED, STOPPED, STARTED, UNCHANGED; alphabetical by name within each group.
- The same medicine twice in one list → **400 Bad Request** with a clear message.

Invalid input (missing list, missing/blank name, any field over 100 characters, malformed JSON) returns
**400** in the standard [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) format.
Unexpected errors return a plain **500** with no stack trace or internal details.

## Design notes

```
HTTP request → ReconcileController → IReconciliationService → ReconciliationService
               (HTTP only)           (interface)              (pure comparison logic)
```

- **Separation of concerns:** the controller only handles HTTP (receive, return). All comparison logic lives in
  `ReconciliationService`, which has no I/O and can be tested on its own.
- **Interface + dependency injection:** `Program.cs` registers `IReconciliationService → ReconciliationService`.
  The controller asks for the interface in its constructor, so tests can swap in a different implementation
  (one integration test swaps in a service that always throws, to prove errors don't leak).
- **Records** for immutable request/response data; an **enum** for the status, serialised as UPPERCASE text.
- **LINQ** (`Where`, `Select`, `OrderBy`, `GroupBy`) for the comparison itself.
- **Validation** with data annotations (`[Required]`, `[StringLength]`) plus `IValidatableObject` for the
  whole-list duplicate rule, so every 400 has the same standard shape.

### What I'd add next

- **EF Core + SQLite** to save each reconciliation and fetch it with `GET /api/reconcile/{id}`.
- **Authentication** (e.g. JWT bearer tokens; in the NHS, NHS CIS2 smartcard login).
- **Docker** image for consistent deployment.

## Project structure

```
DmsCompare.sln
src/DmsCompare.Api/
  Program.cs                 app setup, dependency injection, Swagger, error handling
  Controllers/               ReconcileController, HealthController
  Models/                    request/response records, ChangeStatus enum
  Services/                  IReconciliationService, ReconciliationService, MedicineText (shared text rules)
tests/DmsCompare.Tests/      xUnit unit + integration tests
.github/workflows/ci.yml     restore, build and test on every push and pull request
```
