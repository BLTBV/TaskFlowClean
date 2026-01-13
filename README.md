# TaskFlow (Clean Architecture)

This repo is a re-structured version of your original `TaskFlow.Api` into 4 projects:

- **TaskFlow.Domain** — entities + enums
- **TaskFlow.Application** — DTOs, validators, services (still uses EF Core for simplicity)
- **TaskFlow.Infrastructure** — EF Core DbContext + migrations
- **TaskFlow.Api** — controllers + DI + middleware + Swagger

## Run

1. Configure PostgreSQL connection string in `src/TaskFlow.Api/appsettings.Development.json`
2. From the solution root:

```bash
dotnet restore
dotnet ef database update --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api
dotnet run --project src/TaskFlow.Api
```

Swagger: `/swagger`

## Notes
- For a *fully* clean approach, Application should not depend on EF Core. This version keeps your existing services
  (with minimal changes) so you can run it immediately, while still gaining project separation.
