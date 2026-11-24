# Staging acceptance gate (not yet executed)

- Build with .NET 10, verify dependency audit and IIS Hosting Bundle. Verify host and HTTPS restrictions.
- Entra: valid login, invalid tenant/token rejected, unassigned user denied, MFA required in Conditional Access sign-in logs, logout, expiration, role separation. Test B2B guest resource tenant oid.
- Verify direct resource URL denied for anonymous/wrong role/wrong oid, approved file works, disabled file fails. Test path traversal, executable extension whitelist, Content-Disposition, max upload size, AV scan and signature review.
- External note: wrong email, invalid OTP, expired OTP, sixth attempt denied, cooldown, wrong link, expired link, revoked link, no use on GET/prefetch. Correct link/email/OTP reveals; second use fails when limit=1. For multiple reads request new OTP. Concurrent requests must not exceed maximum reads. Check SMTP error response does not disclose existence. Test rate limits and proxy IP behavior.
- SSO note: no access anonymous, wrong oid, unauthorized role; intended oid with MFA can reveal; token not in request logs. Validate Unicode, HTML/JS injection and literal whitespace in notes.
- Audit: sign-in/failure/logout, every endpoint, create/read/revoke/deny, download, admin edit/publish/disable, click and copy event. Verify no secret, URL token, OTP or input text in database logs/diagnostics. Test SIEM forwarding separately; click loss documented.
- Restart worker and verify decryption. Test consistent backups and restore without reactivating consumed notes; check disk ACL, key ring ACL, full disk behavior, DB lock contention and fail-safe behavior.
- Security review: OWASP ASVS, dependency scanning, secret scanning, auth bypass, CSRF, XSS, business logic, DoS, audit tamper resistance. No Internet deployment until accepted.
