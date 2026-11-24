# Build and CI baseline (issue #1)

SDK pinned to .NET 10.0.404; runtime and package baseline 10.0.15. Official references: https://dotnet.microsoft.com/en-us/download/dotnet/10.0 and https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages. Keep LTS patch updates current; Dependabot configured for NuGet and Actions. No change to .NET 11.

Run from repository root:

```powershell
dotnet restore tests/Sirk.Access.Integration/Sirk.Access.Integration.csproj --use-lock-file
dotnet build tests/Sirk.Access.Integration/Sirk.Access.Integration.csproj -c Release --no-restore
dotnet run --project tests/Sirk.Access.Integration/Sirk.Access.Integration.csproj -c Release --no-build --no-restore
dotnet publish Sirk.Access.csproj -c Release --no-build --no-restore -o "$env:TEMP\sirk-publish"
node --check wwwroot/portal.js
python tests/check_schema.py
python tests/smoke_http.py "$env:TEMP\sirk-publish"
python tests/check_publish.py "$env:TEMP\sirk-publish"
```

The integration project uses a minimal executable harness (no test framework dependency) and real SQLite/AES-GCM/Data Protection. It exits nonzero on failure. HTTP smoke uses a temporary synthetic configuration and local dev certificate verified by Python's TLS context; no real Entra login, mail or IIS. Run only on an isolated developer/CI host.

Restore audits direct and transitive packages and fails on vulnerability or unavailable audit data. First successful CI emits NuGet lock files as a separate artifact. Review and commit BOTH lock files; then require locked restore on all builds. This bootstrap PR does not claim fully reproducible transitive restore until that commit is present.

Artifact guard checks expected binaries/static files, blank sensitive config, and rejects test/data/key/secret file paths. It is not a general secret scanner. Configure GitHub secret scanning and push protection, independent CodeQL/code scanning and branch rules. Do not interpret a successful build as security approval.

## Remaining acceptance

- [ ] CI build, store tests, HTTPS smoke and artifact checks green.
- [ ] Commit reviewed generated lock files and enforce locked mode.
- [ ] Configure repository-level secret scanning/push protection and required build/security checks on main.
- [ ] Review SAST findings and verify enforcement with a controlled synthetic regression.
- [ ] No production release until #36.

GitHub repository settings cannot be changed by the current integration; repository owner must enable or provide a supported management route. All application configuration and secrets remain external to repository and artifact.
