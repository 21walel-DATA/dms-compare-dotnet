using DmsCompare.Api.Models;

namespace DmsCompare.Api.Services;

/// <summary>
/// Shared text rules, used by both the service and the request validation,
/// so "what counts as the same medicine" is defined in exactly one place.
/// </summary>
public static class MedicineText
{
    /// <summary>Case-insensitive comparison, e.g. "RAMIPRIL" equals "ramipril".</summary>
    public static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;

    /// <summary>Turns null into "" and removes surrounding spaces: "  5mg " → "5mg", null → "".</summary>
    public static string Clean(string? value) => (value ?? "").Trim();

    /// <summary>
    /// Returns each medicine name that appears more than once in the list (after cleaning, ignoring case).
    /// An empty result means there are no duplicates.
    /// </summary>
    public static List<string> FindDuplicateNames(IEnumerable<Medicine> medicines) =>
        medicines
            .GroupBy(m => Clean(m.Name), IgnoreCase)   // put medicines with the same name into one group
            .Where(group => group.Count() > 1)        // keep only groups with 2 or more medicines
            .Select(group => group.Key)               // take the name of each of those groups
            .ToList();
}
