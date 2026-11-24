# SIRK Access Portal

Self-hosted access and secure information sharing portal. **MVP / non-production until build, integration tests and security review pass.**

## Features

- Single-tenant Microsoft Entra ID OIDC authorization code + PKCE, application roles and server-side authorization. MFA is enforced by Conditional Access in Entra, not by the application.
- External secure notes: high-entropy link, assigned email, eight-digit email OTP, expiration, maximum reads, revocation.
- SSO secure notes: assigned tenant-local Entra object ID, not mutable email/UPN. Recipient must sign in.
- Notes: AES-256-GCM unique per-note data key, wrapped using ASP.NET Core Data Protection, Windows DPAPI-protected key ring. Atomic read count and final consumption. No plaintext stored in database.
- Resource dashboard: HTTPS application links and protected document/installer downloads, assigned by app role or object ID; category groups onboarding resources.
- Administration: create/edit/publish/disable resources, upload files pending manual review, note policy limits, revoke active notes, audit viewer.
- Audit: request route templates, status codes, actor, source IP, UTC, trace, security events, UI clicks (best effort). No note contents, OTP, share links, form values, or tokens logged by app.

## Deployment

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) and [docs/SECURITY.md](docs/SECURITY.md). This version uses SQLite in WAL mode, local file storage, Windows DPAPI and a single IIS worker. No HA or KCD implementation in this release. The portal is a launcher, not a network tunnel or auth proxy. Use Entra Application Proxy for protected legacy Windows Authentication apps.

## Build

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet publish -c Release -o publish
```

## Publish new private GitHub repo

Requires Git, GitHub CLI and an authenticated account with repository creation permission. Script refuses an existing repository to prevent overwriting.

```powershell
gh auth login
.\scripts\Publish-GitHub.ps1
```

## Status

Initial source and CI configuration. C# build and real Entra/SMTP/IIS integration have not been executed in the generation environment. No credentials or real tenant data included. Do not publish to the Internet until acceptance criteria pass.
