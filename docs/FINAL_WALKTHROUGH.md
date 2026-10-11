# SAPS DDMS Final Walkthrough

## Iteration 4 Use Cases

All Iteration 4 use cases have working system coverage:

- User accounts: create, read, update.
- Authentication: login, change password, activate account.
- Dockets: create, read, update, search/filter.
- Investigation notes: create, read, update.
- Evidence: create, read, update.
- Persons: create, read, update, link person to docket.

## Iteration 5 Use Cases

All Iteration 5 functional areas have coverage:

- User lifecycle: deactivate, reactivate, suspend, unlock, role history, role change.
- FAQ: create, update, delete, view help/FAQ.
- Auth/profile: forgot password, update profile, notifications, logout.
- Dockets: archive, allocation review, court outcome, close, reopen.
- Notes: archive request and review.
- Evidence: transfer and chain of custody.
- Prosecutor bundles: create, view, edit, export, privacy flags.
- Audit: audit trail and AI interaction log.
- AI: summary, extracted-data verification, related-case suggestions.

## SAPS Problem Statement Coverage

- Digital docket registration with OB/FIC references and incident information.
- Role-based access for SAPS operational roles.
- Docket allocation and investigation assignment.
- Evidence management with upload and chain-of-custody transfer.
- Person management for complainants, suspects, victims, and witnesses.
- Audit trail for accountability.
- Prosecutor bundle workflow with privacy redaction.
- Court outcome and case closure/reopen flow.
- AI support with logged interactions and verification.

## Complexity Matrix Coverage

- Responsive GUI with grids, tables, tabs, links, charts, image/file upload, tree view, calendar, and media help.
- Dynamic province/station tree backed by database create/update/delete.
- Admin-configurable timers and business rules stored in database.
- Database target met: live database contains more than 30 tables.
- Reports: simple list, transactional, adjustable criteria, management graph, and PDF export.
- Security: login, fixed roles, flexible role/permission API, encrypted passwords, optional OTP.
- Audit trail and audit search.
- Validated backup creation and transactional restore workflow.
- JSON snapshot export and report PDF export.
- API/data/business separation.
- Stored procedures, trigger, and scheduled backup marker script.
- AI/ML-style innovation through summary, extraction verification, suggestions, and AI logs.

## Final Demonstration Script

1. Log in as System Administrator.
2. Review dashboard graphs, SLA panel, and recent dockets.
3. Create or update a user, then view role history.
4. Open System Config and change an SLA/timer setting.
5. Open SMS Queue and review generated OTP/notification messages.
6. Create/search/update a docket.
7. Allocate a docket, accept assignment, and inspect the SLA timer.
8. Add investigation notes and attachments.
9. Add evidence and record an evidence transfer.
10. Link a person to the docket.
11. Generate AI summary and verify AI extracted data.
12. Create/export prosecutor bundle with privacy flag behavior.
13. Record court outcome, close, and reopen case.
14. Open Reports and export a PDF.
15. Open Calendar to show docket dates.
16. Create a backup, make a reversible test-data change, restore the backup, and verify the original data returns.
17. Open Audit to show logged transactions.
