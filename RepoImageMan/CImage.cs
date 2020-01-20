using Dapper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RepoImageMan
{
    public sealed class CImage : IDisposable, INotifyPropertyChanged, INotifySpecificPropertyChanged
    {
        /// <summary>
        /// Name of the image entry(or file) inside <see cref="Package"/> archive.
        /// </summary>
        public string PackageEntryName => $"{Id}.jpg";

        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly NotificationManager _propertyNotificationManager;
        public INotificationManager PropertyNotificationManager => _propertyNotificationManager;

        /// <summary>
        /// Dimenssions of the image.
        /// </summary>
        public Size Size { get; private set; }

        private void OnPropertyChanged(string propName)
        {
            _propertyNotificationManager.OnPropertyChanged(propName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private CImage(int id, CommodityPackage package)
        {
            _propertyNotificationManager = new NotificationManager(this);
            Package = package;
            Id = id;
        }

        /// <summary>
        /// Loads an image from the package and all of its commodities correctly.
        /// </summary>
        /// <param name="id">Id of the image to load.</param>
        /// <param name="package">The package to load the image from.</param>
        internal static async Task<CImage> Load(int id, CommodityPackage package)
        {
            var res = new CImage(id, package);
            await res.Reload().ConfigureAwait(false);
            res.Refresh();
            await using var con = package.GetConnection();


            var comsIds = (await con.QueryAsync<int>("SELECT id FROM ImageCommodity WHERE imageId = @id", new {id})
                .ConfigureAwait(false)).AsList();
            res._commodities.Capacity = comsIds.Count;
            foreach (var comId in comsIds)
            {
                var com = await ImageCommodity.Load(comId, package, res).ConfigureAwait(false);
                await package.AddImageCommodity(com).ConfigureAwait(false);
                res._commodities.Add(com);
            }

            return res;
        }

        /// <summary>
        /// The <see cref="CommodityPackage"/> that this <see cref="CImage"/> belongs to.
        /// </summary>
        public CommodityPackage Package { get; }

        /// <summary>
        /// Id of the image inside the package.
        /// Unique per <see cref="CommodityPackage"/> but might be repeated accross packages.
        /// </summary>
        public int Id { get; }

        private float _contrast;

        /// <summary>
        /// Contrast or brightness of te final image.
        /// </summary>
        public float Contrast
        {
            get => _contrast;
            set
            {
                if (value < 0.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(Contrast)} can't < 0.");
                }

                if (value == _contrast)
                {
                    return;
                }

                _contrast = value;
                OnPropertyChanged(nameof(Contrast));
            }
        }

        private float _brightness;

        /// <summary>
        /// Contrast or brightness of te final image.
        /// </summary>
        public float Brightness
        {
            get => _brightness;
            set
            {
                if (value < 0.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(Brightness)} can't < 0.");
                }

                if (value == _brightness)
                {
                    return;
                }

                _brightness = value;
                OnPropertyChanged(nameof(Brightness));
            }
        }

        private readonly List<ImageCommodity> _commodities = new List<ImageCommodity>();

        /// <summary>
        /// List of commodities that will be rendered on this image.
        /// </summary>
        public IReadOnlyList<ImageCommodity> Commodities => _commodities;


        public delegate void CommodityModifiedEventHandler(CImage sender, ImageCommodity commodity);

        /// <summary>
        /// Will be raised when a new <see cref="ImageCommodity"/> that belongs to this <see cref="CImage"/> is created in <see cref="Package"/>.
        /// </summary>
        public event CommodityModifiedEventHandler? CommodityAdded;

        /// <summary>
        /// CREATES a new <see cref="ImageCommodity"/> and adds it to this <see cref="CImage"/>.
        /// This action can't be reverted by calling <see cref="Reload"/>.
        /// <see cref="CommodityAdded"/> will be raised before returning.
        /// </summary>
        public async Task<ImageCommodity> AddCommodity()
        {
            await using var con = Package.GetConnection();
            await con.OpenAsync().ConfigureAwait(false);
            await con.ExecuteAsync(
                    @"INSERT INTO Commodity(position) VALUES((COALESCE((SELECT MAX(Position) FROM Commodity), 0) + 1));")
                .ConfigureAwait(false);
            await con.ExecuteAsync(@"INSERT INTO ImageCommodity(id, imageId) VALUES(@id, @imageId);",
                new {id = (int) con.LastInsertRowId, imageId = Id}).ConfigureAwait(false);

            var newCom = await ImageCommodity.Load((int) con.LastInsertRowId, Package, this).ConfigureAwait(false);
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
        /// Because loading, removing commodities is mostly done concurrently we have to guard <see cref="_commodities"/>.
        /// </summary>
        readonly SemaphoreSlim _commoditiesLock = new SemaphoreSlim(1);

        /// <summary>
        /// Will be raised when a <see cref="ImageCommodity"/> that belongs to this <see cref="CImage"/> is deleted from <see cref="Package"/>.
        /// </summary>
        public event CommodityModifiedEventHandler? CommodityRemoved;

        internal async Task RemoveCommodity(ImageCommodity com)
        {
            await _commoditiesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _commodities.Remove(com);
                CommodityRemoved?.Invoke(this, com);
            }
            finally
            {
                _commoditiesLock.Release();
            }
        }

        /// <summary>
        /// Saves the properties of this instance in <see cref="Package"/> database. 
        /// Doesn't affect the commodities
        /// </summary>
        public async Task Save()
        {
            await using var con = Package.GetConnection();
            await con.ExecuteAsync("UPDATE CImage SET contrast = @Contrast, brightness = @Brightness WHERE id = @Id",
                this).ConfigureAwait(false);
        }

        /// <summary>
        /// Re-reads the properties of this instance from <see cref="Package"/> database. 
        /// Doesn't affect the commodities
        /// </summary>
        public async Task Reload()
        {
            await using var con = Package.GetConnection();

            var fields = await con.QueryFirstAsync("SELECT * FROM CImage WHERE id = @Id", new {Id})
                .ConfigureAwait(false);
            Contrast = (float) fields.Contrast;
            Brightness = (float) fields.Brightness;
        }

        /// <summary>
        /// Re-reads any file specific properties like <see cref="Size"/>.
        /// </summary>
        public void Refresh()
        {
            try
            {
                using var imgStream = Package.OpenImageStream(this);
                Size = Image.Identify(imgStream).Size();
            }
            catch (NullReferenceException ex) when (ex.StackTrace.Contains(nameof(Package.OpenImageStream)))
            {
                Size = new Size(0, 0);
            }
        }

        /// <summary>
        /// Doesn't support concurrent access.
        /// In case of modification please call <see cref="Refresh"/>.
        /// </summary>
        public Stream OpenStream() => Package.OpenImageStream(this);

        public delegate void DeletingEventHandler(CImage sender);

        public event DeletingEventHandler? Deleting;

        /// <summary>
        /// Deletes the image and all of its <see cref="Commodities"/> from the <see cref="Package"/>.
        /// Will raise <see cref="Deleting"/> after deleting all of its <see cref="Commodities"/> but before deleting the image it-self.
        /// </summary>
        public async Task Delete()
        {
            //I had to lock on the collection to prevent the commodities from modifying _commodities then its iterator will become invalid 
            System.Runtime.CompilerServices.ConfiguredTaskAwaitable comsDeletingTask;

            await _commoditiesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                //Here the iterator can become invalid
                comsDeletingTask = Task.WhenAll(Commodities.Select(c => c.Delete()).ToArray()).ConfigureAwait(false);
            }
            finally
            {
                _commoditiesLock.Release();
            }

            await comsDeletingTask;
            Deleting?.Invoke(this);
            await using var con = Package.GetConnection();
            await Package.RemoveImage(this).ConfigureAwait(false);
            await con.ExecuteAsync("DELETE FROM CImage WHERE id = @Id", new {Id}).ConfigureAwait(false);
        }

        /// <summary>
        /// Just a lock to prevent the image from being designed by multiple <see cref="DesignCImage{TPixel}"/>s.s
        /// </summary>
        private int _designInstancesCount = 0;

        /// <summary>
        /// Attempts to open this instance in a <see cref="DesignCImage{TPixel}"/> if its not already open.
        /// </summary>
        /// <param name="result">Result of the operation, will be <see cref="null"/> in case the operation wasn't successful.</param>
        /// <param name="designSize"><see cref="DesignCImage{TPixel}.InstanceSize"/> of <paramref name="result"/>.</param>
        /// <typeparam name="TPixel">Type of pixel to use in storing of the resulting image.</typeparam>
        /// <returns>
        /// <see cref="true"/> if its open successfuly and <paramref name="result"/> will contain an instance of <see cref="DesignCImage{TPixel}"/>,
        /// otherwise <see cref="false"/> and <paramref name="result"/> will contain <see cref="null"/>.
        /// </returns>
        public bool TryDesign<TPixel>(out DesignCImage<TPixel>? result, Size designSize)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (Interlocked.CompareExchange(ref _designInstancesCount, 1, 0) == 0)
            {
                result = new DesignCImage<TPixel>(this, designSize);
                result.ImageDisposed += (s) => Interlocked.Decrement(ref _designInstancesCount);
                return true;
            }

            result = null;
            return false;
        }

        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// You shouldn't call this explecitly, instead call <see cref="CommodityPackage.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
                PropertyChanged = null;
                CommodityRemoved = null;
                CommodityAdded = null;
                _propertyNotificationManager.Dispose();
                foreach (var com in _commodities)
                {
                    com.Dispose();
                }

                _commodities.Clear();
                _commoditiesLock.Dispose();
            }
        }

        #endregion
    }
}