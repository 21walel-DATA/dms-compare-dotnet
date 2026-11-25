namespace DmsCompare.Api.Models;

// "Positional records": one line declares an immutable type with a constructor and read-only properties.
// Like a TypeScript type plus Object.freeze, with equality by value built in.

/// <summary>Dose and frequency of a medicine on one side of the comparison. Blank values are "".</summary>
public record MedicineDetails(string Dose, string Frequency);

/// <summary>One row of the result. Before is null for STARTED; After is null for STOPPED.</summary>
public record ReconcileLine(string Name, ChangeStatus Status, MedicineDetails? Before, MedicineDetails? After);

/// <summary>How many lines have each status.</summary>
public record ReconcileSummary(int Started, int Stopped, int Changed, int Unchanged);

/// <summary>The JSON body returned by POST /api/reconcile.</summary>
public record ReconcileResponse(ReconcileSummary Summary, IReadOnlyList<ReconcileLine> Lines);
