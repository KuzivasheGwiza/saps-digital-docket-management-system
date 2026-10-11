# SAPS Digital Docket Management System

SAPS DDMS is a full-stack digital docket management platform for registering cases, allocating investigations, managing evidence and chain of custody, preparing prosecutor bundles, recording court outcomes, reporting, auditing, and operational backup and restore.

## Highlights

- Role-based workflows for seven operational roles
- Docket allocation, controlled reassignment, lifecycle tracking, and sensitivity levels
- Evidence integrity, custody history, investigation notes, and person linking
- Prosecutor bundle preparation, sharing, privacy controls, and court outcomes
- Operational dashboards, configurable reports, station hierarchy, and calendars
- Audited full-system ZIP backup and transactional restore
- Account activation, password reset, lockout, optional OTP, and inactivity timeout
- Configurable SLA, archive, notification, and workload policies

## Technology

- Angular 21 and TypeScript
- ASP.NET Core 8 Web API
- Entity Framework Core 8 and SQL Server
- xUnit and Vitest
- Docker Compose, Nginx, and environment-based production configuration

## Repository layout

```text
SAPS.DDMS.API/          ASP.NET Core API, domain services and migrations
SAPS.DDMS.API.Tests/    Backend use-case and service tests
SAPS.DDMS.Frontend/     Angular client application
docs/                   Setup, deployment and production-readiness guidance
```

## Local development

Prerequisites:

- .NET 8 SDK
- Node.js 20 or later and npm
- SQL Server Express or SQL Server

Restore and start the API:

```powershell
dotnet restore SAPS-DDMS.sln
dotnet run --project SAPS.DDMS.API
```

Install and start the frontend in another terminal:

```powershell
cd SAPS.DDMS.Frontend
npm ci
npm start
```

Open `http://localhost:4200`. Configure local secrets through .NET user secrets or environment variables; do not place credentials in committed configuration files. See [Setup](docs/SETUP.md) for database initialization details.

## Verification

```powershell
dotnet test SAPS.DDMS.API.Tests
cd SAPS.DDMS.Frontend
npm test -- --watch=false
npm run build
```

## Production deployment

Copy `.env.production.example` to a private environment file, replace every placeholder, and follow the [Deployment Guide](docs/DEPLOYMENT_GUIDE.md). Runtime uploads, backup packages, logs, secrets, dependencies, and compiled artifacts are intentionally excluded from source control.

Review [Production Readiness](docs/PRODUCTION_READINESS.md) before any live deployment. A client must provide approved hosting, SQL Server, SMTP/SMS credentials, TLS certificates, retention policy, monitoring, and security ownership.

## Security and privacy

This repository contains source code only. Do not commit production databases, backup packages, uploaded evidence, user exports, access tokens, or environment secrets. Demonstration data must be synthetic and must not contain personal or operational SAPS information.

## Portfolio note

This project demonstrates full-stack architecture, complex role-based workflows, transactional data recovery, auditability, reporting, secure authentication, and automated verification. It is a software portfolio project and does not represent an official public SAPS service.
