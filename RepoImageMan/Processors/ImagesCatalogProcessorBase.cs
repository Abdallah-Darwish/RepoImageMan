using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RepoImageMan.Processors
{
    public abstract class ImagesCatalogProcessorBase
    {
        private readonly ReadOnlyMemory<CImage> _images;


        protected virtual int GetImageQuality(CImage image) => 75;
        protected virtual Avalonia.PixelSize GetImageSize(CImage image) => image.Size;
        protected virtual Stream GetImageStream(CImage image, int pos) => new MemoryStream();
        protected virtual CImage[] SortAndFilter(ReadOnlyMemory<CImage> images) => images
            .ToArray()
            .Where(i => i.IsExported == true)
            .OrderBy(i => i.Commodities.Count > 0 ? i.Commodities.Min(c => c.Position) : int.MaxValue)
            .ThenBy(i => i.Id)
            .ToArray();
        protected virtual string GetCommodityLabel(Commodity com) => _commoditiesLabels[com.Id];
        //must support concurrent calls
        protected abstract void OnImageProcessed(CImage image, int pos, Stream imageStream);
        protected virtual void OnCompleted() { }
        private readonly bool _rotate = false;
        protected ImagesCatalogProcessorBase(ReadOnlyMemory<CImage> images, bool rotate)
        {
            if (images.Length == 0)
            {
                throw new ArgumentException($"{nameof(images)} can't be empty.", nameof(images));
            }
            _rotate = rotate;
            _images = images;
        }
        private IImmutableDictionary<int, string> _commoditiesLabels;

        private void ProcessImage1(CImage image, int pos)
        {
            var sz = GetImageSize(image);
            using var imgStream = image.OpenStream();
            using var orgImg = SKBitmap.Decode(imgStream);
            using var sur = SKSurface.Create(new SKImageInfo(_rotate ? sz.Height : sz.Width, _rotate ? sz.Width : sz.Height));
            if (_rotate)
            {
                sur.Canvas.Translate(sz.Height, 0);
                sur.Canvas.RotateDegrees(90);
            }
            //This code works fine, just want to skip it until I find good values for contrast and brightness
            /*
            using (var orgImgPaint = new SKPaint())
            {
                float scale = image.Contrast + 1f;
                float contrast = (-0.5f * scale + 0.5f) * 255f;
                using var contrastFilter = SKColorFilter.CreateColorMatrix(new float[] {
                scale, 0, 0, 0, contrast,
                0, scale, 0, 0, contrast,
                0, 0, scale, 0, contrast,
                0, 0, 0, 1, 0 });

                using var brightnessFilter = SKColorFilter.CreateColorMatrix(new float[] {
                1, 0, 0, 0, image.Brightness,
                0, 1, 0, 0, image.Brightness,
                0, 0, 1, 0, image.Brightness,
                0, 0, 0, 1, 0 });

                orgImgPaint.FilterQuality = SKFilterQuality.High;
                orgImgPaint.ColorFilter = SKColorFilter.CreateCompose(brightnessFilter, contrastFilter);
                sur.Canvas.DrawBitmap(orgImg, new SKPoint(0, 0), orgImgPaint);
            }
            */
            //Comment next line if brightness and contrast is uncommented
            sur.Canvas.DrawBitmap(orgImg, new SKPoint(0, 0));
            foreach (var com in image.Commodities)
            {
                if (com.IsExported == false) { continue; }
                var comText = new FormattedTextImpl(GetCommodityLabel(com), com.Font, com.LabelColor);
                comText.Draw(sur.Canvas, com.Location.ToSKPoint());
            }
            sur.Canvas.Flush();
            using var surSnap = sur.Snapshot();
            using var surSnapData = surSnap.Encode(SKEncodedImageFormat.Jpeg, GetImageQuality(image));
            using var surSnapDataStrem = surSnapData.AsStream();
            var processedImageStream = GetImageStream(image, pos);
            surSnapDataStrem.CopyTo(processedImageStream);
            OnImageProcessed(image, pos, processedImageStream);
        }

        private volatile bool _started = false;

        public void Start()
        {
            if (_started == true) { throw new InvalidOperationException("This processor has already started."); }
            try
            {
                _started = true;
                var sortedImages = SortAndFilter(_images);
                _commoditiesLabels = ImmutableDictionary<int, string>.Empty.AddRange(
                    sortedImages.SelectMany(i => i.Commodities)
                    .Where(c => c.IsExported == true)
                    .OrderBy(c => c.Position)
                    .Select((com, pos) => new KeyValuePair<int, string>(com.Id, (pos + 1).ToString())));
                Parallel.ForEach(sortedImages.Select((img, idx) => (Image: img, Index: idx + 1)), x => ProcessImage1(x.Image, x.Index));

            }
            finally
            {
                _commoditiesLabels = null;
                OnCompleted();
            }
        }


    }
}