# SIRK Access Portal threat model (draft, issue #2)

Status: proposed for review. No production approval. This document describes the current MVP and mandatory target controls; a target control is not evidence of implementation. Use synthetic identities and data in staging.

## 1. Scope and trust boundaries

```text
External browser / Employee browser (untrusted)
    | HTTPS, OIDC redirect, external OTP verification
    v
Internet edge / WAF (trusted proxy only; no TLS downgrade)
    | HTTPS with expected host
    v
Portal process (IIS dedicated low-privilege application pool)
    | local or private authenticated connections
    +--> Entra ID (tenant-bound OIDC; CA controls MFA)
    +--> Mail relay / Graph (OTP only, restricted sender)
    +--> Data store (notes ciphertext, policy, catalog, grants)
    +--> File storage (quarantine separate from published data)
    +--> Key provider (separate access, per-note lifecycle)
    +--> Audit outbox --> independent append-only audit sink / SIEM
    |
    +--> HTTPS redirect to approved applications
          |
          +--> Entra Application Proxy / private connector --> IIS Kerberos backend
          OR separately approved isolated gateway (not implemented)
```

Boundaries: browser to edge; edge to portal; portal to Entra and mail; portal to database, file/key and audit stores; portal to managed application. Treat browser clicks, request bodies, forwarded headers, filenames, mail input, resource URLs and third-party identity claims as untrusted until validated. Never give the portal an AD delegation identity. Gateway configuration must never permit arbitrary destinations. No AD FS.

Assets: external bootstrap credentials, internal notes, OTP and share capabilities, authentication cookies, identity/grant records, Entra client credentials, wrapped note keys, file versions/signatures, application URLs, audit records, operational backups. Classify note content, keys, tokens and initial passwords as restricted; audit metadata as confidential; approved public documentation separately. Maintain separate production/test identities, data and keys.

## 2. Actors and authorization matrix (target)

| Action | Anonymous | External | Employee | Helpdesk | Content manager | App manager | Auditor | Admin |
|---|---|---|---|---|---|---|---|---|
| Receive external note with valid link + mailbox OTP | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| Receive SSO note addressed to same tenant oid | No | Own | Own | Own | Own | Own | Own | Own |
| Create internal note | No | No | Yes | Yes | No* | No* | No* | Yes |
| Create external bootstrap note | No | No | No* | Yes | No | No | No | Yes |
| Read/download/launch assigned resources | No | Explicit | Assigned | Assigned | Assigned | Assigned | Assigned | Assigned |
| Manage onboarding packages | No | No | No | Scoped | No | No | No | Yes |
| Edit documents/installers | No | No | No | No | Scoped | No | No | Yes |
| Edit application tiles | No | No | No | No | No | Scoped | No | Yes |
| View note metadata/revoke | No | Own sent** | Own sent | Scoped | No* | No* | Audit only | Metadata only |
| View/export audit | No | No | No | Scoped operational | No | No | Read/export | Restricted |
| Change policies/grants | No | No | No | Scoped package grants | No | No | No | Yes |
| Read another user's secret | No | No | No | No | No | No | No | No |

* Additional role assignments may grant permission, never the role itself. ** If future external note creation is explicitly approved; currently disabled. Admin cannot grant themselves access to another user's note or bypass recipient verification. Production audit custody must be independent from the portal admin. Current MVP lacks content/app managers, separation of audit custody, multiple grants and restricted external publishing; #4, #17, #20 govern implementation. Deny default, authorize each backend request; UI visibility is not access control.

## 3. Data flows and invariant tests

- Identity: OIDC authorization code + PKCE, issuer/audience/tid/oid/state/nonce validation; Entra assignment and CA MFA. Stable (tid, oid) binding; session and role revocation limits documented. No access/refresh tokens in session or logs. CA evidence from Entra, not assumed from portal login.
- Note creation: writer permission and server policy, recipient binding, random capability (hash at rest), AES-GCM with per-note DEK and note ID AAD; no plaintext persistence. External notes default off. Do not deliver bootstrap link and OTP through same mailbox as independent MFA; prefer separate channels or TAP where appropriate.
- Receive: GET never consumes; POST requires CSRF + validated recipient proof; generic errors, rate limits, OTP single use, per-challenge binding, expiry and atomic read count. Commit consumption and audit before revealing. A lost response may consume a read; never restore a consumed capability automatically.
- Download: explicit authorization and current published version before serving; fail closed if AV/verification unavailable, random path, attachment response. No serving directly from webroot. Log authorized initiation separately from confirmed transfer if measurable.
- Launch: approved HTTPS URL; target enforces its own MFA and authorization; portal tile is not an access boundary. Protected legacy apps only through isolated approved proxy with direct route blocked.
- Audit: authoritative server event for each security-sensitive operation, including denied attempts; metadata-only UI events are best effort and spoofable. No raw URL query/fragment, form values, clipboard, secrets, OTP, or token payload. Correlation IDs, UTC, stable actor and target IDs, outcome. Independent sink, retention, failure detection.
- Backup: active/consumed/revoked/expired note states are authoritative; restored backup cannot reactivate notes or retired grants. Prove destruction model before advertising irreversible deletion. Receiver screenshots/copies cannot be revoked.

## 4. STRIDE / risk register

Ratings are preliminary inherent risk (H high, M medium). All rows open until evidence and owner review. Owners are functional roles, not named assignments.

| ID | STRIDE | Attack / prerequisites | Impact | Target control and evidence | Issue | Owner |
|---|---|---|---|---|---|---|
| R01 H | S/E | Foreign tenant token, stale role or unassigned account | Unauthorized access | Tenant-bound OIDC, role matrix, CA pilot, negative auth tests | #3 #4 | Identity |
| R02 H | S/I | Stolen link plus known email | Bootstrap secret theft | OTP mailbox proof, separate channel guidance, single-use binding, no enumeration | #5 | Identity/Notes |
| R03 H | T/E | Concurrent reveal, replay or expiry race | Excess reads, restored credential exposure | Atomic state transition, audit same transaction, 100+ concurrency tests | #7 | Notes |
| R04 H | I | DB/WAL/backups plus key store compromise | Secret disclosure | Envelope keys, independent key lifecycle, restore safety, crypto review | #8 #32 | Crypto/Ops |
| R05 H | E/I | IDOR or direct file/backend route | Cross-user data | Resource authorization per endpoint, private storage and backend isolation | #4 #12 #20 #27 | Access |
| R06 H | T/E | Malicious installer or poisoned file version | Malware distribution | AV, signature/hash, quarantine, approval, fail closed | #12 | Content/Security |
| R07 H | R/T | Portal admin or compromised app rewrites log | Lost forensic evidence | Transactional audit outbox, independent sink, integrity and gap alarms | #9 #11 | Audit/SOC |
| R08 M | R/T | JS disabled, dropped or fabricated click | Incomplete analytics | Separate untrusted UI telemetry and authoritative server audit | #10 | UI/Audit |
| R09 H | I | Secrets in query, form, APM, exception or SIEM | Credential compromise | Redaction, fragment/POST, no-store, proxy log policy, synthetic leakage tests | #9 #14 | Security |
| R10 H | D | OTP floods, upload exhaustion, SQLite locks, audit flood | Availability loss | Global/per-identity quotas, WAF, durable queues, disk/DB alerts | #5 #11 #12 #14 #33 | Ops |
| R11 H | S/T | CSRF, XSS, host or forwarded header spoof | Session theft, false audit | CSP, antiforgery, strict host/TLS, trusted proxy, negative tests | #14 #16 | Security |
| R12 H | E | Delegation account compromise or arbitrary proxy target | AD lateral movement | Separate gateway, explicit SPN, no NTLM/unconstrained delegation, independent review | #27 #28 #29 | AD/Security |
| R13 H | T/E | CI dependency/action compromised | Supply-chain code execution | Pinned actions, dependency audit, SBOM, signed artifacts, review | #1 #34 #36 | Build |
| R14 H | S/E | Overbroad Graph/SMTP permissions | Directory or mailbox abuse | Scoped read/mail permissions, separate optional privileged worker | #21 #22 #24 | Identity |
| R15 H | T | Restore stale grants or consumed note | Access resurrected | Restore reconciliation, backup drill, documented RPO/RTO | #32 | Ops |
| R16 M | I | Excess audit PII and unrestricted exports | Privacy breach | Minimize fields, role separation, retention, export audit | #26 | Privacy/SOC |
| R17 H | D/T | Key loss, multi-node inconsistent state | Unreadable or excess reveals | Key recovery, shared transactional store, worker leases, failover tests | #30 #31 #32 | Ops |

## 5. Verification and release gate

Map target tests to OWASP ASVS (authentication/session, access control, validation, cryptography, logging, files, communications, configuration and architecture); #34 owns exact versioned control IDs and independent review. Record test ID, source commit, environment, expected result, observed evidence, reviewer and residual risk decision. Evidence must contain no credentials, note contents, real tenant identifiers or internal hostnames in this public repository.

Release requires: successful CI, negative tests, Entra CA/MFA pilot, external/employee onboarding E2E, audited privileged mutations, independent audit sink, file quarantine, key/restore drill, operational monitoring and security sign-off. #36 is the final gate. Optional custom gateway stays disabled until #28/#29 approved; supported Entra App Proxy is preferred. HA is required only for HA deployment profile. Single-node RTO/RPO must be explicitly agreed.

## 6. Review checklist

- [ ] Identity owner validates role boundaries, CA and external identity flows.
- [ ] Security owner approves threat model and residual risk handling.
- [ ] Operations owner validates data/key custody, recovery and monitoring.
- [ ] Privacy owner approves audit minimization and retention.
- [ ] QA links executed evidence to each required risk.
- [ ] Release owner records go/no-go under #36.

No reviewer approval or executed production evidence is claimed by this draft.
