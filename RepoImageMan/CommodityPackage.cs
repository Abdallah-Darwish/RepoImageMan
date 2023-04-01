using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RepoImageMan
{
    //TODO: Add logging
    /// <summary>
    /// Expects a a folder that coontaines a file named <see cref="CommodityPackage.DbName"/>.
    /// </summary>
    public sealed partial class CommodityPackage : IDisposable
    {
        public SQLiteConnection DbConnection { get; }
        private readonly string _dbPath;
        private readonly string ConnectionString;

        public string PackageDirectoryPath { get; }
        private readonly Stream _lck;
        internal CommodityPackage(string packageDirectoryPath, Stream pkgLock)
        {
            _lck = pkgLock;
            PackageDirectoryPath = packageDirectoryPath;
            _dbPath = GetPackageDbPath(PackageDirectoryPath);
            ConnectionString = GetConnectionString(_dbPath);
            DbConnection = new SQLiteConnection(ConnectionString);
            DbConnection.Execute("PRAGMA foreign_keys = ON;");
            DbConnection.Open();
        }

        private readonly List<Commodity> _commodities = new List<Commodity>();
        public IReadOnlyList<Commodity> Commodities => _commodities;

        private readonly List<CImage> _images = new List<CImage>();
        public IReadOnlyList<CImage> Images => _images;

        public delegate void ImageModifiedEventHandler(CommodityPackage sender, CImage image);

        public event ImageModifiedEventHandler? ImageAdded;

        /// <summary>
        /// Will create an image with all values set to default and you can initialize it then call <see cref="CImage.Save"/>.
        /// </summary>
        public async Task<CImage> AddImage()
        {
            await DbConnection.ExecuteAsync("INSERT INTO CImage DEFAULT VALUES;").ConfigureAwait(false);
            int newImageId = (int)DbConnection.LastInsertRowId;
            await File.Create(CImage.GetCImagePackageFilePath(this, newImageId)).DisposeAsync().ConfigureAwait(false);
            var newImage = await CImage.Load(newImageId, this).ConfigureAwait(false);
            _images.Add(newImage);
            ImageAdded?.Invoke(this, newImage);
            return newImage;
        }

        /// <summary>
        /// Will be raised when an image is about to be deleted from this <see cref="CommodityPackage"/>.
        /// </summary>
        public event ImageModifiedEventHandler? ImageRemoved;

        /// <summary>
        /// To be called only by <see cref="CImage.Delete"/> to remove it from my list and raise corresponding events.
        /// </summary>
        /// <param name="image">The image to delete.</param>
        internal async Task RemoveImage(CImage image)
        {
            File.Delete(image.PackageFilePath);
            _images.Remove(image);
            ImageRemoved?.Invoke(this, image);
        }

        public delegate void CommodityModifiedEventHandler(CommodityPackage sender, Commodity com);

        /// <summary>
        /// Will be raised when a new <see cref="Commodity"/>(or <see cref="ImageCommodity"/>) is added to this <see cref="CommodityPackage"/>.
        /// </summary>
        public event CommodityModifiedEventHandler? CommodityAdded;

        /// <summary>
        /// Will create a commodity with all values set to default and you can initialize it then call <see cref="Commodity.Save"/>.
        /// </summary>
        public async Task<Commodity> AddCommodity()
        {
            await DbConnection.ExecuteAsync("INSERT INTO Commodity(Position) VALUES((COALESCE((SELECT MAX(Position) FROM Commodity), 0) + 1));").ConfigureAwait(false);

            var newCom = await Commodity.Load((int)DbConnection.LastInsertRowId, this).ConfigureAwait(false);
            _commodities.Add(newCom);
            CommodityAdded?.Invoke(this, newCom);
            return newCom;
        }

        /// <summary>
        /// Will be raised when a <see cref="Commodity"/>(or <see cref="ImageCommodity"/>) is deleted from this <see cref="CommodityPackage"/>.
        /// </summary>
        public event CommodityModifiedEventHandler? CommodityRemoved;

        /// <summary>
        /// To be called only by <see cref="Commodity.Delete"/> to remove it from <see cref="Commodities"/> and raise corresponding events. 
        /// </summary>
        /// <param name="com">The <see cref="Commodity"/> to delete.</param>
        internal async Task RemoveCommodity(Commodity com)
        {
            _commodities.Remove(com);
            CommodityRemoved?.Invoke(this, com);
        }

        /// <summary>
        /// To be called only by <see cref="CImage.AddCommodity"/> to add the new <see cref="ImageCommodity"/> to <see cref="Commodities"/> and raise corresponding events. 
        /// </summary>
        /// <param name="com">The newly created <see cref="ImageCommodity"/>.</param>
        internal async Task AddImageCommodity(ImageCommodity com)
        {
            _commodities.Add(com);
            CommodityAdded?.Invoke(this, com);
        }

        public async Task Tidy()
        {
            using var trans = await DbConnection.BeginTransactionAsync().ConfigureAwait(false);


            var orderedImages = Images.OrderBy(i => i.Commodities.Count > 0 ? i.Commodities.Max(c => c.Position) : int.MaxValue).ToArray();
            if (Images.Count > 0)
            {
                var maxImgId = await DbConnection.ExecuteScalarAsync<int>("SELECT MAX(Id) FROM CImage;").ConfigureAwait(false) + 5;
                foreach (var img in orderedImages)
                {
                    var newImgPath = CImage.GetCImagePackageFilePath(PackageDirectoryPath, maxImgId);
                    if (File.Exists(newImgPath))
                    {
                        File.Delete(newImgPath);
                    }
                    await img.Tidy(maxImgId++).ConfigureAwait(false);
                }
                var pkgFiles = Images.Select(i => i.PackageFileName).ToHashSet();
                pkgFiles.Add(CommodityPackage.DbName);
                pkgFiles.Add(CommodityPackage.LockName);
                pkgFiles.Add(Path.ChangeExtension(CommodityPackage.DbName, ".sqlite-journal"));
                foreach (var fileName in Directory.GetFiles(PackageDirectoryPath))
                {
                    if (!pkgFiles.Contains(Path.GetFileName(fileName)))
                    {
                        File.Delete(fileName);
                    }
                }
                var pos = 0;
                foreach (var img in orderedImages)
                {
                    await img.Tidy(pos++).ConfigureAwait(false);
                }
            }
            if (Commodities.Count > 0)
            {
                var orderedCommodites = Commodities.OrderBy(c => c.Position).ToArray();

                ///Shift all of the commodities to end to skip additional work inside <see cref="Commodity.SetPosition(int)"/> when setting final position.
                int pos = await DbConnection.ExecuteScalarAsync<int>("SELECT MAX(Position) FROM Commodity;").ConfigureAwait(false) + 1;
                foreach (var com in orderedCommodites)
                {
                    await com.ChangePositionInDb(pos++).ConfigureAwait(false);
                }

                //contains ids of images that we already processed their commodities
                var processedImages = new HashSet<int>();
                pos = 0;
                foreach (var com in orderedCommodites)
                {
                    if (com is ImageCommodity imgCom)
                    {
                        //sort this image commodities only if this is the first commodity
                        var img = imgCom.Image;
                        if (!processedImages.Add(img.Id)) { continue; }
                        foreach (var c in img.Commodities.OrderBy(c => c.Position))
                        {
                            await c.ChangePositionInDb(pos++).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await com.ChangePositionInDb(pos++).ConfigureAwait(false);
                    }
                }
                //Shift all of the commodities Ids to end then back to where they belong
                pos = await DbConnection.ExecuteScalarAsync<int>("SELECT MAX(Id) FROM Commodity;").ConfigureAwait(false) + 1;
                foreach (var com in Commodities)
                {
                    await com.Tidy(pos++).ConfigureAwait(false);
                }
                foreach (var com in Commodities)
                {
                    await com.Tidy(com.Position).ConfigureAwait(false);
                }
            }

            await trans.CommitAsync().ConfigureAwait(false);
        }
        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        private void DisposeImpl()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                CommodityAdded = null;
                CommodityRemoved = null;
                ImageAdded = null;
                ImageRemoved = null;
                foreach (var img in Images)
                {
                    img.Dispose();
                }

                foreach (var com in Commodities)
                {
                    com.Dispose();
                }

                _images.Clear();
                _commodities.Clear();
                _lck.Dispose();
                File.Delete(Path.Combine(PackageDirectoryPath, LockName));
            }
        }

        ~CommodityPackage() => DisposeImpl();

        public void Dispose()
        {
            DisposeImpl();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}