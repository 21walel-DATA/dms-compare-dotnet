using System.ComponentModel.DataAnnotations;

namespace DmsCompare.Api.Models;

/// <summary>
/// One medicine in a request list, e.g. { "name": "Ramipril", "dose": "5mg", "frequency": "once daily" }.
/// The attributes in [square brackets] are validation rules that ASP.NET Core checks automatically
/// before the controller runs (similar to a Zod schema in TypeScript).
/// </summary>
public record Medicine
{
    // Required + AllowEmptyStrings = false also rejects "" and "   ".
    [Required(AllowEmptyStrings = false, ErrorMessage = "Each medicine needs a name.")]
    [StringLength(100, ErrorMessage = "Medicine name must be 100 characters or fewer.")]
    public string Name { get; init; } = "";

    // string? means "may be missing (null)". Optional fields.
    [StringLength(100, ErrorMessage = "Dose must be 100 characters or fewer.")]
    public string? Dose { get; init; }

    [StringLength(100, ErrorMessage = "Frequency must be 100 characters or fewer.")]
    public string? Frequency { get; init; }
}
