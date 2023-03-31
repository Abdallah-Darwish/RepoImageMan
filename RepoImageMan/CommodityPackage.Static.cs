using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SixLabors.ImageSharp;

namespace RepoImageMan
{
    public sealed partial class CommodityPackage
    {
        public const string DbExtension = "sqlite", DbName = "db000.sqlite", LockName = "pkg000.lckxy";
        private static string GetConnectionString(string dbPath) => $"Data Source={dbPath};Version=3;foreign keys=True;";
        public static string GetPackageLockPath(string packageDirectoryPath) => Path.Combine(packageDirectoryPath, LockName);
        private static string GetPackageDbPath(string packageDirectoryPath) => Path.Combine(packageDirectoryPath, DbName);
        internal static async Task VerifyPackage(string pd)
        {
            try
            {
                if (!Directory.Exists(pd))
                {
                    throw new PackageCorruptException("Package folder doesn't exist.");
                }
                if (!File.Exists(GetPackageDbPath(pd)))
                {
                    throw new PackageCorruptException(
     $@"Can't find package Database.
Expected Database path is {GetPackageDbPath(pd)}.");
                }
                await using var con = new SQLiteConnection(GetConnectionString(GetPackageDbPath(pd)));
                var images = (await con.QueryAsync("SELECT * FROM CImage;").ConfigureAwait(false)).ToArray();

                var systemFontsNames = Avalonia.Media.FontManager.Current.GetInstalledFontFamilyNames().Select(f => f.ToUpperInvariant()).ToHashSet();
                foreach (var img in images)
                {
                    if (img.Contrast < 0)
                    {
                        throw new PackageCorruptException($"Image(Id: {img.Id}) has invalid contrast");
                    }
                    if (img.Brightness < 0)
                    {
                        throw new PackageCorruptException($"Image(Id :{img.Id}) has invalid brightness");
                    }
                    var imgComs = (await con.QueryAsync("SELECT * FROM ImageCommodity WHERE imageId = @imageId;",
                        new { imageId = img.Id }).ConfigureAwait(false)).ToArray();
                    var imgPath = CImage.GetCImagePackageFilePath(pd, (int)img.Id);
                    if (!File.Exists(imgPath))
                    {
                        throw new PackageCorruptException($"Image(Id: {img.Id}) file doesn't exist.");
                    }
                    ImageInfo? imgInfo = null;
                    using (var imgStream = File.OpenRead(imgPath))
                    {
                        try
                        {
                            imgInfo = Image.Identify(imgStream);
                        }
                        catch { }
                    }
                    if (imgInfo is null) { throw new PackageCorruptException($"Image(id: {img.Id}) isn't a valid image."); }
                    foreach (var com in imgComs)
                    {
                        if (com.LocationX < 0 || com.LocationX > imgInfo.Width || com.LocationY < 0 || com.LoactionY > imgInfo.Height)
                        {
                            throw new PackageCorruptException($"Commodity(Id: {com.Id}) coordinates is out of bounds.");
                        }
                        if (com.FontSize <= 0)
                        {
                            throw new PackageCorruptException($"Commodity(Id: {com.Id}) font size is <= 0.");
                        }
                        if (com.FontStyle < 0 || com.FontStyle > 3)
                        {
                            throw new PackageCorruptException($"Commodity(Id: {com.Id}) has invalid style.");
                        }
                        try
                        {
                            Avalonia.Media.Color.Parse(com.LabelColor);
                        }
                        catch
                        {
                            throw new PackageCorruptException($"Commodity(id: {com.Id}) has invalid color.");
                        }
                        if (!systemFontsNames.Contains((com.FontFamilyName as string)!.ToUpperInvariant()))
                        {
                            throw new PackageCorruptException($"Commodity(Id: {com.Id}) has a non-existing font.");
                        }
                    }
                }
                var coms = (await con.QueryAsync("SELECT * FROM Commodity;").ConfigureAwait(false)).ToArray();
                var comsPositions = new HashSet<int>();
                foreach (var com in coms)
                {
                    if (com.Cost < 0)
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) cost is < 0.");
                    }
                    if (com.WholePrice < 0)
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) whole price is < 0.");
                    }
                    if (com.PartialPrice < 0)
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) partial price is < 0.");
                    }
                    if (com.CashPrice < 0)
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) cash price is < 0.");
                    }
                    if (string.IsNullOrWhiteSpace(com.Name))
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) name is empty or invalid.");
                    }
                    if (com.Position < 0)
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) position is < 0.");
                    }
                    if (com.Position is null)
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) position is null.");
                    }
                    if (!comsPositions.Add((int)com.Position))
                    {
                        throw new PackageCorruptException($"Commodity(Id: {com.Id}) position is duplicate.");
                    }
                }
            }
            catch (PackageCorruptException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PackageCorruptException("Some error occured while validating the package.", ex);
            }
        }
        public static async Task<CommodityPackage?> TryOpen(string packageDirectoryPath)
        {
            await VerifyPackage(packageDirectoryPath).ConfigureAwait(false);
            //created lock as a stream so other apps can't delete it
            FileStream? lck = null;
            try
            {
                lck = new FileStream(GetPackageLockPath(packageDirectoryPath), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when (ex.Message.Contains("exists", StringComparison.OrdinalIgnoreCase))
            {
                lck?.Dispose();
                return null;
            }
            var res = new CommodityPackage(packageDirectoryPath, lck);
            await using var con = res.GetConnection();
            var nonImageCommoditiesIds = await con.QueryAsync<int>(
@"SELECT c.id FROM Commodity c
LEFT JOIN ImageCommodity ic
ON c.id = ic.id
WHERE ic.id IS NULL;").ConfigureAwait(false);
            await nonImageCommoditiesIds.ForEachAsync(async cid =>
            {
                var com = await Commodity.Load(cid, res).ConfigureAwait(false);
                await res._commoditiesLock.WaitAsync().ConfigureAwait(false);
                res._commodities.Add(com);
                res._commoditiesLock.Release();
            }).ConfigureAwait(false);



            var imagesIds = await con.QueryAsync<int>("SELECT id FROM CImage;").ConfigureAwait(false);
            await imagesIds.ForEachAsync(async iid =>
            {
                var img = await CImage.Load(iid, res).ConfigureAwait(false);
                await res._imagesLock.WaitAsync().ConfigureAwait(false);
                res._images.Add(img);
                res._imagesLock.Release();
            }).ConfigureAwait(false);
            return res;
        }

        public static async Task Create(string packageContainerPath)
        {
            const string CreationCommand =
@"CREATE TABLE Commodity (
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
";
            string dbPath = GetPackageDbPath(packageContainerPath);
            SQLiteConnection.CreateFile(dbPath);
            await using var con = new SQLiteConnection(GetConnectionString(dbPath));
            await con.ExecuteAsync(CreationCommand).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the package and all of its files, the returned package will be disposed.
        /// </summary>
        /// <param name="package">The package to delete.</param>
        public static void Delete(CommodityPackage package)
        {
            package.Dispose();
            File.Delete(package._dbPath);
            Directory.Delete(package.PackageDirectoryPath);
        }
    }
}