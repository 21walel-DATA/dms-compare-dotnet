using System.ComponentModel.DataAnnotations;
using DmsCompare.Api.Services;

namespace DmsCompare.Api.Models;

/// <summary>
/// The JSON body sent to POST /api/reconcile.
/// Both lists are required, but either can be an empty array [].
/// </summary>
public record ReconcileRequest : IValidatableObject
{
    // The type is nullable (?) so that a MISSING list arrives as null and [Required] can reject it.
    [Required(ErrorMessage = "beforeAdmission is required (it can be an empty list).")]
    public IReadOnlyList<Medicine>? BeforeAdmission { get; init; }

    [Required(ErrorMessage = "onDischarge is required (it can be an empty list).")]
    public IReadOnlyList<Medicine>? OnDischarge { get; init; }

    /// <summary>
    /// Rules that look at a whole list, not just one field. ASP.NET Core calls this automatically
    /// after the [Required]/[StringLength] checks, and any errors returned here appear in the same
    /// standard 400 response.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = new List<ValidationResult>();
        CheckList(BeforeAdmission, "beforeAdmission", errors);
        CheckList(OnDischarge, "onDischarge", errors);
        return errors;
    }

    private static void CheckList(IReadOnlyList<Medicine>? list, string fieldName, List<ValidationResult> errors)
    {
        if (list is null)
        {
            return; // already reported by [Required]
        }

        // e.g. "beforeAdmission": [null] — a list item with no medicine in it.
        if (list.Any(m => m is null))
        {
            errors.Add(new ValidationResult($"{fieldName} cannot contain empty (null) entries.", new[] { fieldName }));
            return;
        }

        var duplicates = MedicineText.FindDuplicateNames(list);
        if (duplicates.Count > 0)
        {
            errors.Add(new ValidationResult(
                $"{fieldName} contains the same medicine more than once: {string.Join(", ", duplicates)}.",
                new[] { fieldName }));
        }
    }
}
