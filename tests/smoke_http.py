"""Start the published app over local TLS, without real Entra credentials."""
import http.client
import os
from pathlib import Path
import socket
import ssl
import subprocess
import sys
import tempfile
import time

publish = Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix="sirk-smoke-") as tmp:
    root = Path(tmp)
    cert = root / "localhost.pem"
    subprocess.run(["dotnet", "dev-certs", "https", "--export-path", str(cert),
                    "--format", "PEM", "--no-password"], check=True, timeout=60)
    ctx = ssl.create_default_context(cafile=str(cert))
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        port = s.getsockname()[1]
    origin = f"https://localhost:{port}"
    env = os.environ.copy()
    for key in tuple(env):
        if key.lower().startswith(("entra__", "portal__", "kestrel__", "aspnetcore_", "dotnet_environment")):
            env.pop(key)
    env.update({
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": f"https://127.0.0.1:{port}",
        "Kestrel__Certificates__Default__Path": str(cert),
        "Kestrel__Certificates__Default__KeyPath": str(cert.with_suffix(".key")),
        "AllowedHosts": "localhost",
        "Entra__TenantId": "11111111-1111-4111-8111-111111111111",
        "Entra__ClientId": "22222222-2222-4222-8222-222222222222",
        "Entra__ClientSecret": "synthetic-smoke-only",
        "Portal__PublicOrigin": origin,
        "Portal__DataDirectory": str(root / "data"),
    })

    def request(method, path, headers=None, body=None):
        conn = http.client.HTTPSConnection("localhost", port, context=ctx, timeout=3)
        try:
            conn.request(method, path, body=body, headers=headers or {})
            res = conn.getresponse()
            return res.status, dict(res.getheaders()), res.read()
        finally:
            conn.close()

    with (root / "server.log").open("w+b") as log:
        proc = subprocess.Popen(["dotnet", "Sirk.Access.dll"], cwd=publish,
                                env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic() + 45
            while True:
                if proc.poll() is not None:
                    raise RuntimeError("Application exited before readiness")
                try:
                    status, headers, body = request("GET", "/receive")
                    if status == 200:
                        break
                except (OSError, http.client.HTTPException):
                    pass
                if time.monotonic() >= deadline:
                    raise RuntimeError("Readiness deadline exceeded")
                time.sleep(0.5)
            assert b"SIRK Access Portal" in body
            normalized = {k.lower(): v for k, v in headers.items()}
            assert normalized.get("cache-control") == "no-store"
            assert normalized.get("referrer-policy") == "no-referrer"
            assert "frame-ancestors 'none'" in normalized.get("content-security-policy", "")
            assert request("GET", "/portal.js")[0] == 200
            assert request("POST", "/notes/read", {"Content-Type": "application/x-www-form-urlencoded"}, "token=invalid")[0] == 400
            assert request("GET", "/receive", {"Host": "wrong.invalid"})[0] == 400
            assert (root / "data" / "portal.db").is_file()
            print("Published app HTTPS / headers / CSRF / host / SQLite: PASS")
        except Exception:
            log.flush()
            log.seek(0)
            # All configuration and content in this isolated test is synthetic.
            print(log.read().decode("utf-8", errors="replace"), file=sys.stderr)
            raise
        finally:
            proc.terminate()
            try:
                proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=10)
