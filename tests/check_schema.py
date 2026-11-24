"""Exercise actual SQLite schema and consumption SQL; not an HTTP integration test."""
from pathlib import Path
import re
import sqlite3
import tempfile
from concurrent.futures import ThreadPoolExecutor

root = Path(__file__).resolve().parents[1]
source = (root / 'Store.cs').read_text(encoding='utf-8')
program = (root / 'Program.cs').read_text(encoding='utf-8')
schema = re.search(r'Cmd\(c, """(.*?)"""\)', source, re.S).group(1)
consume = re.search(r'"(UPDATE notes SET remaining=remaining-1[^\"]+)"', program).group(1)
with tempfile.TemporaryDirectory() as tmp:
    path = Path(tmp) / 'test.db'
    with sqlite3.connect(path) as c:
        c.executescript(schema)
        assert c.execute("SELECT value FROM settings WHERE name='external_enabled'").fetchone() == (0,)
        for note, limit in [('one', 1), ('multi', 7)]:
            c.execute("INSERT INTO notes(id,token,owner,mode,recipient,expires,remaining,payload,wrapped) VALUES(?,?,'owner','sso','recipient',9999999999,?,X'01',X'02')", (note, note, limit))

    def reveal(note):
        with sqlite3.connect(path, timeout=15) as c:
            c.execute('BEGIN IMMEDIATE')
            if c.execute("SELECT id FROM notes WHERE id=? AND state='active' AND remaining>0", (note,)).fetchone():
                c.execute(consume, {'i': note})
                return 1
            return 0

    for note, limit in [('one', 1), ('multi', 7)]:
        with ThreadPoolExecutor(max_workers=24) as pool:
            assert sum(pool.map(reveal, [note] * 128)) == limit
        with sqlite3.connect(path) as c:
            assert c.execute("SELECT state,remaining,payload,wrapped FROM notes WHERE id=?", (note,)).fetchone() == ('consumed', 0, None, None)
            assert c.execute('PRAGMA integrity_check').fetchone() == ('ok',)
        print(f'SQLite {note}: {limit}/128 successful reveals, key and payload cleared: PASS')
