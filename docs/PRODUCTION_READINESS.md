# SAPS DDMS Production Readiness

## Purpose

SAPS DDMS is an operational digital docket management system for docket registration, investigation notes, evidence handling, person linking, chain of custody, prosecutor bundles, audit review, reporting, AI assistance, system configuration, backup review, and controlled communications.

This document records what is production-ready, what is controlled by configuration, and what must be completed by an SAPS/DBA/ICT operations team before official live deployment.

## Security

- Passwords are stored using BCrypt hashing.
- Login supports account lockout, activation, password reset, configurable session timeout, and optional OTP.
- OTP delivery is generated through the SMS service.
- The SMS service supports two modes:
  - `LocalQueue`: stores messages for communications officer review and manual dispatch.
  - `HttpGateway`: sends to a configured HTTP SMS provider using `SmsGateway:ApiUrl` and `SmsGateway:ApiKey`.
- Roles are enforced through route guards and API authorization attributes.
- Role and permission management APIs exist for flexible profile administration.
- Development SQL can use a local application login to avoid Windows SSPI failures. Run `SAPS.DDMS.API/Database/DevelopmentSqlLogin.sql` in SQLCMD mode with a private `DDMS_SQL_PASSWORD`; never store that value in source control.
- Production SQL must use a DBA-managed service account with `Encrypt=True` and a trusted certificate.

## Audit And Accountability

- User actions are logged through the audit service.
- Login, logout, user lifecycle, role change, docket status changes, station changes, backup requests, report PDF exports, SMS queue actions, evidence transfer, and AI interactions are auditable.
- Audit search supports investigation by date, user, and transaction type.
- SMS queue actions remain visible even when messages are cancelled.

## Privacy And Evidence Handling

- Dockets support privacy flags and privacy-aware prosecutor bundle output.
- Evidence supports file/image upload, evidence metadata, location tracking, and chain-of-custody transfer records.
- Evidence transfer requires reason/signature metadata and is logged.
- AI-generated summaries and extracted data are logged and require verification.

## Backup And Restore

- The front end creates server-side application snapshot packages containing operational tables and managed uploads.
- Restore validates the package and schema before changing data.
- Database rows are replaced inside a transaction; failures roll back the database operation.
- Managed uploads are restored with rollback protection, active sessions are cleared, and the result is audited.
- Restore is destructive by design and must be limited to authorized administrators with an approved recovery procedure.

## Deployment

Before official deployment:

1. Host the API on an approved IIS, Windows Server, or container platform.
2. Host the Angular build behind HTTPS.
3. Replace development SQL credentials with DBA-managed production credentials.
4. Configure SQL Server encryption with a trusted certificate.
5. Confirm SQL Server authentication mode and restart the SQL Server service after DBA configuration changes.
6. Configure a real SMS provider or formally approve LocalQueue manual dispatch.
7. Configure SMTP credentials through environment variables or a secrets manager.
8. Set CORS to the official production front-end URL only.
9. Enable centralized logs and backup retention.
10. Apply database scripts in `SAPS.DDMS.API/Database`.
11. Run final UAT with SAPS role representatives.

## Known Operational Limitations

- LocalQueue is intentionally not a live SMS gateway. It is an auditable manual dispatch queue.
- Application restore replaces operational data and uploads from a compatible snapshot; production use requires authorization and a tested recovery plan.
- Production secrets must not be stored in source-controlled JSON files.
- AI features are deterministic/local unless a production AI provider is configured and approved.
- Official go-live requires SAPS ICT security review, privacy review, and hosting approval.
