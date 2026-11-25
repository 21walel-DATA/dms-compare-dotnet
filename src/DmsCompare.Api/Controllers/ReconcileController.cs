using DmsCompare.Api.Models;
using DmsCompare.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DmsCompare.Api.Controllers;

/// <summary>
/// HTTP layer only: receive the request, hand it to the service, return the result.
/// No comparison logic lives here.
/// </summary>
[ApiController]                 // turns on automatic validation: invalid body → 400 before Post() runs
[Route("api/reconcile")]
public class ReconcileController : ControllerBase
{
    private readonly IReconciliationService _reconciliationService;

    // Dependency injection: ASP.NET Core sees this constructor needs an IReconciliationService
    // and passes in whatever Program.cs registered for it (ReconciliationService).
    public ReconcileController(IReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    /// <summary>Compares the medicines before admission with the medicines on discharge.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReconcileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<ReconcileResponse> Post(ReconcileRequest request)
    {
        // If we get here, validation has passed, so both lists are present (not null).
        // The ! tells the compiler "I know these are not null".
        var result = _reconciliationService.Reconcile(request.BeforeAdmission!, request.OnDischarge!);
        return Ok(result);
    }
}
