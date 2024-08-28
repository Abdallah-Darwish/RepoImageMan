using Dapper;
using System.Data.SQLite;
using System.Collections.Immutable;

const string dbPath = @"C:\Users\basel\OneDrive\Desktop\db000.sqlite";
var conString = $"Data Source={dbPath};Version=3;foreign keys=True;";
await using var con = new SQLiteConnection(conString);
await con.OpenAsync();
con.Execute("PRAGMA foreign_keys = ON;");
var images = (await con.QueryAsync<Image>("""
SELECT
    i.Id,
    MIN(c.Position) AS Position
FROM
    CImage i
INNER JOIN
    ImageCommodity ic ON i.id = ic.imageId
INNER JOIN
    Commodity c ON c.id = ic.id
GROUP BY
    i.id;
""")).ToArray();
var imageMap = images.ToImmutableDictionary(i => i.Id);
var commodities = (await con.QueryAsync<Commodity>("""
SELECT
    c.id,
    c.position,
    ic.imageId,
    ic.LocationX,
    ic.LocationY
FROM
    Commodity c
LEFT JOIN
    ImageCommodity ic USING(id);
""")).OrderBy(c => c.ImageId.HasValue ? imageMap[c.ImageId.Value].Position: int.MaxValue)
    .ThenBy(c => c.LocationX ?? int.MaxValue)
    .ThenBy(c => c.LocationY ?? int.MaxValue)
    .ThenBy(c => c.Position)
    .ToArray();
int pos = 0;
foreach(var com in commodities)
{
    com.Position = pos++;
}
await con.ExecuteAsync("""
UPDATE
    Commodity
SET
    position = NULL;
""");
foreach(var com in commodities)
{
    await com.Update(con);
}
await con.CloseAsync();

record class Commodity
{
    public int Id {get; set;}
    public int? ImageId {get; set;}
    public int Position {get; set;}
    public double? LocationX {get; set;}
    public double? LocationY {get; set;}
    public async Task Update(SQLiteConnection con)
    {
        await con.ExecuteAsync("""
        UPDATE
            Commodity
        SET
            position = @newPosition
        WHERE
            id = @id;
        """, new{id = Id, newPosition = Position});
    }
}

record class Image
{
    public int Id {get;set;}
    public int Position {get;set;}
}