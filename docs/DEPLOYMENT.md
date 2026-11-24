# Deployment (Windows Server / IIS)

## 1. Identity

Create single-tenant Entra web app registration. Set Web redirect URI `https://access.example.com/signin-oidc` and post-logout redirect URI `https://access.example.com/signout-callback-oidc`. Disable implicit grant and public client flows. Create a client secret, store outside repository, rotate. In enterprise application require assignment. Create app roles with exact values `Admin`, `Employee`, `Helpdesk`, `External`, `Auditor` (allowed member types Users/Groups), assign users/groups. Assign the minimum role set. External technicians can be tenant members or invited B2B guests assigned the External role. For SSO notes use the resource tenant object ID (not home tenant ID). Configure Conditional Access targeting this enterprise app, require MFA or phishing-resistant authentication strength, test with pilot groups; protect admin access and consider sign-in frequency. Entra logs are authoritative for MFA. No MFA claim is inferred by the portal.

## 2. IIS

Install ASP.NET Core .NET 10 Hosting Bundle (current patched runtime), IIS and TLS certificate. Publish application into a dedicated directory. Use separate application pool (No Managed Code, 64-bit, one worker, ApplicationPoolIdentity, load user profile true), anonymous authentication enabled, Windows Authentication disabled. Bind HTTPS only to intended hostname. Configure host filtering. Use a dedicated data directory outside application root and outside any webroot. Grant pool identity modify only to data directory and read/execute to publish directory. Deny access to other application identities. DPAPI key ring is tied to Windows profile/machine; verify restart and backup recovery. Use BitLocker, ACLs and encrypted backups. Configure WAF, request limits, TLS, firewall, egress restriction; never forward arbitrary client-supplied X-Forwarded-* headers. Current app expects actual HTTPS and configured Host. If a proxy terminates TLS, use HTTPS to IIS and preserve Host; audit records proxy IP unless a trusted forwarded-header configuration is added.

Example precheck (admin PowerShell):

```powershell
Get-WindowsFeature Web-Server
Get-Command dotnet
Get-ChildItem Cert:\LocalMachine\My | Select Subject,Thumbprint,NotAfter
```

Configure settings via environment injected into dedicated pool (or protected configuration provider, never commit values):

| Variable | Example / purpose |
|---|---|
| AllowedHosts | access.example.com |
| Entra__TenantId | Tenant GUID |
| Entra__ClientId | Application GUID |
| Entra__ClientSecret | Secret from protected store |
| Portal__PublicOrigin | https://access.example.com |
| Portal__DataDirectory | D:\SirkAccessData |
| Portal__SmtpHost | Authenticated STARTTLS relay |
| Portal__SmtpPort | 587 |
| Portal__SmtpUser | Dedicated relay account |
| Portal__SmtpPassword | Relay secret |
| Portal__SmtpFrom | Approved sender |

External notes are disabled by default; enable them in Administration only after relay verification. SMTP requires a relay with authenticated STARTTLS; this implementation is not Exchange Online OAuth SMTP. Use a scoped service relay, or implement Graph mail with application RBAC before production. Never disable TLS certificate validation. No outbound mail contains notes, links or passwords, only OTP.

Configure IIS logs without query strings; do not capture bodies in WAF, failed request tracing, APM or diagnostics. OIDC callback can carry authorization codes. Do not expose data directory as virtual directory. Confirm no anonymous direct access to file storage. Publish files only after AV scan and signature/hash review outside portal; MVP approval is manual, not an AV integration.

## 3. Verify / rollback

Build and run CI. Execute `docs/ACCEPTANCE.md` on isolated staging. Backup existing application files/config and consistent SQLite backup (stop worker or use SQLite backup API; copying a live database alone is unsafe), file storage and key ring. Deploy new versions to separate directory, switch IIS site physical path during maintenance, verify health and sign-in. Roll back physical path and configuration, and restore compatible data only if needed; never resurrect previously consumed secrets from backup without explicit incident handling. No production changes are performed by repository publishing script.
