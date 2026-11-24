"""Exercise the actual SQLite schema and consumption SQL. Not an ASP.NET integration test."""
from pathlib import Path
import re
import sqlite3
import tempfile
from concurrent.futures import ThreadPoolExecutor

root = Path(__file__).resolve().parents[1]
source = (root / 'Store.cs').read_text()
program = (root / 'Program.cs').read_text()
schema = re.search(r'Cmd\(c, """(.*?)"""\)', source, re.S).group(1)
consume = re.search(r'"(UPDATE notes SET remaining=remaining-1[^\"]+)"', program).group(1)
with tempfile.TemporaryDirectory() as tmp:
    path = Path(tmp) / 'test.db'
    with sqlite3.connect(path) as c:
        c.executescript(schema)
        assert c.execute("SELECT value FROM settings WHERE name='external_enabled'").fetchone() == (0,)
        c.execute("INSERT INTO notes(id,token,owner,mode,recipient,expires,remaining,payload,wrapped) VALUES('n','hash','owner','sso','recipient',9999999999,1,X'01',X'02')")
    def reveal(_):
        with sqlite3.connect(path, timeout=10) as c:
            c.execute('BEGIN IMMEDIATE')
            if c.execute("SELECT id FROM notes WHERE id='n' AND state='active' AND remaining>0").fetchone():
                c.execute(consume, {'i': 'n'})
                return 1
            return 0
    with ThreadPoolExecutor(max_workers=8) as pool:
        assert sum(pool.map(reveal, range(16))) == 1
    with sqlite3.connect(path) as c:
        assert c.execute("SELECT state,remaining,payload,wrapped FROM notes WHERE id='n'").fetchone() == ('consumed',0,None,None)
        assert c.execute('PRAGMA integrity_check').fetchone() == ('ok',)
print('SQLite schema and concurrent one-read consumption: PASS')
