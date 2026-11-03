# Case study: SAPS Digital Docket Management System (academic prototype)

## 1. Business context

The University of Pretoria / Boxfusion capstone brief identified problems with paper-based criminal-case docket handling, including missing or misplaced files, fragmented records, restricted case visibility, delays and audit-trail weaknesses. The team explored a secure digital docket-management prototype to address these workflow issues. The project was **not** a national rollout or replacement for existing SAPS systems.

## 2. My responsibility

I was the **primary developer** of **Administration, User and Docket Management** subsystems within the group project. That responsibility does not mean I developed the entire team system or its Reports subsystem.

## 3. Requirements interpreted into system behaviour

| Business need | Examples of use cases and rules in my subsystem scope | Technical response |
| --- | --- | --- |
| Controlled access | Create/read/update accounts; deactivate/reactivate/suspend/unlock | Account-management screens and API logic with role-aware actions |
| Secure self-service for users | Login, password recovery/change, profile changes, logout, feedback | Authenticated Angular workflows and backend endpoints |
| Structured registration | Create docket with required incident information and a unique identifier | Validated form, REST API and SQL Server persistence |
| Accountable modifications | Restrict editing of archived dockets; capture change reasons and audit history | Status checks, permissions and event recording |
| Controlled allocation | Assign an OPEN docket to an eligible officer; review an allocation | Role-aware allocation workflows and assignment state changes |

The system specification documents **10 Administration**, **8 User** and **6 Docket Management** use cases. These numbers measure documented functional scope, not successful production use.

## 4. Technology and implementation approach

- Angular + TypeScript for interface components, forms and API integrations.
- ASP.NET Core 8 Web API in C# for server-side request handling and business rules.
- Entity Framework Core 8 + Microsoft SQL Server for relational data access.
- JWT / role-permission checks as described in project design.
- Swagger UI used by me for **manual API testing** and response validation.

## 5. What made it technically interesting

The project required business rules that cut across data and UI boundaries: permissions on account changes, user/session lifecycle handling, docket status restrictions, and eligibility constraints during allocation. The important challenge was not only storing records, but keeping state transitions consistent across the frontend, API and database.

## 6. Outcome and boundaries

The capstone produced a **working academic prototype** demonstrating digital workflow concepts. I can discuss my subsystem implementations, requirements, architecture and manual API checks. There are **no verified production adoption, time-saving or national rollout metrics** to claim.

## 7. What I would improve next

With appropriate access and approval: refine error handling, improve accessibility, establish reproducible development configuration, build a privacy-safe demo with fictional records, and document traceability between requirements, API behaviour and implementation status.

> This public case study paraphrases project requirements at a high level. It does not reproduce confidential client documents, real docket contents or sensitive system information.