using Dapper;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
namespace RepoImageMan
{
    public sealed partial class CommodityPackage
    {
        private static string GetConnectionString(string dbPath) => $"Data Source={dbPath};Version=3;";
        public static async Task<CommodityPackage> Open(string dbPath, string packagePath)
        {
            var conString = GetConnectionString(dbPath);
            var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Update);
            var res = new CommodityPackage(conString, packageArchive);
            await using var con = res.GetConnection();
            var nonImageCommoditiesIds = await con.QueryAsync<int>(
@"SELECT c.id FROM Commodity c
LEFT JOIN ImageCommodity ic
ON c.id = ic.id
WHERE ic.id IS NULL;").ConfigureAwait(false);
            var readingTasks = new List<Task>();
            //TODO: lock on the commoditiesLock
            foreach (var comId in nonImageCommoditiesIds)
            {
                readingTasks.Add(Commodity.Load(comId, res).ContinueWith(ca => res._commodities.Add(ca.Result), TaskContinuationOptions.OnlyOnRanToCompletion));
            }

            var imagesIds = await con.QueryAsync<int>("SELECT id FROM CImage;").ConfigureAwait(false);
            foreach (var imgId in imagesIds)
            {
                readingTasks.Add(CImage.Load(imgId, res).ContinueWith(ca => res._images.Add(ca.Result), TaskContinuationOptions.OnlyOnRanToCompletion));
            }
            await Task.WhenAll(readingTasks).ConfigureAwait(false);
            return res;
        }
        public static async Task<CommodityPackage> Create(string dbPath, string packagePath)
        {
            const string CreationCommand =
@"CREATE TABLE Commodity (
	Id INTEGER NOT NULL PRIMARY KEY,
	Name TEXT NOT NULL DEFAULT('Commodity ' || CURRENT_TIMESTAMP),
	Position INTEGER UNIQUE,
	Cost REAL NOT NULL DEFAULT(0.0) CHECK(Cost >= 0.0),
	WholePrice REAL NOT NULL DEFAULT(0.0) CHECK(WholePrice >= 0.0),
	PartialPrice REAL NOT NULL DEFAULT(0.0) CHECK(PartialPrice >= 0.0)
);
CREATE TABLE CImage (
	Id INTEGER NOT NULL PRIMARY KEY,
	Contrast REAL NOT NULL DEFAULT(1.0) CHECK(Contrast >= 0.0),
	Brightness REAL NOT NULL DEFAULT(1.0) CHECK(Brightness >= 0.0),
	SizeRatio REAL NOT NULL DEFAULT(1.0) CHECK(SizeRatio > 0.0)
);
CREATE TABLE ImageCommodity (
	Id INTEGER NOT NULL PRIMARY KEY REFERENCES Commodity(Id),
	ImageId INTEGER NOT NULL REFERENCES CImage(Id),
	FontFamilyName TEXT NOT NULL DEFAULT('Arial'),
	FontStyle INTEGER NOT NULL DEFAULT(0) CHECK(FontStyle >= 0 AND FontStyle <= 3),
	FontSize REAL NOT NULL DEFAULT(100) CHECK(FontSize > 0.0),
	LocationX REAL NOT NULL DEFAULT(0.0) CHECK(LocationX >= 0.0),
	LocationY REAL NOT NULL DEFAULT(0.0) CHECK(LocationY >= 0.0),
	LabelColor TEXT NOT NULL DEFAULT('FFFFFFFF')
);";
            SQLiteConnection.CreateFile(dbPath);
            await using (var con = new SQLiteConnection(GetConnectionString(dbPath)))
            using (var archiveStream = new FileStream(packagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
            {
                await con.ExecuteAsync(CreationCommand).ConfigureAwait(false);
            }
            return await Open(dbPath, packagePath).ConfigureAwait(false);

        }
    }
}
