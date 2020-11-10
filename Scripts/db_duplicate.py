import sqlite3
def copy_table(oldCon: sqlite3.Connection, newCon: sqlite3.Connection, tbl: str) -> int:
    oldCon.row_factory = sqlite3.Row
    rows = oldCon.execute(f'SELECT * FROM {tbl};').fetchall()
    cmd = f"INSERT INTO {tbl}({', '.join(rows[0].keys())}) VALUES ({', '.join([f':{k}' for k in rows[0].keys()])})"
    newCon.executemany(cmd, rows)
    newCon.commit()
    return len(rows)

def get_connection(dbPath: str) -> sqlite3.Connection:
    con = sqlite3.connect(dbPath)
    con.row_factory = sqlite3.Row
    #con.execute('PRAGMA foreign_keys = ON;')
    con.commit()
    return con

creation_script = """
CREATE TABLE Commodity (
    Id INTEGER NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL DEFAULT('Commodity ' || CURRENT_TIMESTAMP),
    Position INTEGER UNIQUE CHECK(Position IS NULL OR Position >= 0),
    Cost REAL NOT NULL DEFAULT(0.0) CHECK(Cost >= 0.0),
    WholePrice REAL NOT NULL DEFAULT(0.0) CHECK(WholePrice >= 0.0),
    PartialPrice REAL NOT NULL DEFAULT(0.0) CHECK(PartialPrice >= 0.0),
    CashPrice REAL NOT NULL DEFAULT(0.0) CHECK(CashPrice >= 0.0),
    IsExported BOOLEAN NOT NULL DEFAULT(FALSE)
);
CREATE TABLE CImage (
    Id INTEGER NOT NULL PRIMARY KEY,
    Contrast REAL NOT NULL DEFAULT(1.0) CHECK(Contrast >= 0.0),
    Brightness REAL NOT NULL DEFAULT(1.0) CHECK(Brightness >= 0.0),
    IsExported BOOLEAN NOT NULL DEFAULT(FALSE)
);
CREATE TABLE ImageCommodity (
    Id INTEGER NOT NULL PRIMARY KEY REFERENCES Commodity(Id) ON UPDATE CASCADE,
    ImageId INTEGER NOT NULL REFERENCES CImage(Id) ON UPDATE CASCADE,
    FontFamilyName TEXT NOT NULL DEFAULT('Arial'),
    FontStyle INTEGER NOT NULL DEFAULT(0) CHECK(FontStyle >= 0 AND FontStyle <= 3),
    FontSize REAL NOT NULL DEFAULT(100) CHECK(FontSize > 0.0),
    LocationX REAL NOT NULL DEFAULT(0.0) CHECK(LocationX >= 0.0),
    LocationY REAL NOT NULL DEFAULT(0.0) CHECK(LocationY >= 0.0),
    LabelColor TEXT NOT NULL DEFAULT('White')
);
CREATE INDEX IDX_ImageCommodity_ImageId ON ImageCommodity (ImageId);
"""
oldDb, newDb = r"C:\Users\abdal\Desktop\RepoFiles\NewRepo\db000.sqlite", r"C:\Users\abdal\Desktop\RepoFiles\NewRepo\db111.sqlite"
tables = ['CImage', 'Commodity', 'ImageCommodity']
oldCon, newCon = get_connection(oldDb), get_connection(newDb)
newCon.executescript(creation_script)
newCon.commit()
for t in tables:
    x = copy_table(oldCon, newCon, t)
    print(f'Copied {x} rows to table {t}')