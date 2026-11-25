using DmsCompare.Api.Models;

namespace DmsCompare.Api.Services;

/// <summary>
/// The contract for comparing two medicine lists. The controller depends on this interface,
/// not on the concrete class, so the implementation can be swapped (or faked in tests).
/// </summary>
public interface IReconciliationService
{
    ReconcileResponse Reconcile(IReadOnlyList<Medicine> beforeAdmission, IReadOnlyList<Medicine> onDischarge);
}
