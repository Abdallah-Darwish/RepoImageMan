﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Dapper;
using RepoImageMan.Controls;

namespace RepoImageMan
{
    public sealed class CImage : IDisposable, IObservable<string>
    {

        /// <summary>
        /// Returns the expected file name that will be generated for an image.
        /// used mainly to create an empty file before loading an image.
        /// </summary>
        internal static string GetCImagePackageFilePath(string packageDirectory, int imageId) => Path.Combine(packageDirectory, $"{imageId}.bmp");

        /// <summary>
        /// Returns the expected file name that will be generated for an image.
        /// used mainly to create an empty file before loading an image.
        /// </summary>
        internal static string GetCImagePackageFilePath(CommodityPackage package, int imageId) => GetCImagePackageFilePath(package.PackageDirectoryPath, imageId);
        /// <summary>
        /// Name of the image entry(or file) inside <see cref="Package"/> directory.
        /// </summary>
        public string PackageFileName => $"{Id}.bmp";
        public string PackageFilePath => Path.Combine(Package.PackageDirectoryPath, PackageFileName);

        /// <summary>
        /// Dimensions of the image.
        /// </summary>
        public PixelSize Size { get; private set; }
        private readonly ISubject<string> _notificationsSubject = new Subject<string>();

        public IDisposable Subscribe(IObserver<string> observer) => _notificationsSubject.Subscribe(observer);

        //Kept as a seperate method in case I want to support INotifyPropertyChanged in the future.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnPropertyChanged([CallerMemberName] string propName = null) => _notificationsSubject.OnNext(propName);
        private CImage(int id, CommodityPackage package)
        {
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
            var comsIds = (await package.DbConnection.QueryAsync<int>("SELECT id FROM ImageCommodity WHERE imageId = @id", new { id }).ConfigureAwait(false)).AsList();
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
        /// Unique per <see cref="CommodityPackage"/> but might be repeated across packages.
        /// </summary>
        public int Id { get; private set; }

        private float _contrast;

        /// <summary>
        /// Contrast of the final image.
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
                if (value == _contrast) { return; }

                _contrast = value;
                OnPropertyChanged();
            }
        }
        private bool _isExported;
        public bool IsExported
        {
            get => _isExported;
            set
            {
                if (value == _isExported) { return; }
                _isExported = value;
                OnPropertyChanged();
            }
        }
        private float _brightness;

        /// <summary>
        /// Brightness of the final image.
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

                if (value == _brightness) { return; }

                _brightness = value;
                OnPropertyChanged();
            }
        }

        private readonly List<ImageCommodity> _commodities = new();

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
            await Package.DbConnection.ExecuteAsync(
                    @"INSERT INTO Commodity(position) VALUES((COALESCE((SELECT MAX(Position) FROM Commodity), 0) + 1));")
                .ConfigureAwait(false);
            await Package.DbConnection.ExecuteAsync(@"INSERT INTO ImageCommodity(id, imageId) VALUES(@id, @imageId);",
                new { id = (int)Package.DbConnection.LastInsertRowId, imageId = Id }).ConfigureAwait(false);

            var newCom = await ImageCommodity.Load((int)Package.DbConnection.LastInsertRowId, Package, this).ConfigureAwait(false);

            if (_commodities.Count > 0)
            {
                await newCom.SetPosition(_commodities.Max(c => c.Position) + 1).ConfigureAwait(false);
            }
            _commodities.Add(newCom);
            newCom.Font = newCom.Font.WithSize((float)(Size.ToSize(1.0).Average() * 0.2));
            await newCom.Save().ConfigureAwait(false);
            await Package.AddImageCommodity(newCom).ConfigureAwait(false);
            CommodityAdded?.Invoke(this, newCom);
            return newCom;
        }

        /// <summary>
        /// Will be raised when a <see cref="ImageCommodity"/> that belongs to this <see cref="CImage"/> is deleted from <see cref="Package"/>.
        /// </summary>
        public event CommodityModifiedEventHandler? CommodityRemoved;

        internal async Task RemoveCommodity(ImageCommodity com)
        {
            _commodities.Remove(com);
            CommodityRemoved?.Invoke(this, com);
        }

        /// <summary>
        /// Saves the properties of this instance in <see cref="Package"/> database. 
        /// Doesn't affect the commodities
        /// </summary>
        public async Task Save()
        {
            await Package.DbConnection.ExecuteAsync("UPDATE CImage SET contrast = @Contrast, brightness = @Brightness, isExported = @IsExported WHERE id = @Id", this).ConfigureAwait(false);
        }

        /// <summary>
        /// Re-reads the properties of this instance from <see cref="Package"/> database. 
        /// Doesn't affect the commodities
        /// </summary>
        public async Task Reload()
        {
            var fields = await Package.DbConnection.QueryFirstAsync("SELECT * FROM CImage WHERE id = @Id", new { Id }).ConfigureAwait(false);
            Contrast = (float)fields.Contrast;
            Brightness = (float)fields.Brightness;
            IsExported = (bool)fields.IsExported;
        }

        public delegate void ImageFileUpdatedEventHandler(CImage image);

        /// <summary>
        /// Raised when the bytes of this image are modeified
        /// </summary>
        public event ImageFileUpdatedEventHandler FileUpdated;

        /// <summary>
        /// Re-reads any file specific properties like <see cref="Size"/>.
        /// </summary>
        private void Refresh()
        {
            using (var imgStream = new FileStream(PackageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Size = new PixelSize(0, 0);
                if (imgStream.Length > 0)
                {
                    var imageInfo = SixLabors.ImageSharp.Image.Identify(imgStream);
                    Size = imageInfo is null ? new PixelSize(0, 0) : new PixelSize(imageInfo.Width, imageInfo.Height);
                }
                //If the stream is empty(it will be when we create a new image) the returned 'IImageInfo' would be null
                foreach (var com in Commodities)
                {
                    com.Location = new Point(Math.Min(Size.Width, com.Location.X), Math.Min(Size.Height, com.Location.Y));
                }
            }

            FileUpdated?.Invoke(this);
        }

        /// <summary>
        /// RETURNS A READONLY STREAM.
        /// </summary>
        public Stream OpenStream() => new FileStream(PackageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);


        /// <summary>
        /// Erases the old file and writes this one instead.
        /// </summary>
        /// <param name="newFile">Will be read from its current position to end.</param>
        public async Task ReplaceFile(Stream newFile)
        {
            if (_designInstancesCount != 0)
            {
                throw new InvalidOperationException("You can't modify the image file while it is open for design.");
            }
            var originalStreamPos = newFile.Position;
            var imageInfo = SixLabors.ImageSharp.Image.Identify(newFile);
            if (imageInfo is null)
            {
                throw new ArgumentException("Invalid image stream.", nameof(newFile));
            }
            newFile.Position = originalStreamPos;

            using (var fs = new FileStream(PackageFilePath, FileMode.Create, FileAccess.ReadWrite))
            using (var img = SixLabors.ImageSharp.Image.Load(newFile))
            {
                fs.SetLength(0);
                SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(img, fs);
                await fs.FlushAsync().ConfigureAwait(false);
            }
            var oldSize = Size;
            Refresh();
            float fontResizeFactor = (float)Size.Width / oldSize.Width + (float)Size.Height / oldSize.Height;
            fontResizeFactor /= 2f;
            foreach (var com in Commodities)
            {
                com.Location = new Point(Math.Floor(com.Location.X / oldSize.Width * Size.Width), Math.Floor(com.Location.Y / oldSize.Height * Size.Height));
                com.Font = com.Font.WithSize(com.Font.Size * fontResizeFactor);
                await com.Save().ConfigureAwait(false);
            }

        }

        public delegate void DeletingEventHandler(CImage sender);

        public event DeletingEventHandler? Deleting;

        /// <summary>
        /// Deletes the image and all of its <see cref="Commodities"/> from the <see cref="Package"/>.
        /// Will raise <see cref="Deleting"/> after deleting all of its <see cref="Commodities"/> but before deleting the image it-self.
        /// </summary>
        public async Task Delete()
        {
            //Here the iterator can become invalid
            while (Commodities.Count != 0)
            {
                await Commodities[0].Delete().ConfigureAwait(false);
            }
            Deleting?.Invoke(this);
            await Package.RemoveImage(this).ConfigureAwait(false);
            await Package.DbConnection.ExecuteAsync("DELETE FROM CImage WHERE id = @Id", new { Id }).ConfigureAwait(false);
        }

        /// <summary>
        /// Just a lock to prevent the image from being designed by multiple <see cref="DesignCImage"/>s.
        /// </summary>
        private int _designInstancesCount = 0;

        internal bool TryEnterDesign() => Interlocked.CompareExchange(ref _designInstancesCount, 1, 0) == 0;

        internal void ExitDesign()
        {
            if (Interlocked.CompareExchange(ref _designInstancesCount, 0, 1) != 1)
            {
                throw new InvalidOperationException("This image is not in design state originally to exit it.");
            }
        }

        internal async Task Tidy(int newId)
        {
            var newImagePath = GetCImagePackageFilePath(Package, newId);
            if (File.Exists(newImagePath))
            {
                throw new InvalidOperationException($"Can't change image id because there exists another file with same expected path for this image when the change is done.\n{newImagePath}");
            }
            if ((await Package.DbConnection.ExecuteScalarAsync<int?>("SELECT id FROM CImage WHERE id = @newId", new { newId }).ConfigureAwait(false)) != null)
            {
                throw new InvalidOperationException("Can't change image id because there exists another image with same id.");
            }
            await Package.DbConnection.ExecuteAsync("UPDATE CImage SET id = @newId WHERE id = @Id", new { Id, newId }).ConfigureAwait(false);
            File.Move(PackageFilePath, GetCImagePackageFilePath(Package, newId));
            Id = newId;
        }
        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// You shouldn't call this explicitly, instead call <see cref="CommodityPackage.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            if (_disposedValue) return;
            _disposedValue = true;
            CommodityRemoved = null;
            CommodityAdded = null;
            _notificationsSubject.OnCompleted();
            foreach (var com in _commodities)
            {
                com.Dispose();
            }

            _commodities.Clear();
        }
        #endregion
    }
}