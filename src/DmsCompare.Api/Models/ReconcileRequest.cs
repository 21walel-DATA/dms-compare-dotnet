using System.ComponentModel.DataAnnotations;

namespace DmsCompare.Api.Models;

/// <summary>
/// The JSON body sent to POST /api/reconcile.
/// Both lists are required, but either can be an empty array [].
/// </summary>
public record ReconcileRequest
{
    // The type is nullable (?) so that a MISSING list arrives as null and [Required] can reject it.
    [Required(ErrorMessage = "beforeAdmission is required (it can be an empty list).")]
    public IReadOnlyList<Medicine>? BeforeAdmission { get; init; }

    [Required(ErrorMessage = "onDischarge is required (it can be an empty list).")]
    public IReadOnlyList<Medicine>? OnDischarge { get; init; }
}
