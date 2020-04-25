﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Diagnostics.Tracing;
using System.Diagnostics;
using System.Reactive.Linq;
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
        /// Occurs when the underlying <see cref="CImage"/> is updated or any <see cref="ImageCommodity"/> is updated.
        /// <see cref="RenderedImage"/> will be updated before raising.
        /// </summary>
        public event ImageUpdatedEventHandler? ImageUpdated;

        internal delegate void ImageDisposedEventHandler(DesignCImage<TPixel> sender);

        /// <remarks>
        /// Used to tell the original image that this instance is being disposed so another instance can be created.
        /// </remarks>
        internal event ImageDisposedEventHandler? DesignImageDisposed;

        /// <summary>
        /// The original image that this instance is actting upon.
        /// </summary>
        public CImage Image { get; }

        private Size _instanceSize;

        /// <summary>
        /// Provieds a way to resize this image to fit the final ImageBox without having to resize after every update.
        /// This will not change <see cref="Image"/> <see cref="CImage.SizeRatio"/>.
        /// </summary>
        public Size InstanceSize
        {
            get => _instanceSize;
            set
            {
                if (value.Height < 0 || value.Width < 0) { throw new ArgumentOutOfRangeException(nameof(value), value, $"The size must be in range [(1, 1), (INF, INF)]."); }
                if (_instanceSize == value) { return; }

                _instanceSize = value;
                foreach (var com in _commodities)
                {
                    com.UpdateAfterImageResize();
                }
                UpdateMe();
            }
        }

        /// <summary>
        /// The scale that is used to map points from <see cref="CImage"/> to this resized <see cref="DesignCImage"/>.
        /// </summary>
        public SizeF ToOriginalMappingScale => new SizeF(_originalImage.Width / (float)InstanceSize.Width, _originalImage.Height / (float)InstanceSize.Height);

        /// <summary>
        /// The scale that is used to map points from this instance to the original <see cref="CImage"/>.
        /// </summary>
        public SizeF ToDesignMappingScale => new SizeF(InstanceSize.Width / (float)_originalImage.Width, InstanceSize.Height / (float)_originalImage.Height);

        private readonly List<DesignImageCommodity<TPixel>> _commodities = new List<DesignImageCommodity<TPixel>>();

        /// <summary>
        /// Commodities in the image with the ability to modify them.
        /// </summary>
        public IReadOnlyList<DesignImageCommodity<TPixel>> Commodities => _commodities;

        /// <summary>
        /// Returns first <see cref="DesignImageCommodity{TPixel}"/> that the point <paramref name="p"/> lies inside,
        /// or <see langword="null"/> if there is none.
        /// </summary>
        public DesignImageCommodity<TPixel>? FirstOnPoint(PointF p) => _commodities.AsParallel().FirstOrDefault(com => com.IsInside(p));

        public Image<TPixel> RenderedImage { get; private set; }

        /// <summary>
        /// Contains the image with <see cref="CImage.Contrast"/> and <see cref="CImage.Brightness"/> applied to it and resized but nothing is written on it.
        /// </summary>
        private Image<TPixel> _renderingPlayground;

        /// <summary>
        /// Contains the original image stored in <see cref="Image"/> stream, but resized.
        /// </summary>
        private readonly Image<TPixel> _originalImage;

        private void Render()
        {
            void CopyRow(ReadOnlyMemory<TPixel> rowMemory, Point p)
            {
                int toBeRemoved = 0;
                if (p.X < 0)
                {
                    toBeRemoved = -p.X;
                    p.X = 0;
                }

                var dstRow = MemoryMarshal.AsBytes(RenderedImage.GetPixelRowSpan(p.Y).Slice(p.X));
                var srcRow = MemoryMarshal.AsBytes(rowMemory.Span);
                if (toBeRemoved > 0)
                {
                    srcRow = srcRow[toBeRemoved..];
                }

                if (srcRow.Length > dstRow.Length)
                {
                    srcRow = srcRow[..dstRow.Length];
                }

                Vector<byte> srcV, dstV;

                for (; srcRow.Length >= Vector<byte>.Count;)
                {
                    srcV = new Vector<byte>(srcRow);
                    dstV = new Vector<byte>(dstRow);
                    dstV |= srcV;
                    dstV.CopyTo(dstRow);

                    //p.X += Vector<int>.Count;
                    srcRow = srcRow.Slice(Vector<byte>.Count);
                    dstRow = dstRow.Slice(Vector<byte>.Count);
                }

                for (int i = 0; i < srcRow.Length; i++)
                {
                    dstRow[i] |= srcRow[i];
                }
            }

            void SurroundCommodity(DesignImageCommodity<TPixel> com)
            {
                var comHandle = Image.Package.GetHandle<TPixel>(com.HandleSize);
                RenderedImage.Mutate(c =>
                c.DrawPolygon(com.SurroundingBoxColor, com.SurroundingBoxThickness, com.GetSurroundingBox())
                .DrawImage(comHandle, com.HandleLocation, 1f));
            }
            Trace.WriteLine("Disposing old image");
            RenderedImage?.Dispose();

            Trace.WriteLine("Cloning playground image");
            RenderedImage = _renderingPlayground.Clone(c => c.Resize(InstanceSize));

            var copyingJobs = new List<(ReadOnlyMemory<TPixel> Row, Point RowLocation)>();
            var labelsCache = Image.Package.GetLabelsCache<TPixel>();

            Trace.WriteLine($"Preparing commodities");
            Trace.Indent();

            foreach (var com in Commodities)
            {
                Trace.WriteLine($"Preparing commodity {com.Commodity}");
                Trace.Indent();
                Trace.WriteLine($"Preparing commodity {com.Commodity} label");
                //Possible optimization is to execute the next line in parallel alone to ensure that the label rendering is done in parallel but its not a hot-path so it doesn't matter.
                var comLabel = labelsCache.GetLabel(new LabelRenderingOptions(DesignImageCommodity<TPixel>.LabelText, com.Font, com.Commodity.LabelColor)).Span;


                Trace.WriteLine($"Splitting commodity {com.Commodity} label into rows");
                var comLocation = (Point)com.Location;
                for (int labelRowIndex = 0;
                    labelRowIndex < comLabel.Length && comLocation.Y < RenderedImage.Height;
                    labelRowIndex++, comLocation.Y++)
                {
                    copyingJobs.Add((comLabel[labelRowIndex].AsMemory(), comLocation));
                }
                Trace.WriteLine($"Finished preparing commodity {com.Commodity}");
                Trace.Unindent();
            }
            Trace.Unindent();

            Trace.WriteLine($"Finished preparing all commodities with {copyingJobs.Count} copying jobs.");
            Trace.WriteLine($"The average row size is {copyingJobs.Select(j => j.Row.Length).Average(): 0}");

            Trace.WriteLine($"Copying rows to final image");
            if (Parallel.ForEach(copyingJobs, tu => CopyRow(tu.Row, tu.RowLocation)).IsCompleted == false)
            {
                RenderedImage.Mutate(c => c.Fill(Color.Red));
                throw new Exception($"Rendering wasn't done, successfully !!!{Environment.NewLine}Couldn't RENDER labels correctly.");
            }
            Trace.WriteLine("Finished copying rows");
            Trace.WriteLine("Surronding commodities");

            if (Parallel.ForEach(Commodities.Where(c => c.IsSurrounded), SurroundCommodity).IsCompleted == false)
            {
                RenderedImage.Mutate(c => c.Fill(Color.Green));
                throw new Exception($"Rendering wasn't done, successfully !!!{Environment.NewLine}Couldn't SURROUND labels correctly.");
            }
            Trace.WriteLine("Finished Surrounding commodities");
        }

        private void AddCommodity(ImageCommodity com)
        {
            var dCom = new DesignImageCommodity<TPixel>(com, this);
            dCom.Updated += CommodityUpdated;
            _commodities.Add(dCom);
            UpdateMe();
        }

        private void CommodityUpdated(DesignImageCommodity<TPixel> sender) => UpdateMe();

        private void UpdateMe()
        {
            Render();
            ImageUpdated?.Invoke(this);
        }
        private IDisposable _imageBrightnessContrastSubscription;
        internal DesignCImage(CImage image)
        {
            Image = image;
            _instanceSize = image.Size;


            using (var imgStream = image.OpenStream())
            {
                _originalImage = Image<TPixel>.Load<TPixel>(imgStream);
            }
            UpdatePlayground();

            foreach (var com in Image.Commodities)
            {
                AddCommodity(com);
            }

            Image.CommodityAdded += CommodityAdded;
            Image.CommodityRemoved += CommodityRemoved;
            _imageBrightnessContrastSubscription = Image
                 .Where(pn => pn == nameof(CImage.Contrast) || pn == nameof(CImage.Brightness))
                 .Subscribe(pn => UpdatePlayground());
            Render();
        }

        private void CommodityAdded(CImage sender, ImageCommodity com) => AddCommodity(com);

        private void UpdatePlayground()
        {
            _renderingPlayground = _originalImage.Clone(c => c.Contrast(Image.Contrast).Brightness(Image.Brightness));
        }

        private void CommodityRemoved(CImage sender, ImageCommodity com)
        {
            var dCom = _commodities.First(c => c.Commodity.Id == com.Id);
            dCom.Dispose();
            _commodities.Remove(dCom);
            UpdateMe();
        }

        /// <summary>
        /// Returns a <see cref="SizeF"/> that you can use to map points from other images(mainly ImageBox with different size) to this image with <see cref="InstanceSize"/>.
        /// You can use the returned value with <see cref="Extensions.Scale(PointF, SizeF)"/>.
        /// </summary>
        /// <param name="sz">Points origin rectangle size.</param>
        public SizeF GetToInstanceMappingScale(SizeF sz)
        {
            if (sz.Width <= 0.0f || sz.Height <= 0.0f) { throw new ArgumentOutOfRangeException($"The size must be in range [(1, 1), (INF, INF)]."); }
            return new SizeF(InstanceSize.Width / sz.Width, InstanceSize.Height / sz.Height);
        }

        /// <summary>
        /// Returns a <see cref="SizeF"/> that you can use to map points from this image with <see cref="InstanceSize"/> to other rectangles sizes.
        /// You can use the returned value with <see cref="Extensions.Scale(PointF, SizeF)"/>.
        /// </summary>
        /// <param name="sz">Points target rectangle size.</param>
        public SizeF GetFromInstanceMappingScale(SizeF sz)
        {
            if (sz.Width <= 0.0f || sz.Height <= 0.0f) { throw new ArgumentOutOfRangeException($"The size must be in range [(1, 1), (INF, INF)]."); }
            return new SizeF(sz.Width / InstanceSize.Width, sz.Height / InstanceSize.Height);
        }

        #region IDisposable Support

        private bool _disposedValue = false; // To detect redundant calls

        /// <summary>
        /// You shouldn't call this explicitly, instead call <see cref="CommodityPackage.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_disposedValue)
            {
                _imageBrightnessContrastSubscription.Dispose();
                _imageBrightnessContrastSubscription = null;
                Image.CommodityRemoved -= CommodityRemoved;
                Image.CommodityAdded -= CommodityAdded;
                foreach (var com in _commodities)
                {
                    com.Dispose();
                }

                _commodities.Clear();
                _originalImage.Dispose();
                _renderingPlayground.Dispose();
                RenderedImage.Dispose();
                ImageUpdated = null;
                DesignImageDisposed?.Invoke(this);
                DesignImageDisposed = null;
            }
        }

        #endregion
    }
}