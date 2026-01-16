# Admin Demo API

ASP.NET Core WebAPI projekt s Identity frameworkom.

## Funkcie

- ASP.NET Core Identity pre autentifikáciu a autorizáciu
- Entity Framework Core s SQLite
- Swagger/OpenAPI dokumentácia
- RESTful API endpoints pre registráciu, prihlásenie a odhlásenie

## Technológie

- .NET 10.0
- ASP.NET Core WebAPI
- Entity Framework Core 10.0
- ASP.NET Core Identity
- SQLite

## Spustenie

1. Vytvorte databázu pomocou migrations (SQLite súbor sa vytvorí automaticky):
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
3. Spustite aplikáciu:
   ```bash
   dotnet run
   ```
4. Otvorte Swagger UI na: `https://localhost:5001/swagger`

## API Endpoints

- `POST /api/auth/register` - Registrácia nového používateľa
- `POST /api/auth/login` - Prihlásenie
- `POST /api/auth/logout` - Odhlásenie
