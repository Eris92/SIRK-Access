"""Check deployment contents and fail on accidental configuration/secret artifacts."""
import json
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()
for path in ("Sirk.Access.dll", "Sirk.Access.deps.json", "Sirk.Access.runtimeconfig.json",
             "web.config", "wwwroot/portal.js", "wwwroot/portal.css", "appsettings.json"):
    assert (root / path).is_file(), f"Missing artifact: {path}"
settings = json.loads((root / "appsettings.json").read_text(encoding="utf-8"))
for section, names in {
    "Entra": ("TenantId", "ClientId", "ClientSecret"),
    "Portal": ("DataDirectory", "SmtpHost", "SmtpUser", "SmtpPassword", "SmtpFrom"),
}.items():
    for name in names:
        assert not settings[section][name], f"Nonempty deployment setting: {section}:{name}"
for path in root.rglob("*"):
    relative = path.relative_to(root)
    assert not any(part.lower() in {".git", "tests", "data", "keys", "scripts", "docs"} for part in relative.parts), f"Unexpected path: {relative}"
    if path.is_file():
        assert not path.name.lower().startswith((".env", "appsettings.local", "portal.db"))
        assert path.suffix.lower() not in {".pfx", ".p12", ".pem", ".key", ".db", ".sqlite", ".sqlite3"}
print("Publish artifact layout and empty secret configuration: PASS")
