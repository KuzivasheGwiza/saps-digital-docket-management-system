# SAPS DDMS Angular Client

This directory contains the Angular 21 frontend for SAPS DDMS. See the [repository README](../README.md) for the system overview and complete setup instructions.

```powershell
npm ci
npm start
```

The development client runs at `http://localhost:4200` and expects the API at the URL configured in `src/app/config/api.config.ts`.

Verification commands:

```powershell
npm test -- --watch=false
npm run build
```
