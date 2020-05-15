using Dapper;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.Primitives;

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

        private readonly ConcurrentDictionary<Type, object> _labelsCaches = new ConcurrentDictionary<Type, object>();


        private readonly string _dbPath;
        internal readonly string _packageDirectoryPath;
        private readonly DirectoryInfo _packageDirectory;
        private readonly string ConnectionString;

        internal SQLiteConnection GetConnection()
        {
            var con = new SQLiteConnection(ConnectionString);
            con.Execute(@"PRAGMA foreign_keys = ON");
            return con;
        }
        private readonly Stream _lck;
        internal CommodityPackage(string packageDirectoryPath, Stream pkgLock)
        {
            _lck = pkgLock;
            _packageDirectoryPath = packageDirectoryPath;
            _dbPath = GetPackageDbPath(_packageDirectoryPath);
            if (File.Exists(_dbPath) == false)
            {
                throw new FileNotFoundException(
$@"Can't find package Database.
Expected Database path is {_dbPath}.");
            }
            ConnectionString = GetConnectionString(_dbPath);
            _packageDirectory = new DirectoryInfo(_packageDirectoryPath);
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
                _labelsCaches.Clear();
                _commoditiesLock.Dispose();
                _imagesLock.Dispose();
                _lck.Dispose();
                File.Delete(Path.Combine(_packageDirectoryPath, LockName));
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