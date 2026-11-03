# SAPS DDMS API

## Overview

This project is the ASP.NET Core backend for SAPS DDMS. It centralizes operational business rules and exposes secure APIs for the Angular client. The API solves the consistency and accountability problems created when docket information, evidence movements, deadlines, and user actions are managed independently.

## Features

- JWT authentication, BCrypt password hashing, account activation, reset, lockout, OTP, and sessions
- Role and permission enforcement at API boundaries
- Docket registration, allocation, reassignment, status history, sensitivity, archive, and court workflows
- Investigation notes, attachments, linked people, evidence, and chain-of-custody transfers
- Prosecutor bundles, privacy controls, notifications, audit trails, and configurable operational rules
- Dashboard queries, report datasets, chart/PDF generation, and data exchange
- Validated snapshot packages with transactional table restore, managed-upload rollback, and session invalidation
- SQL Server migrations plus operational schema/reference-data bootstrap checks

## Technologies

- C# and ASP.NET Core 8 Web API
- Entity Framework Core 8 with Microsoft SQL Server
- JWT bearer authentication and BCrypt.Net
- QuestPDF and ScottPlot for report output
- MailKit/MimeKit for email integration
- Swagger/OpenAPI for development-time API exploration
- Docker for deployment

## API areas

| Area | Responsibilities |
| --- | --- |
| Authentication | Login, activation, OTP, password reset, sessions |
| Dockets | Registration, assignment, status, sensitivity, court outcomes |
| Investigation | Notes, attachments, people, evidence, custody transfers |
| Administration | Users, roles, stations, settings, SMS queue, audit history |
| Reporting | Operational datasets, charts, PDF exports, calendars |
| Recovery | Backup creation, download, validation, and transactional restore |

## Screenshots

The API supplies the docket-linked evidence records and prosecutor-bundle workflow shown in these application screens:

| Docket-linked evidence | Prosecutor bundle |
| --- | --- |
| ![Evidence records returned for a selected docket](../docs/images/evidence-management.png) | ![Shared and draft prosecutor bundles with export and sharing actions](../docs/images/prosecutor-bundle.png) |

## My contribution

I was the **primary developer of the Administration, User and Docket Management subsystems** in this team capstone. My backend responsibilities covered the C# / ASP.NET Core API functionality and Entity Framework Core / SQL Server integrations supporting account administration, user authentication and lifecycle workflows, docket registration, status controls and allocation.

I manually exercised API endpoints and checked responses using **Swagger UI**. The API also includes other team-level functionality (such as reporting, backup/restore and evidence workflows) and a separate automated-test project; those are not claims that I personally developed or tested every module.

See the [root project README](../README.md) and [my scope and use-case map](../docs/FEATURE_SCOPE.md) for a clear breakdown.

## Setup

From the repository root, restore tools and packages:

```powershell
dotnet tool restore
dotnet restore SAPS-DDMS.sln
```

Configure local secrets:

```powershell
$connection = 'Server=.\SQLEXPRESS;Database=SAPS.DDMS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $connection --project SAPS.DDMS.API
dotnet user-secrets set 'JwtSettings:SecretKey' $jwtKey --project SAPS.DDMS.API
dotnet ef database update --project SAPS.DDMS.API --startup-project SAPS.DDMS.API
```

Run the API:

```powershell
dotnet run --project SAPS.DDMS.API
```

Configuration files contain placeholders only. Production connection strings, JWT keys, SMTP credentials, and SMS provider secrets must come from environment variables or a secrets manager.

## Verification

```powershell
dotnet test SAPS.DDMS.API.Tests --configuration Release
```

See the [test-project README](../SAPS.DDMS.API.Tests/README.md) for covered use cases and the [production-readiness guide](../docs/PRODUCTION_READINESS.md) before deployment.
