using Microsoft.Extensions.Logging;
using Shouldly;

namespace Tests;

[Collection(TestConstants.IntegrationTests), IntegrationTest]
public sealed class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GetApiEventsReturnsValidJson()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
            cancellationToken
        );
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
            clientBuilder.AddStandardResilienceHandler()
        );

        await using var app = await appHost
            .BuildAsync(cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        await app
            .ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
        using var httpClient = app.CreateHttpClient("api");
        using var response = await httpClient.GetAsync("/events?threshold=0.13", cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBe("utf-8");
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        body.ShouldBeValidJson();
        body.ShouldMatchJsonSchema(
            /*lang=json,strict*/
            """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "start": { "type": "string", "format": "date-time" },
                  "end": { "type": "string", "format": "date-time" },
                  "durationMinutes": { "type": "number", "minimum": 0 },
                  "peakValue": { "type": "number", "exclusiveMinimum": 0 }
                },
                "required": [ "start", "end", "durationMinutes", "peakValue" ]
              }
            }
            """
        );

        // Would do separate test method/case here, but that would require setting up a fixture to not recreate the apphost
        var badResponse = await httpClient.GetAsync("/events?threshold=-0.13", cancellationToken);
        badResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
