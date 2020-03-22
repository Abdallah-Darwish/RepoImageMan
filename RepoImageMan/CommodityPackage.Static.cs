using System;
using Dapper;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.PixelFormats;

namespace RepoImageMan
{
    public sealed partial class CommodityPackage
    {
        public const string DbExtension = "sqlite", DbName = "db000.sqlite";
        private static string GetConnectionString(string dbPath) => $"Data Source={dbPath};Version=3;";

        internal static string GetPackageDbPath(string packageDirectoryPath) => Path.Combine(packageDirectoryPath, DbName);
        public static async Task<CommodityPackage> Open(string packageDirectoryPath, Image? handleImage = null)
        {
            handleImage ??= new Image<Rgba32>(1, 1);
            var res = new CommodityPackage(packageDirectoryPath, handleImage);
            await using var con = res.GetConnection();
            var nonImageCommoditiesIds = await con.QueryAsync<int>(
@"SELECT c.id FROM Commodity c
LEFT JOIN ImageCommodity ic
ON c.id = ic.id
WHERE ic.id IS NULL;").ConfigureAwait(false);
            var readingTasks = new List<Task>();
            //This could be developed further by using a real Parallel.AsyncForEach
            foreach (var comId in nonImageCommoditiesIds)
            {
                readingTasks.Add(Commodity.Load(comId, res).ContinueWith(ca =>
                {
                    res._commoditiesLock.Wait();
                    res._commodities.Add(ca.Result);
                    res._commoditiesLock.Release();
                }, TaskContinuationOptions.OnlyOnRanToCompletion));
            }

            var imagesIds = await con.QueryAsync<int>("SELECT id FROM CImage;").ConfigureAwait(false);
            foreach (var imgId in imagesIds)
            {
                readingTasks.Add(CImage.Load(imgId, res).ContinueWith(ca =>
                {
                    res._imagesLock.Wait();
                    res._images.Add(ca.Result);
                    res._imagesLock.Release();
                }, TaskContinuationOptions.OnlyOnRanToCompletion));
            }

            await Task.WhenAll(readingTasks).ConfigureAwait(false);
            return res;
        }

        public static async Task Create(string packageContainerPath)
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
	Brightness REAL NOT NULL DEFAULT(1.0) CHECK(Brightness >= 0.0)
);
CREATE TABLE ImageCommodity (
	Id INTEGER NOT NULL PRIMARY KEY REFERENCES Commodity(Id),
	ImageId INTEGER NOT NULL REFERENCES CImage(Id),
	FontFamilyName TEXT NOT NULL DEFAULT('Arial'),
	FontStyle INTEGER NOT NULL DEFAULT(0) CHECK(FontStyle >= 0 AND FontStyle <= 3),
	FontSize REAL NOT NULL DEFAULT(100) CHECK(FontSize > 0.0),
	LocationX REAL NOT NULL DEFAULT(0.0) CHECK(LocationX >= 0.0),
	LocationY REAL NOT NULL DEFAULT(0.0) CHECK(LocationY >= 0.0),
	LabelColor TEXT NOT NULL DEFAULT('FFFFFFFF'),
	IsPositionHolder BOOLEAN NOT NULL DEFAULT(0)
);
CREATE INDEX IDX_ImageCommodity_ImageId ON ImageCommodity (ImageId);
";
            string dbPath = GetPackageDbPath(packageContainerPath);
            SQLiteConnection.CreateFile(dbPath);
            await using (var con = new SQLiteConnection(GetConnectionString(dbPath)))
            await using (var archiveStream =
                new FileStream(packageContainerPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (new ZipArchive(archiveStream, ZipArchiveMode.Create))
            {
                await con.ExecuteAsync(CreationCommand).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deletes the package and all of its files, the returned package will be disposed.
        /// </summary>
        /// <param name="package">The package to delete.</param>
        public static void Delete(CommodityPackage package)
        {
            package.Dispose();
            File.Delete(package._dbPath);
            File.Delete(package._packageDirectoryPath);
        }
    }
}