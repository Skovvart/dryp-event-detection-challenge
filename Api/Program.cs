using System.ComponentModel.DataAnnotations;
using Api;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults().Services.AddProblemDetails().AddOpenApi().AddValidation();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet(
        "/events",
        static async (
            [Range(0, double.MaxValue)] decimal threshold = 0.0m,
            [Range(0, double.MaxValue)] double minDuration = 5,
            [Range(0, double.MaxValue)] double maxGap = 10
        ) =>
            TypedResults.Json(
                EventDetector.DetectEvents(
                    await EventDetector.SampleTimeSeries.Value,
                    threshold,
                    TimeSpan.FromMinutes(minDuration),
                    TimeSpan.FromMinutes(maxGap)
                ),
                EventJsonSerializerContext.Default.IEnumerableOverflowEvent
            )
    )
    .WithName("DetectEvents");

app.MapDefaultEndpoints();

// evaluate SampleTimeSeries to warm up dataset, but do not delay startup
_ = EventDetector.SampleTimeSeries.Value;

await app.RunAsync();
