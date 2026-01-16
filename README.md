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

### Spustenie v Docker kontajneri (odporúčané pre vývoj)

Najjednoduchší spôsob spustenia pomocou Docker:

```bash
./start-dev.sh
```

Alebo manuálne:

```bash
docker-compose -f docker-compose.dev.yml up --build
```

Aplikácia bude dostupná na: `http://localhost:8080`
Swagger UI: `http://localhost:8080/swagger`

**Užitočné príkazy:**
- Zobraziť logy: `docker-compose -f docker-compose.dev.yml logs -f`
- Zastaviť: `docker-compose -f docker-compose.dev.yml down`
- Reštartovať: `docker-compose -f docker-compose.dev.yml restart`

### Lokálne spustenie (bez Docker)

1. Vytvorte databázu pomocou migrations (SQLite súbor sa vytvorí automaticky):
   ```bash
   cd AdminDemo.Api
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
2. Spustite aplikáciu:
   ```bash
   dotnet run --launch-profile http
   ```
3. Otvorte Swagger UI na: `http://localhost:8080/swagger`

## API Endpoints

- `POST /api/auth/register` - Registrácia nového používateľa
- `POST /api/auth/login` - Prihlásenie
- `POST /api/auth/logout` - Odhlásenie
