# WineAgentFlow

Lightweight sample demonstrating a concurrent agent workflow that loads an OpenAPI spec (`spec.json`), registers an OpenAPI tool, creates multiple agents, fans out requests to them, aggregates responses, streams output, and deletes agents.

## Features
- Loads an OpenAPI spec from `spec.json`
- Creates an `OpenApiToolDefinition` and multiple agents
- Builds a concurrent workflow (fan-out / fan-in)
- Streams execution and prints aggregated output
- Cleans up agents after execution

## Prerequisites (Windows)
- .NET SDK (compatible version for the project)
- JetBrains Rider 2025.2.2.1 (optional — used for development)
- Azure CLI (if using `AzureCliCredential`) and logged in: `az login`
- Git

## Getting started

1. Clone repository
   - `git clone https://github.com/rpbs/WineAgentFlow.git`
   - `git checkout master`

2. Place the OpenAPI spec
   - Put your spec file at the project root as `spec.json` or adjust the path in `Program.cs`.

3. Configure Azure / credentials (if applicable)
   - Ensure Azure CLI is authenticated (`az login`) if `AzureCliCredential` is used.
   - Provide any required endpoint/connection values the app expects (inspect `Program.cs` for exact env var names or constants).

4. Build
   - `dotnet build`

5. Run
   - `dotnet run --project WineAgentFlow`
