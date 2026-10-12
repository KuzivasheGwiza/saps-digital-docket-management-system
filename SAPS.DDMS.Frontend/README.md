# SAPS DDMS Angular Client

## Overview

This project is the responsive web interface for SAPS DDMS. It turns complex docket, evidence, investigation, administration, and reporting workflows into role-aware screens that work on desktop and smaller displays.

The client solves two usability problems: operational users should see only the actions relevant to their role, and complex case information should remain searchable and understandable without navigating separate systems.

## Features

- Secure sign-in, account activation, password recovery, optional OTP, and automatic inactivity handling
- Lazy-loaded, role-protected routes and permission-aware navigation
- Docket creation, filtering, allocation, reassignment, sensitivity, court outcome, and archive workflows
- Investigation notes, people, evidence, and chain-of-custody interfaces
- Dashboards, reports, charts, calendars, editable station hierarchy, and PDF export actions
- Administration for users, roles, system settings, audit history, SMS queue, and backup/restore
- Responsive navigation, form validation, accessible dialogs, loading states, and API error feedback

## Screenshot gallery

These screenshots were captured from the running application. Captions identify the workflow and, where relevant, the user role shown.

### Secure access and administration

| Sign-in | System Administrator dashboard |
| --- | --- |
| ![Sign-in form with personnel number or email and password](../docs/images/login.png) | ![System Administrator dashboard with administration shortcuts](../docs/images/admin-dashboard.png) |

| User management | Station hierarchy |
| --- | --- |
| ![User Management table in the System Administrator account](../docs/images/user-management.png) | ![Province, city and station hierarchy in the System Administrator account](../docs/images/station-hierarchy.png) |

### Docket intake and allocation

| Create docket: classifications | Create docket: address search and map |
| --- | --- |
| ![Case type, sensitivity and incident fields in the Create New Docket form](../docs/images/create-docket-incident.png) | ![Address search and interactive map picker in the Create New Docket form](../docs/images/create-docket-location.png) |

| Create docket: narrative and complainant | DSC Supervisor docket actions |
| --- | --- |
| ![Docket summary, modus operandi and complainant fields](../docs/images/create-docket-complainant.png) | ![Docket list with allocation action menu in the DSC Supervisor account](../docs/images/docket-management.png) |

### People, evidence and prosecutor workflow

| Person management | Evidence management |
| --- | --- |
| ![Person Management table with linked-person actions](../docs/images/person-management.png) | ![Investigating Officer evidence page with a selected docket](../docs/images/evidence-management.png) |

![Prosecutor bundle list with privacy controls and Preview and Share action](../docs/images/prosecutor-bundle.png)

*Prosecutor bundle management in the DSC Supervisor workflow.*

## Technologies

- Angular 21 and TypeScript 5.9
- Angular Router, Material, and CDK
- RxJS and Angular HTTP client
- Bootstrap and Font Awesome
- Chart.js for report visualizations
- Leaflet for geographic views
- Vitest, jsdom, and Angular testing utilities
- Nginx and Docker for production hosting

## My contribution

I personally coded the entire system, including the frontend and its integration with the API and database. On the client application, I developed the role-aware screens, docket reassignment and sensitivity controls, report presentation, editable station tree, court and identity validation feedback, inactivity handling, backup/restore experience, responsive navigation, and automated component and service tests.

## Setup

From this directory:

```powershell
npm ci
npm start
```

Open `http://localhost:4200`. The development proxy in `proxy.conf.json` forwards `/api` requests to the ASP.NET Core API.

For the complete database and API setup, see the [repository setup guide](../docs/SETUP.md).

## Verification

```powershell
npm test -- --watch=false
npm run build
npm audit
```

Generated folders such as `node_modules`, `.angular`, `dist`, and coverage output are intentionally excluded from source control.
