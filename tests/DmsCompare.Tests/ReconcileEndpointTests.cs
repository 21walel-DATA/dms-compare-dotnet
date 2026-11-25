using System.Net;
using System.Text;
using System.Text.Json;
using DmsCompare.Api.Models;
using DmsCompare.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DmsCompare.Tests;

/// <summary>
/// Integration tests: WebApplicationFactory starts the real app (Program.cs, controllers, validation,
/// JSON settings) in memory, and we send real HTTP requests to it. Like Supertest in Express.
/// </summary>
public class ReconcileEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ReconcileEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    [Fact]
    public async Task Health_returns_200_ok()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Valid_request_returns_200_with_the_expected_json()
    {
        const string request = """
            {
              "beforeAdmission": [
                { "name": "Ramipril", "dose": "5mg", "frequency": "once daily" },
                { "name": "Aspirin", "dose": "75mg", "frequency": "once daily" }
              ],
              "onDischarge": [
                { "name": "Ramipril", "dose": "10mg", "frequency": "once daily" },
                { "name": "Atorvastatin", "dose": "20mg", "frequency": "at night" }
              ]
            }
            """;

        var response = await _client.PostAsync("/api/reconcile", Json(request));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);

        var summary = body.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("started").GetInt32());
        Assert.Equal(1, summary.GetProperty("stopped").GetInt32());
        Assert.Equal(1, summary.GetProperty("changed").GetInt32());
        Assert.Equal(0, summary.GetProperty("unchanged").GetInt32());

        var lines = body.GetProperty("lines");
        Assert.Equal(3, lines.GetArrayLength());

        // Statuses are UPPERCASE strings, in the required order.
        Assert.Equal("CHANGED", lines[0].GetProperty("status").GetString());
        Assert.Equal("Ramipril", lines[0].GetProperty("name").GetString());
        Assert.Equal("5mg", lines[0].GetProperty("before").GetProperty("dose").GetString());
        Assert.Equal("10mg", lines[0].GetProperty("after").GetProperty("dose").GetString());

        Assert.Equal("STOPPED", lines[1].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, lines[1].GetProperty("after").ValueKind);

        Assert.Equal("STARTED", lines[2].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, lines[2].GetProperty("before").ValueKind);
    }

    [Fact]
    public async Task Empty_lists_return_200_with_zero_counts()
    {
        var response = await _client.PostAsync("/api/reconcile",
            Json("""{ "beforeAdmission": [], "onDischarge": [] }"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal(0, body.GetProperty("lines").GetArrayLength());
        Assert.Equal(0, body.GetProperty("summary").GetProperty("started").GetInt32());
    }

    [Fact]
    public async Task Missing_name_returns_400_problem_details()
    {
        var response = await _client.PostAsync("/api/reconcile",
            Json("""{ "beforeAdmission": [ { "dose": "5mg" } ], "onDischarge": [] }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await ReadJson(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("beforeAdmission[0].name", out _));
    }

    [Fact]
    public async Task Blank_name_returns_400()
    {
        var response = await _client.PostAsync("/api/reconcile",
            Json("""{ "beforeAdmission": [], "onDischarge": [ { "name": "   " } ] }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Name_longer_than_100_characters_returns_400()
    {
        var longName = new string('A', 101);
        var response = await _client.PostAsync("/api/reconcile",
            Json($$"""{ "beforeAdmission": [ { "name": "{{longName}}" } ], "onDischarge": [] }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_list_returns_400()
    {
        var response = await _client.PostAsync("/api/reconcile",
            Json("""{ "beforeAdmission": [] }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJson(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("onDischarge", out _));
    }

    [Fact]
    public async Task Duplicate_name_in_one_list_returns_400_with_a_clear_message()
    {
        var response = await _client.PostAsync("/api/reconcile", Json("""
            {
              "beforeAdmission": [ { "name": "Ramipril", "dose": "5mg" }, { "name": "ramipril ", "dose": "10mg" } ],
              "onDischarge": []
            }
            """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJson(response)).GetProperty("errors");
        var message = errors.GetProperty("beforeAdmission")[0].GetString();
        Assert.Contains("more than once", message);
        Assert.Contains("Ramipril", message);
    }

    [Fact]
    public async Task Malformed_json_returns_400()
    {
        var response = await _client.PostAsync("/api/reconcile", Json("{ this is not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unexpected_error_returns_500_without_leaking_details()
    {
        // Thanks to dependency injection, we can swap the real service for one that always crashes.
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IReconciliationService, ExplodingService>()))
            .CreateClient();

        var response = await client.PostAsync("/api/reconcile",
            Json("""{ "beforeAdmission": [], "onDischarge": [] }"""));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret internal detail", text);
        Assert.DoesNotContain("ExplodingService", text);   // no stack trace
    }

    /// <summary>A fake service used only by the test above.</summary>
    private class ExplodingService : IReconciliationService
    {
        public ReconcileResponse Reconcile(IReadOnlyList<Medicine> beforeAdmission, IReadOnlyList<Medicine> onDischarge) =>
            throw new InvalidOperationException("secret internal detail");
    }
}
