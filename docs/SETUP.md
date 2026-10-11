# SAPS DDMS Local Setup

## Prerequisites

- Git
- .NET 8 SDK
- Node.js 20 or later with npm
- SQL Server 2019 or later, including SQL Server Express

The Angular CLI and Entity Framework CLI are project-managed; global installations are not required.

## 1. Clone and restore

```powershell
git clone <repository-url> SAPS-DDMS
cd SAPS-DDMS
dotnet tool restore
dotnet restore SAPS-DDMS.sln
cd SAPS.DDMS.Frontend
npm ci
cd ..
```

## 2. Configure local secrets

The committed configuration files contain no credentials. Store the development connection string and JWT key in .NET user secrets:

```powershell
$connection = 'Server=.\SQLEXPRESS;Database=SAPS.DDMS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $connection --project SAPS.DDMS.API
dotnet user-secrets set 'JwtSettings:SecretKey' $jwtKey --project SAPS.DDMS.API
```

For SQL authentication, create the local application login with `SAPS.DDMS.API/Database/DevelopmentSqlLogin.sql` in SQLCMD mode and keep its password outside the repository.

## 3. Create or update the database

```powershell
dotnet ef database update --project SAPS.DDMS.API --startup-project SAPS.DDMS.API
```

This applies the checked-in Entity Framework migrations. The API also verifies operational schema extensions and reference data during startup.

## 4. Run the application

Start the API from the repository root:

```powershell
dotnet run --project SAPS.DDMS.API
```

Start the Angular client in another terminal:

```powershell
cd SAPS.DDMS.Frontend
npm start
```

Open `http://localhost:4200`. The development proxy forwards `/api` requests to the API.

## Verification

```powershell
dotnet test SAPS.DDMS.API.Tests --configuration Release
cd SAPS.DDMS.Frontend
npm test -- --watch=false
npm run build
npm audit
```

## Common problems

- Database connection failure: confirm the SQL Server instance is running and the user-secret connection string names the correct instance.
- Port already in use: stop the existing process or run Angular with `npm start -- --port 4201`.
- Missing frontend packages: rerun `npm ci` from `SAPS.DDMS.Frontend`.
- Migration tool unavailable: rerun `dotnet tool restore` from the repository root.
