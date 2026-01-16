# Admin Demo API

ASP.NET Core WebAPI projekt s Identity frameworkom.

## Funkcie

- ASP.NET Core Identity pre autentifikáciu a autorizáciu
- Entity Framework Core s SQL Server
- Swagger/OpenAPI dokumentácia
- RESTful API endpoints pre registráciu, prihlásenie a odhlásenie

## Technológie

- .NET 10.0
- ASP.NET Core WebAPI
- Entity Framework Core 10.0
- ASP.NET Core Identity
- SQL Server

## Spustenie

1. Nastavte connection string v `appsettings.json`
2. Vytvorte databázu pomocou migrations:
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
