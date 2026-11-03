# Administration, User and Docket Management — my development scope

This page describes my **primary full-stack development responsibility** within the University of Pretoria / Boxfusion SAPS DDMS team prototype. The 24 use cases below are from the project specification, not a claim of 24 independently verified production features.

| Administration (10) | User (8) | Docket Management (6) |
| --- | --- | --- |
| 1.1 Create user account | 2.1 Log in | 3.1 Create docket |
| 1.2 Read user account | 2.2 Create password | 3.2 Read docket |
| 1.3 Update user account | 2.3 Update password | 3.3 Update docket |
| 1.4 Deactivate account | 2.4 Forgot password | 3.4 Archive docket |
| 1.5 Reactivate account | 2.5 Update profile | 3.5 Allocate docket |
| 1.6 Suspend account | 2.6 Review feedback | 3.6 Review allocation |
| 1.7 Unlock account | 2.7 Log out | |
| 1.8 Create FAQ | 2.8 View FAQ | |
| 1.9 Update FAQ | | |
| 1.10 Delete FAQ | | |

## Rules that influenced implementation

- Only appropriately authorised roles may administer users, change account states and access restricted functions.
- Docket creation must validate required case information and identifier rules.
- Modifying core docket information requires status/permission checks and traceability.
- Archival restricts subsequent changes; docket allocation is conditional on eligible docket and officer states.
- User login, password recovery, account/profile updates and session controls span both UI and API boundaries.

## Architecture and verification

The implemented subsystems span Angular / TypeScript, ASP.NET Core 8 Web API / C#, Entity Framework Core and SQL Server. My **hands-on API testing used Swagger UI**, with manual requests and response checks.

The repository contains additional modules and test tooling. They should not be interpreted as my individual implementation or automated-testing responsibility. The project is **an academic prototype**, not an operational SAPS deployment.

## Traceability exercise for technical interviews

| Use case | Relevant repository implementation | Manual validation approach |
| --- | --- | --- |
| 1.1 Create user | `SAPS.DDMS.API/Controllers/UserController.cs` and Angular administration UI | Verify authorised vs unauthorised account creation with Swagger |
| 2.1 Login | `SAPS.DDMS.API/Controllers/AuthController.cs` and Angular login UI | Verify accepted/rejected authentication responses |
| 3.1 Create docket | `SAPS.DDMS.API/Controllers/DocketController.cs` and Angular docket UI | Verify required-field and valid-creation responses |
| 3.5 Allocate docket | Docket API and Angular allocation workflow | Verify permissions and invalid allocation responses |

The table maps to relevant code areas; specific endpoint paths and the execution results should be confirmed from a fresh local run before publishing detailed test evidence.
