using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

sealed class Store(string root, IDataProtectionProvider protection)
{
    public string Files { get; } = Path.Combine(root, "files");
    readonly string connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "portal.db"), DefaultTimeout = 15 }.ToString();
    readonly IDataProtector noteProtector = protection.CreateProtector("SIRK.Access.NoteKey.v1");
    readonly IDataProtector otpProtector = protection.CreateProtector("SIRK.Access.OTP.v1");
    public SqliteConnection Open() { var c = new SqliteConnection(connection); c.Open(); return c; }
    public static SqliteCommand Cmd(SqliteConnection c, string sql, params (string, object?)[] args)
    { var cmd = c.CreateCommand(); cmd.CommandText = sql; foreach (var (k,v) in args) cmd.Parameters.AddWithValue(k,v ?? DBNull.Value); return cmd; }
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string Id() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public void Init()
    {
        Directory.CreateDirectory(Files);
        using var c = Open(); using var cmd = Cmd(c, """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS notes(id TEXT PRIMARY KEY, token TEXT UNIQUE NOT NULL, owner TEXT NOT NULL, mode TEXT NOT NULL, recipient TEXT NOT NULL, expires INTEGER NOT NULL, remaining INTEGER NOT NULL, payload BLOB, wrapped BLOB, otp TEXT, otp_exp INTEGER NOT NULL DEFAULT 0, otp_sent INTEGER NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 0, state TEXT NOT NULL DEFAULT 'active');
        CREATE TABLE IF NOT EXISTS resources(id TEXT PRIMARY KEY, title TEXT NOT NULL, category TEXT NOT NULL, kind TEXT NOT NULL, target TEXT NOT NULL, audience TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 0, filename TEXT, sha TEXT);
        CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY AUTOINCREMENT, at INTEGER NOT NULL, actor TEXT NOT NULL, ip TEXT NOT NULL, action TEXT NOT NULL, target TEXT NOT NULL, outcome TEXT NOT NULL, trace TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS settings(name TEXT PRIMARY KEY, value INTEGER NOT NULL);
        INSERT OR IGNORE INTO settings VALUES('max_hours',168),('max_reads',10),('external_enabled',0);
        CREATE INDEX IF NOT EXISTS idx_audit_at ON audit(at);
        CREATE INDEX IF NOT EXISTS idx_notes_exp ON notes(state,expires);
        """); cmd.ExecuteNonQuery();
    }
    public int Setting(string name) { using var c=Open(); using var q=Cmd(c,"SELECT value FROM settings WHERE name=$n",("$n",name)); return Convert.ToInt32(q.ExecuteScalar()); }
    public void Audit(string actor,string ip,string action,string target,string outcome,string trace)
    { using var c=Open(); using var q=Cmd(c,"INSERT INTO audit(at,actor,ip,action,target,outcome,trace) VALUES($at,$a,$ip,$ac,$t,$o,$tr)",("$at",Now),("$a",actor),("$ip",ip),("$ac",action),("$t",target),("$o",outcome),("$tr",trace)); q.ExecuteNonQuery(); }
    public (byte[] payload,byte[] wrapped) Encrypt(string id,string message)
    {
        var key=RandomNumberGenerator.GetBytes(32); var nonce=RandomNumberGenerator.GetBytes(12); var plain=Encoding.UTF8.GetBytes(message); var cipher=new byte[plain.Length]; var tag=new byte[16];
        try { using var aes=new AesGcm(key,16); aes.Encrypt(nonce,plain,cipher,tag,Encoding.UTF8.GetBytes(id)); return (nonce.Concat(tag).Concat(cipher).ToArray(),noteProtector.Protect(key)); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public string Decrypt(string id,byte[] payload,byte[] wrapped)
    {
        var key=noteProtector.Unprotect(wrapped); var plain=new byte[payload.Length-28];
        try { using var aes=new AesGcm(key,16); aes.Decrypt(payload.AsSpan(0,12),payload.AsSpan(28),payload.AsSpan(12,16),plain,Encoding.UTF8.GetBytes(id)); return Encoding.UTF8.GetString(plain); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public string ProtectOtp(string code) => otpProtector.Protect(code);
    public bool VerifyOtp(string protectedCode,string input) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(otpProtector.Unprotect(protectedCode)),Encoding.UTF8.GetBytes(input));
    public void Expire()
    { using var c=Open(); using var tx=c.BeginTransaction(deferred:false); var now=Now; using(var audit=Cmd(c,"INSERT INTO audit(at,actor,ip,action,target,outcome,trace) SELECT $now,'system','','note.expire',id,'success','cleanup' FROM notes WHERE state='active' AND expires<=$now",("$now",now))){audit.Transaction=tx;audit.ExecuteNonQuery();} using var q=Cmd(c,"UPDATE notes SET state='expired',payload=NULL,wrapped=NULL,otp=NULL WHERE state='active' AND expires<=$now",("$now",now)); q.Transaction=tx;q.ExecuteNonQuery();tx.Commit(); }
}
sealed class Cleanup(Store store, ILogger<Cleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    { using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1)); do { try { store.Expire(); } catch(Exception ex) { logger.LogError("Note expiry failed: {Type}",ex.GetType().Name); } } while(await timer.WaitForNextTickAsync(stoppingToken)); }
}
