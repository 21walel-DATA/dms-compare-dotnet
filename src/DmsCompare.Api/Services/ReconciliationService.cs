using DmsCompare.Api.Models;

namespace DmsCompare.Api.Services;

/// <summary>
/// Compares the before-admission list with the discharge list and labels each medicine
/// STARTED / STOPPED / CHANGED / UNCHANGED. Pure logic: no database, no HTTP, no AI.
/// It only labels differences for a professional to review; it makes no clinical decisions.
/// </summary>
public class ReconciliationService : IReconciliationService
{
    public ReconcileResponse Reconcile(IReadOnlyList<Medicine> beforeAdmission, IReadOnlyList<Medicine> onDischarge)
    {
        // Request validation already blocks duplicates, but the service protects itself too,
        // in case it is ever called from somewhere else.
        RejectDuplicates(beforeAdmission, "beforeAdmission");
        RejectDuplicates(onDischarge, "onDischarge");

        // Look-up tables: cleaned name → medicine. Like a JavaScript Map, with case-insensitive keys.
        var beforeByName = beforeAdmission.ToDictionary(m => MedicineText.Clean(m.Name), MedicineText.IgnoreCase);
        var afterByName = onDischarge.ToDictionary(m => MedicineText.Clean(m.Name), MedicineText.IgnoreCase);

        // STOPPED: in the before list, but not found in the discharge list.
        var stopped = beforeAdmission
            .Where(m => !afterByName.ContainsKey(MedicineText.Clean(m.Name)))
            .Select(m => new ReconcileLine(MedicineText.Clean(m.Name), ChangeStatus.Stopped, Details(m), null));

        // STARTED: in the discharge list, but not found in the before list.
        var started = onDischarge
            .Where(m => !beforeByName.ContainsKey(MedicineText.Clean(m.Name)))
            .Select(m => new ReconcileLine(MedicineText.Clean(m.Name), ChangeStatus.Started, null, Details(m)));

        // In BOTH lists: CHANGED if dose or frequency differs, otherwise UNCHANGED.
        var inBoth = onDischarge
            .Where(m => beforeByName.ContainsKey(MedicineText.Clean(m.Name)))
            .Select(after => CompareSameMedicine(beforeByName[MedicineText.Clean(after.Name)], after));

        // Put all lines together, then sort: by status group first, then alphabetically by name.
        var lines = stopped
            .Concat(started)
            .Concat(inBoth)
            .OrderBy(line => SortRank(line.Status))
            .ThenBy(line => line.Name, MedicineText.IgnoreCase)
            .ToList();

        var summary = new ReconcileSummary(
            Started: lines.Count(l => l.Status == ChangeStatus.Started),
            Stopped: lines.Count(l => l.Status == ChangeStatus.Stopped),
            Changed: lines.Count(l => l.Status == ChangeStatus.Changed),
            Unchanged: lines.Count(l => l.Status == ChangeStatus.Unchanged));

        return new ReconcileResponse(summary, lines);
    }

    /// <summary>Builds a CHANGED or UNCHANGED line for a medicine that is in both lists.</summary>
    private static ReconcileLine CompareSameMedicine(Medicine before, Medicine after)
    {
        var beforeDetails = Details(before);
        var afterDetails = Details(after);

        bool isSame =
            MedicineText.IgnoreCase.Equals(beforeDetails.Dose, afterDetails.Dose) &&
            MedicineText.IgnoreCase.Equals(beforeDetails.Frequency, afterDetails.Frequency);

        var status = isSame ? ChangeStatus.Unchanged : ChangeStatus.Changed;

        // Use the discharge letter's spelling of the name (it is the most recent).
        return new ReconcileLine(MedicineText.Clean(after.Name), status, beforeDetails, afterDetails);
    }

    /// <summary>Dose and frequency with spaces trimmed; missing values become "".</summary>
    private static MedicineDetails Details(Medicine m) =>
        new(MedicineText.Clean(m.Dose), MedicineText.Clean(m.Frequency));

    /// <summary>Output order rule: CHANGED first, then STOPPED, then STARTED, then UNCHANGED.</summary>
    private static int SortRank(ChangeStatus status) => status switch
    {
        ChangeStatus.Changed => 1,
        ChangeStatus.Stopped => 2,
        ChangeStatus.Started => 3,
        _ => 4, // Unchanged
    };

    private static void RejectDuplicates(IReadOnlyList<Medicine> medicines, string listName)
    {
        var duplicates = MedicineText.FindDuplicateNames(medicines);
        if (duplicates.Count > 0)
        {
            throw new ArgumentException(
                $"{listName} contains the same medicine more than once: {string.Join(", ", duplicates)}.");
        }
    }
}
