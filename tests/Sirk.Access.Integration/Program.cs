using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

var root = Path.Combine(Path.GetTempPath(), "sirk-access-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var provider = new EphemeralDataProtectionProvider();
    var store = new Store(root, provider);
    store.Init();
    store.Init();
    Check(store.Setting("external_enabled") == 0, "External notes disabled by default");
    var id = Store.Id();
    var message = "synthetic test \u0141\u00f3d\u017a \uD83D\uDE80\n";
    var first = store.Encrypt(id, message);
    var second = store.Encrypt(id, message);
    Check(!first.payload.SequenceEqual(second.payload), "Randomized encryption");
    Check(store.Decrypt(id, first.payload, first.wrapped) == message, "Unicode roundtrip");
    Check(!Encoding.UTF8.GetString(first.payload).Contains(message, StringComparison.Ordinal), "No plaintext payload");
    var tampered = first.payload.ToArray();
    tampered[^1] ^= 1;
    Reject(() => store.Decrypt(id, tampered, first.wrapped), "Tampered ciphertext");
    Reject(() => store.Decrypt(Store.Id(), first.payload, first.wrapped), "Wrong associated data");
    Reject(() => new Store(root, new EphemeralDataProtectionProvider()).Decrypt(id, first.payload, first.wrapped), "Wrong wrapping key");
    var code = store.ProtectOtp("01234567");
    Check(store.VerifyOtp(code, "01234567"), "Valid OTP");
    Check(!store.VerifyOtp(code, "01234568"), "Invalid OTP");
    using (var c = store.Open())
    using (var q = Store.Cmd(c,
        "INSERT INTO notes(id,token,owner,mode,recipient,expires,remaining,payload,wrapped) VALUES($i,$t,'owner','sso','recipient',$e,1,$p,$w)",
        ("$i", id), ("$t", Store.Hash(Store.Id())), ("$e", Store.Now - 1), ("$p", first.payload), ("$w", first.wrapped)))
        q.ExecuteNonQuery();
    store.Expire();
    store.Expire();
    using (var c = store.Open())
    {
        using var q = Store.Cmd(c, "SELECT state,payload,wrapped FROM notes WHERE id=$i", ("$i", id));
        using var r = q.ExecuteReader();
        Check(r.Read() && r.GetString(0) == "expired" && r.IsDBNull(1) && r.IsDBNull(2), "Expired note cleared");
        r.Close();
        using var a = Store.Cmd(c, "SELECT COUNT(*) FROM audit WHERE action='note.expire' AND target=$i", ("$i", id));
        Check(Convert.ToInt64(a.ExecuteScalar()) == 1, "Expiry audit exactly once");
    }
    Console.WriteLine("Store integration checks: PASS");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(root, recursive: true);
}

static void Check(bool result, string name)
{
    if (!result) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

static void Reject(Action action, string name)
{
    try { action(); }
    catch (CryptographicException) { Console.WriteLine("PASS: " + name); return; }
    throw new InvalidOperationException("FAIL: " + name);
}
