namespace DmsCompare.Api.Models;

/// <summary>
/// What happened to a medicine between admission and discharge.
/// Sent in JSON as UPPERCASE text ("STARTED", "STOPPED"...) — configured once in Program.cs.
/// </summary>
public enum ChangeStatus
{
    Started,
    Stopped,
    Changed,
    Unchanged
}
