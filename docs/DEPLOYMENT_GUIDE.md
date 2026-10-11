# SAPS DDMS Deployment Guide

This guide prepares SAPS DDMS for the deployment complexity requirements using Docker-based production assets.

## What is included

- `SAPS.DDMS.API/Dockerfile` for the ASP.NET Core 8 API.
- `SAPS.DDMS.Frontend/Dockerfile` and `SAPS.DDMS.Frontend/nginx.conf` for the Angular frontend.
- `docker-compose.production.yml` for running both services together.
- `.env.production.example` with the required production environment variables.
- `SAPS.DDMS.API/appsettings.Production.json` with non-secret production defaults.

## One-time setup

1. Copy `.env.production.example` to `.env.production`.
2. Replace every placeholder value with the real production values.
3. Make sure the SQL Server instance is reachable from the API container.
4. Make sure the SMTP and optional SMS gateway credentials are valid.
5. Point the DNS name in `DDMS_APP_URL` to the public frontend host.

## Build and run

From the solution root:

```powershell
docker compose --env-file .env.production -f docker-compose.production.yml up --build -d
```

## Public endpoints

- Frontend: `http://<host>/`
- API: `http://<host>:8080/`

In a production go-live, place the frontend behind HTTPS and keep the API behind a reverse proxy or secured internal endpoint.

## Remote database requirement

The API reads `ConnectionStrings__DefaultConnection` from the environment, so the same build can connect to a remote SQL Server without changing source code.

## CORS

`Program.cs` now reads `Cors:AllowedOrigins`, which is supplied in production through:

- `appsettings.Production.json`, or
- environment variables such as `Cors__AllowedOrigins__0`

This keeps the production frontend host configurable instead of hard-coded.

## Operational note

After deployment, verify:

1. Login
2. OTP queue or gateway behaviour
3. Email delivery
4. Reports PDF export
5. Backup creation and restore
6. Uploaded evidence and attachment retrieval
