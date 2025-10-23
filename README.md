# Overflow Event Detector
https://github.com/DrypDevelopment/event-detection-challenge submission

*Notes*:
- [Api/EventDetector.cs#L65](./Api/EventDetector.cs#L65) notes some ambiguity about the sample end times.

> Be explicit about inclusive/exclusive end time — note it in your README and test against it.

Not entirely sure what was meant by this, but primarily [EventDetectorTests.cs#L102](./Tests/EventDetectorTests.cs#L102) lists the expected interpretation of minDuration and maxGap.

## Prerequisites
- .NET 10.0-RC2 SDK or later. For Windows development, I currently recommend getting Visual Studios Insiders that includes it: `winget install Microsoft.VisualStudio.Community.Insiders`
- Running Docker host, for Windows I recommend Rancher Desktop: `winget install SUSE.RancherDesktop` 

### Recommended for development (not necessary for evaluation / running the solution)
- Install local dotnet tools *(done automatically if running the .NET Aspire [AppHost](./AppHost/AppHost.csproj#L14))*: 
    ```pwsh
    dotnet tool restore
    dotnet husky install
    ```
- Install CSharpier & IDE extension
    - https://marketplace.visualstudio.com/items?itemName=csharpier.CSharpier / https://marketplace.visualstudio.com/items?itemName=csharpier.csharpier-vscode
    - `dotnet tool install -g csharpier`
    - Update IDE settings to format on save

## How to run
From the [solution](OverflowEventDetector.slnx)
-  Either, run the [AppHost](./AppHost/AppHost.csproj) project, which will also launch the Aspire dashboard for observability **(Recommended)**
- Or run the [Api](./Api/Api.csproj) project, which will only run the API

Both can also be run from the command line, using the `dotnet run` command.

### Working with the API
Once the project is running, the [Api/api.http](Api/api.http) file contains sample requests that can be used to test the API. If not running Aspire, update the base url port.

## Tests
Tests are in the [Tests](./Tests) project.

From the root of the repository, run `dotnet test` to run the tests (alternatively `dotnet build && .\Tests\bin\Debug\net10.0\Tests.exe` for xUnit output), alternatively run unit tests from your IDE.

## Publish to Docker (compose)
The .NET Aspire AppHost can be used to publish to a Docker Compose environment.

To do this, run the following command from the repository root: `dotnet aspire publish -o docker-compose-artifacts`. 

To run the docker compose environment:
```pwsh
cd docker-compose-artifacts
docker compose up
```