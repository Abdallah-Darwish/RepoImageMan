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
        /// <summary>
        /// A lock on <see cref="_commodities"/> since creating, loading and removing commodities is done concurrently.
        /// </summary>
        private readonly SemaphoreSlim _commoditiesLock = new SemaphoreSlim(1);

        /// <summary>
        /// A lock on <see cref="_images"/> since creating, loading and removing images is done concurrently.
        /// </summary>
        private readonly SemaphoreSlim _imagesLock = new SemaphoreSlim(1);

        internal readonly SemaphoreSlim _imageRepositinningLock = new SemaphoreSlim(1);
        private readonly string _dbPath;
        private readonly string ConnectionString;

        public string PackageDirectoryPath { get; }

        internal SQLiteConnection GetConnection()
        {
            var con = new SQLiteConnection(ConnectionString);
            con.Execute("PRAGMA foreign_keys = ON;");
            return con;
        }
        private readonly Stream _lck;
        internal CommodityPackage(string packageDirectoryPath, Stream pkgLock)
        {
            _lck = pkgLock;
            PackageDirectoryPath = packageDirectoryPath;
            _dbPath = GetPackageDbPath(PackageDirectoryPath);
            ConnectionString = GetConnectionString(_dbPath);
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
            await using var con = GetConnection();
            await con.OpenAsync().ConfigureAwait(false);
            await con.ExecuteAsync("INSERT INTO CImage DEFAULT VALUES;").ConfigureAwait(false);
            int newImageId = (int)con.LastInsertRowId;
            await File.Create(CImage.GetCImagePackageFilePath(this, newImageId)).DisposeAsync().ConfigureAwait(false);
            var newImage = await CImage.Load(newImageId, this).ConfigureAwait(false);
            await _imagesLock.WaitAsync().ConfigureAwait(false);
            _images.Add(newImage);
            _imagesLock.Release();


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
            await _imagesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                File.Delete(image.PackageFilePath);
                _images.Remove(image);
            }
            finally
            {
                _imagesLock.Release();
            }

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
            await using var con = GetConnection();
            await con.OpenAsync().ConfigureAwait(false);
            await con.ExecuteAsync("INSERT INTO Commodity(Position) VALUES((COALESCE((SELECT MAX(Position) FROM Commodity), 0) + 1));").ConfigureAwait(false);

            var newCom = await Commodity.Load((int)con.LastInsertRowId, this).ConfigureAwait(false);
            await _commoditiesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _commodities.Add(newCom);
            }
            finally
            {
                _commoditiesLock.Release();
            }

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
            await _commoditiesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _commodities.Remove(com);
            }
            finally
            {
                _commoditiesLock.Release();
            }

            CommodityRemoved?.Invoke(this, com);
        }

        /// <summary>
        /// To be called only by <see cref="CImage.AddCommodity"/> to add the new <see cref="ImageCommodity"/> to <see cref="Commodities"/> and raise corresponding events. 
        /// </summary>
        /// <param name="com">The newly created <see cref="ImageCommodity"/>.</param>
        internal async Task AddImageCommodity(ImageCommodity com)
        {
            await _commoditiesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _commodities.Add(com);
            }
            finally
            {
                _commoditiesLock.Release();
            }

            CommodityAdded?.Invoke(this, com);
        }

        public async Task Tidy()
        {
            using var con = GetConnection();

            await con.OpenAsync().ConfigureAwait(false);
            using var trans = await con.BeginTransactionAsync().ConfigureAwait(false);

            await _imagesLock.WaitAsync().ConfigureAwait(false);
            await _commoditiesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var orderedImages = Images.OrderBy(i => i.Commodities.Count > 0 ? i.Commodities.Max(c => c.Position) : int.MaxValue).ToArray();
                if (Images.Count > 0)
                {
                    var maxImgId = await con.ExecuteScalarAsync<int>("SELECT MAX(Id) FROM CImage;").ConfigureAwait(false) + 5;
                    foreach (var img in orderedImages)
                    {
                        var newImgPath = CImage.GetCImagePackageFilePath(PackageDirectoryPath, maxImgId);
                        if (File.Exists(newImgPath))
                        {
                            File.Delete(newImgPath);
                        }
                        await img.Tidy(maxImgId++, con).ConfigureAwait(false);
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
                        await img.Tidy(pos++, con).ConfigureAwait(false);
                    }
                }
                if (Commodities.Count > 0)
                {
                    var orderedCommodites = Commodities.OrderBy(c => c.Position).ToArray();

                    ///Shift all of the commodities to end to skip additional work inside <see cref="Commodity.SetPosition(int)"/> when setting final position.
                    int pos = await con.ExecuteScalarAsync<int>("SELECT MAX(Position) FROM Commodity;").ConfigureAwait(false) + 1;
                    foreach (var com in orderedCommodites)
                    {
                        await com.ChangePosition(pos++, con).ConfigureAwait(false);
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
                                await c.ChangePosition(pos++, con).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await com.ChangePosition(pos++, con).ConfigureAwait(false);
                        }
                    }
                    //Shift all of the commodities Ids to end then back to where they belong
                    pos = await con.ExecuteScalarAsync<int>("SELECT MAX(Id) FROM Commodity;").ConfigureAwait(false) + 1;
                    foreach (var com in Commodities)
                    {
                        await com.Tidy(pos++, con).ConfigureAwait(false);
                    }
                    foreach (var com in Commodities)
                    {
                        await com.Tidy(com.Position, con).ConfigureAwait(false);
                    }
                }

                await trans.CommitAsync().ConfigureAwait(false);
            }
            finally
            {
                _imagesLock.Release();
                _commoditiesLock.Release();
            }
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
                _commoditiesLock.Dispose();
                _imagesLock.Dispose();
                _imageRepositinningLock.Dispose();
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