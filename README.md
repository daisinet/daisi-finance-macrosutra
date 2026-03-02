# MacroSutra

Finance application for the Daisinet ecosystem.

## Projects

| Project | Description |
|---------|-------------|
| `MacroSutra.Core` | Domain models, enums, and interfaces |
| `MacroSutra.Data` | Cosmos DB data access layer |
| `MacroSutra.Services` | Business logic |
| `MacroSutra.Web` | Blazor Server web application |
| `MacroSutra.Tools` | Daisinet bot tool integration |
| `MacroSutra.Tests` | Unit tests |
| `MacroSutra.SDK` | SDK for external consumers |

## Getting Started

### Prerequisites

- .NET 10 SDK
- Azure Cosmos DB account (or emulator)

### Configuration

Set the Cosmos DB connection string in user secrets:

```bash
cd MacroSutra.Web
dotnet user-secrets set "Cosmo:ConnectionString" "AccountEndpoint=https://...;AccountKey=..."
```

### Running

```bash
cd MacroSutra.Web
dotnet run --launch-profile https
```

### Running Tests

```bash
dotnet test MacroSutra.Tests
```
