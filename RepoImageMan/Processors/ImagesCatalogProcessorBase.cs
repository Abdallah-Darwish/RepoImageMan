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

        protected ImagesCatalogProcessorBase(ReadOnlyMemory<CImage> images)
        {
            if (images.Length == 0)
            {
                throw new ArgumentException($"{nameof(images)} can't be empty.", nameof(images));
            }
            //_rotationMode = rotationMode;
            _images = images;
        }
        private IImmutableDictionary<int, string> _commoditiesLabels;

        private void ProcessImage1(CImage image, int pos)
        {
            var sz = GetImageSize(image);
            using var imgStream = image.OpenStream();
            using var orgImg = SKBitmap.Decode(imgStream);
            using var sur = SKSurface.Create(new SKImageInfo(sz.Width, sz.Height));
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