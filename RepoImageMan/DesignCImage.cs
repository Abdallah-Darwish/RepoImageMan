using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
namespace RepoImageMan
{
    /// <summary>
    /// A specialization of <see cref="CImage"/> that is used for editing the image using UI and still provide fast operations.
    /// YOU CAN'T MODIFY IMAGE STREAM WHILE DESIGNING.
    /// </summary>
    public sealed class DesignCImage<TPixel> : IDisposable where TPixel : unmanaged, IPixel<TPixel>
    {
        public delegate void ImageUpdatedEventHandler(DesignCImage<TPixel> sender);
        /// <summary>
        /// Occures when the underlying <see cref="CImage"/> is updated or any <see cref="ImageCommodity"/> is updated.
        /// <see cref="RenderedImage"/> will be updated before raising.
        /// </summary>
        public event ImageUpdatedEventHandler? ImageUpdated;

        internal delegate void ImageDisposedEventHandler(DesignCImage<TPixel> sender);
        /// <remarks>
        /// Used to tell the original image that this instance is being disposed so another instance can be created.
        /// </remarks>
        internal event ImageDisposedEventHandler? ImageDisposed;

        /// <summary>
        /// The original image that this instance is actting upon.
        /// </summary>
        public CImage Image { get; }
        /// <summary>
        /// Provieds a way to resize this image to fit the final ImageBox without having to resize after every update.
        /// This will not change <see cref="Image"/> <see cref="CImage.SizeRatio"/>.
        /// </summary>
        public Size InstanceSize { get; }
        /// <summary>
        /// The scale that is used to map points from <see cref="CImage"/> to this resized <see cref="DesignCImage"/>.
        /// </summary>
        public SizeF ToOriginalMappingScale { get; }
        /// <summary>
        /// The scale that is used to map points from this instance to the original <see cref="CImage"/>.
        /// </summary>
        public SizeF ToDesignMappingScale { get; }

        private readonly List<DesignImageCommodity<TPixel>> _commodities = new List<DesignImageCommodity<TPixel>>();
        /// <summary>
        /// Commodities in the image with the ability to modify them.
        /// </summary>
        public IReadOnlyList<DesignImageCommodity<TPixel>> Commodities => _commodities;

        /// <summary>
        /// Returns first <see cref="DesignImageCommodity"/> that the point <paramref name="p"/> lies inside,
        /// or <see langword="null"/> if there is none.
        /// </summary>
        public DesignImageCommodity<TPixel>? FirstOnPoint(in PointF p)
        {
            foreach (var com in _commodities)
            {
                if (com.IsInside(in p)) { return com; }
            }
            return null;
        }

        public Image<TPixel> RenderedImage { get; private set; }

        /// <summary>
        /// Contains the image with <see cref="CImage.Contrast"/> applied to it and resized but nothing is written on it.
        /// </summary>
        private Image<TPixel> _renderingPlayground;
        /// <summary>
        /// Contains the original image stored in <see cref="Image"/> stream, but resized.
        /// </summary>
        private readonly Image<TPixel> _originalImage;

        private readonly Image<TPixel> _handleImage;
        private void Render()
        {
            void CopyRow(ReadOnlyMemory<TPixel> rowMemory, Point p)
            {
                var srcRow = MemoryMarshal.AsBytes(rowMemory.Span);
                var dstRow = MemoryMarshal.AsBytes(RenderedImage.GetPixelRowSpan(p.Y).Slice(p.X));

                Vector<int> srcV, dstV, tmpV;

                for (; srcRow.Length >= Vector<byte>.Count;)
                {
                    srcV = new Vector<int>(srcRow);
                    dstV = new Vector<int>(dstRow);
                    tmpV = srcV | dstV;
                    //TODO: check me pls
                    tmpV.TryCopyTo(dstRow);

                    //p.X += Vector<int>.Count;
                    srcRow = srcRow.Slice(Vector<byte>.Count);
                    dstRow = dstRow.Slice(Vector<byte>.Count);
                }

                for (int i = 0; i < srcRow.Length; i++)
                {
                    dstRow[i] = srcRow[i];
                }
            }
            void SurroundCommodity(DesignImageCommodity<TPixel> com)
            {
                //TODO: cache the resized images(No need to use CopyRow because the handle is usually too small so we will waste time in ContextSwitching and jumping)
                var comHandle = _handleImage.Clone(c => c.Resize(com.HandleSize));
                RenderedImage
                    .Mutate(c => c
                    .DrawPolygon(com.SurroundingBoxColor, com.SurroundingBoxThickness, com.GetSurroundingBox())
                    .DrawImage(comHandle, com.HandleLocation, 1f));
            }
            RenderedImage?.Dispose();
            RenderedImage = _renderingPlayground.Clone(c => c.Resize(InstanceSize));

            var copyingJobs = new List<(ReadOnlyMemory<TPixel> Row, Point RowLocation)>();
            var labelsCache = Image.Package.GetLabelsCache<TPixel>();

            foreach (var com in Commodities)
            {
                //Possible optimization is to execute the next line in parallel alone to enusre that the label rendering is done in parallel but its not a hot-path so it doesn't matter.
                var comLabel = labelsCache.GetLabel(new LabelRenderingOptions
                {
                    Font = com.Font,
                    Color = com.Commodity.LabelColor,
                    Text = DesignImageCommodity<TPixel>.LabelText
                }).Span;
                Point rowLoaction = (Point)com.Location;
                for (int labelRowIndex = 0; labelRowIndex < comLabel.Length; labelRowIndex++, rowLoaction.Y++)
                {
                    copyingJobs.Add((comLabel[labelRowIndex].AsMemory(), rowLoaction));
                }
            }

            if (Parallel.ForEach(copyingJobs, tu => CopyRow(tu.Row, tu.RowLocation)).IsCompleted == false)
            {
                RenderedImage.Mutate(c => c.Fill(Color.Red));
                throw new Exception($"Rendering wasn't done, successfully !!!{Environment.NewLine}Couldn't RENDER labels correctly.");
            }

            if (Parallel.ForEach(Commodities.Where(c => c.IsSurrounded), SurroundCommodity).IsCompleted == false)
            {
                RenderedImage.Mutate(c => c.Fill(Color.Green));
                throw new Exception($"Rendering wasn't done, successfully !!!{Environment.NewLine}Couldn't SURROUND labels correctly.");
            }
        }

        private void AddCommodity(ImageCommodity com)
        {
            var dCom = new DesignImageCommodity<TPixel>(com, this);
            dCom.Updated += CommodityUpdated;
            _commodities.Add(dCom);
        }
        private void CommodityUpdated(DesignImageCommodity<TPixel> sender) => UpdateMe();
        private void UpdateMe()
        {
            Render();
            ImageUpdated?.Invoke(this);
        }

        internal DesignCImage(CImage image, Size mySize, Image<TPixel> handleImage)
        {
            InstanceSize = mySize;
            Image = image;

            _handleImage = handleImage.Clone();

            using (var imgStream = Image.OpenStream())
            {
                _originalImage = Image<TPixel>.Load<TPixel>(imgStream);
            }
            ToOriginalMappingScale = new SizeF(_originalImage.Width / (float)InstanceSize.Width, _originalImage.Height / (float)InstanceSize.Height);
            ToDesignMappingScale = new SizeF(InstanceSize.Width / (float)_originalImage.Width, InstanceSize.Height / (float)_originalImage.Height);

            foreach (var com in Image.Commodities)
            {
                AddCommodity(com);
            }
            Image.CommodityAdded += CommodityAdded;
            Image.CommodityRemoved += CommodityRemoved;
            Image.PropertyNotificationManager
                .Subscribe(nameof(CImage.Contrast), UpdatePlayground)
                .Subscribe(nameof(CImage.Brightness), UpdatePlayground);
            UpdatePlayground(this, new PropertyChangedEventArgs("CALLED FROM CONSTRUCTOR"));
            Render();
        }

        private void CommodityAdded(CImage sender, ImageCommodity com) => AddCommodity(com);

        private void UpdatePlayground(object sender, PropertyChangedEventArgs e)
        {
            //TODO: make me brightness
            _renderingPlayground = _originalImage.Clone(c => c.Contrast(Image.Contrast).Brightness(Image.Brightness));
        }
        private void CommodityRemoved(CImage sender, ImageCommodity com)
        {
            var dCom = _commodities.First(c => c.Commodity.Id == com.Id);
            dCom.Dispose();
            _commodities.Remove(dCom);
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
                Image.PropertyNotificationManager
                    .Unsubscribe(nameof(CImage.Contrast), UpdatePlayground)
                    .Unsubscribe(nameof(CImage.Brightness), UpdatePlayground);
                Image.CommodityRemoved -= CommodityRemoved;
                Image.CommodityAdded -= CommodityAdded;
                foreach (var com in _commodities) { com.Dispose(); }
                _commodities.Clear();
                _originalImage.Dispose();
                _renderingPlayground.Dispose();
                RenderedImage.Dispose();
                ImageUpdated = null;
                ImageDisposed?.Invoke(this);
                ImageDisposed = null;
            }
        }
        #endregion
    }
}