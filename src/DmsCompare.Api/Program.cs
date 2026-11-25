using System.Text.Json;
using System.Text.Json.Serialization;
using DmsCompare.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

// ---------- 1. Register services (the dependency injection "container") ----------
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options =>
    {
        // Validation error keys use the JSON names the caller sent ("beforeAdmission[0].name"),
        // not the C# property names ("BeforeAdmission[0].Name").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options =>
    {
        // Send enums as UPPERCASE text: ChangeStatus.Started → "STARTED".
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

// "When something asks for IReconciliationService, give it a ReconciliationService."
// Scoped = one instance per HTTP request.
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();

// Standard error format (ProblemDetails, RFC 9457) for errors.
builder.Services.AddProblemDetails();

// Swagger: generates the OpenAPI description and the "try it out" web page.
builder.Services.AddSwaggerGen();

// ---------- 2. Build the app and set up the request pipeline (middleware, in order) ----------
var app = builder.Build();

// Catches any unexpected exception and returns a plain 500 ProblemDetails
// with no stack trace or internal details. The real error is still written to the server log.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      // serves /swagger/v1/swagger.json
    app.UseSwaggerUI();    // serves the page at /swagger
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
