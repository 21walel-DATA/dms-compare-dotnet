using DmsCompare.Api.Models;
using DmsCompare.Api.Services;

namespace DmsCompare.Tests;

/// <summary>
/// Unit tests: they call ReconciliationService directly, with no web server and no HTTP.
/// Synthetic example medicines only.
/// </summary>
public class ReconciliationServiceTests
{
    private readonly ReconciliationService _service = new();

    // Small helper so each test reads like the JSON it represents.
    private static Medicine Med(string name, string? dose = null, string? frequency = null) =>
        new() { Name = name, Dose = dose, Frequency = frequency };

    private static IReadOnlyList<Medicine> List(params Medicine[] medicines) => medicines;

    // ---------- Each status on its own ----------

    [Fact]
    public void Medicine_only_on_discharge_is_Started()
    {
        var result = _service.Reconcile(List(), List(Med("Atorvastatin", "20mg", "at night")));

        var line = Assert.Single(result.Lines);
        Assert.Equal("Atorvastatin", line.Name);
        Assert.Equal(ChangeStatus.Started, line.Status);
        Assert.Null(line.Before);
        Assert.Equal(new MedicineDetails("20mg", "at night"), line.After);
    }

    [Fact]
    public void Medicine_only_before_admission_is_Stopped()
    {
        var result = _service.Reconcile(List(Med("Aspirin", "75mg", "once daily")), List());

        var line = Assert.Single(result.Lines);
        Assert.Equal(ChangeStatus.Stopped, line.Status);
        Assert.Equal(new MedicineDetails("75mg", "once daily"), line.Before);
        Assert.Null(line.After);
    }

    [Fact]
    public void Different_dose_is_Changed()
    {
        var result = _service.Reconcile(
            List(Med("Ramipril", "5mg", "once daily")),
            List(Med("Ramipril", "10mg", "once daily")));

        var line = Assert.Single(result.Lines);
        Assert.Equal(ChangeStatus.Changed, line.Status);
        Assert.Equal(new MedicineDetails("5mg", "once daily"), line.Before);
        Assert.Equal(new MedicineDetails("10mg", "once daily"), line.After);
    }

    [Fact]
    public void Different_frequency_is_Changed()
    {
        var result = _service.Reconcile(
            List(Med("Metformin", "500mg", "twice daily")),
            List(Med("Metformin", "500mg", "three times daily")));

        Assert.Equal(ChangeStatus.Changed, Assert.Single(result.Lines).Status);
    }

    [Fact]
    public void Same_dose_and_frequency_is_Unchanged()
    {
        var result = _service.Reconcile(
            List(Med("Amlodipine", "5mg", "once daily")),
            List(Med("Amlodipine", "5mg", "once daily")));

        var line = Assert.Single(result.Lines);
        Assert.Equal(ChangeStatus.Unchanged, line.Status);
        Assert.NotNull(line.Before);
        Assert.NotNull(line.After);
    }

    // ---------- Matching rules ----------

    [Fact]
    public void Name_matching_ignores_case_and_surrounding_spaces()
    {
        var result = _service.Reconcile(
            List(Med("ramipril ", "5mg", "once daily")),
            List(Med("  RAMIPRIL", "5mg", "once daily")));

        var line = Assert.Single(result.Lines);
        Assert.Equal(ChangeStatus.Unchanged, line.Status);
    }

    [Fact]
    public void Dose_and_frequency_comparison_ignores_case_and_surrounding_spaces()
    {
        var result = _service.Reconcile(
            List(Med("Ramipril", "5MG ", "Once Daily")),
            List(Med("Ramipril", " 5mg", "once daily ")));

        Assert.Equal(ChangeStatus.Unchanged, Assert.Single(result.Lines).Status);
    }

    [Fact]
    public void Missing_and_blank_dose_count_as_the_same()
    {
        var result = _service.Reconcile(
            List(Med("Paracetamol", dose: null, frequency: "when required")),
            List(Med("Paracetamol", dose: "  ", frequency: "when required")));

        var line = Assert.Single(result.Lines);
        Assert.Equal(ChangeStatus.Unchanged, line.Status);
        Assert.Equal("", line.Before!.Dose);
    }

    [Fact]
    public void Line_shows_trimmed_discharge_spelling_of_the_name()
    {
        var result = _service.Reconcile(
            List(Med("ramipril", "5mg")),
            List(Med("  Ramipril  ", "10mg")));

        Assert.Equal("Ramipril", Assert.Single(result.Lines).Name);
    }

    // ---------- Edge cases ----------

    [Fact]
    public void Both_lists_empty_gives_empty_result_and_zero_counts()
    {
        var result = _service.Reconcile(List(), List());

        Assert.Empty(result.Lines);
        Assert.Equal(new ReconcileSummary(0, 0, 0, 0), result.Summary);
    }

    [Fact]
    public void Duplicate_name_in_one_list_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => _service.Reconcile(
            List(Med("Ramipril", "5mg"), Med(" ramipril", "10mg")),
            List()));

        Assert.Contains("Ramipril", ex.Message);
    }

    // ---------- Ordering and summary ----------

    [Fact]
    public void Output_is_Changed_then_Stopped_then_Started_then_Unchanged_alphabetical_within_each()
    {
        var before = List(
            Med("Simvastatin", "40mg"),   // unchanged
            Med("Bisoprolol", "2.5mg"),   // unchanged
            Med("Warfarin", "3mg"),       // stopped
            Med("Aspirin", "75mg"),       // stopped
            Med("Ramipril", "5mg"),       // changed
            Med("Furosemide", "40mg"));   // changed
        var after = List(
            Med("Simvastatin", "40mg"),
            Med("Bisoprolol", "2.5mg"),
            Med("Ramipril", "10mg"),
            Med("Furosemide", "80mg"),
            Med("Lansoprazole", "15mg"),  // started
            Med("Apixaban", "5mg"));      // started

        var result = _service.Reconcile(before, after);

        var order = result.Lines.Select(l => $"{l.Status}:{l.Name}").ToList();
        Assert.Equal(
            new[]
            {
                "Changed:Furosemide", "Changed:Ramipril",
                "Stopped:Aspirin", "Stopped:Warfarin",
                "Started:Apixaban", "Started:Lansoprazole",
                "Unchanged:Bisoprolol", "Unchanged:Simvastatin",
            },
            order);
        Assert.Equal(new ReconcileSummary(Started: 2, Stopped: 2, Changed: 2, Unchanged: 2), result.Summary);
    }

    [Fact]
    public void Example_from_the_brief_gives_the_expected_result()
    {
        var result = _service.Reconcile(
            List(Med("Ramipril", "5mg", "once daily"), Med("Aspirin", "75mg", "once daily")),
            List(Med("Ramipril", "10mg", "once daily"), Med("Atorvastatin", "20mg", "at night")));

        Assert.Equal(new ReconcileSummary(Started: 1, Stopped: 1, Changed: 1, Unchanged: 0), result.Summary);
        Assert.Equal(
            new[] { ChangeStatus.Changed, ChangeStatus.Stopped, ChangeStatus.Started },
            result.Lines.Select(l => l.Status));
    }
}
