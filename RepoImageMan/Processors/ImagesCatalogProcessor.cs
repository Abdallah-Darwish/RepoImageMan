using Avalonia.Media;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Memory;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RepoImageMan.Processors
{
    public abstract class ImagesCatalogProcessor<TPixel> where TPixel : struct, IPixel<TPixel>
    {
        private readonly CImage[] _images;

        private readonly RotateMode _rotationMode = RotateMode.None;
        protected virtual int? GetImageQuality(CImage image) => null;
        protected virtual Avalonia.PixelSize GetImageSize(CImage image) => image.Size;
        protected virtual Stream GetImageStream(CImage image, int pos) => new MemoryStream();
        //must support concurrent calls
        protected abstract void OnCompleted(CImage image, int pos, Stream imageStream);

        protected ImagesCatalogProcessor(ReadOnlyMemory<CImage> images, int bufferSize = 50000000, RotateMode rotationMode = RotateMode.None)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), $"{nameof(bufferSize)} must be > 0.");
            }
            if (images.Length == 0)
            {
                throw new ArgumentException($"{nameof(images)} can't be empty.", nameof(images));
            }
            _bufferSize = bufferSize;
            _rotationMode = rotationMode;

            _images = images.ToArray()
                .OrderBy(i => i.Commodities.Count > 0 ? i.Commodities.Min(c => c.Position) : int.MaxValue)
                .ThenBy(i => i.Id)
                .ToArray();
        }
        private IImmutableDictionary<int, string> _commoditiesLabels;
        private int _bufferSize = 50000000;
        private ArrayPool<byte> _convertingBuffers;
        private ArrayPoolMemoryAllocator _imagesPool;
        private Configuration _imagesConfig;
        private void ProcessImage(CImage image, int pos)
        {
            var convertingBuff = _convertingBuffers.Rent(_bufferSize);
            try
            {
                using (var convertingStream = new MemoryStream(convertingBuff))
                {
                    using (var bmp = new RenderTargetBitmap(image.Size))
                    {
                        using (var ctx = bmp.CreateDrawingContext(null))
                        {
                            using (var imageStream = image.OpenStream())
                            {
                                using var originalImage = new Bitmap(imageStream);
                                ctx.DrawImage(originalImage.PlatformImpl, 1.0, new Avalonia.Rect(default, image.Size.ToSize(1.0)), new Avalonia.Rect(default, image.Size.ToSize(1.0)));
                            }
                            foreach (var com in image.Commodities)
                            {
                                var txt = new FormattedText
                                {
                                    Text = _commoditiesLabels[com.Id],
                                    Wrapping = TextWrapping.NoWrap,
                                    Typeface = com.Font.ToTypeFace(),
                                    TextAlignment = TextAlignment.Left
                                };
                                ctx.DrawText(new SolidColorBrush(com.LabelColor), com.Location, txt.PlatformImpl);
                            }
                        }
                        bmp.Save(convertingStream);
                    }
                    var resStream = GetImageStream(image, pos);
                    using (var processingImage = Image.Load(_imagesConfig, convertingStream))
                    {
                        processingImage.Mutate(c => c.Resize(GetImageSize(image).ToSixLabors()).Rotate(_rotationMode));

                        var encoder = new JpegEncoder
                        {
                            Quality = GetImageQuality(image)
                        };
                        processingImage.Save(resStream, encoder);
                    }
                    OnCompleted(image, pos, resStream);
                }
            }
            finally
            {
                _convertingBuffers.Return(convertingBuff);
            }
        }
        private volatile bool _working = false;
        public void Start()
        {
            if (_working == true) { throw new InvalidOperationException("This processor has already started."); }
            try
            {
                _working = true;
                _commoditiesLabels = ImmutableDictionary<int, string>.Empty.AddRange(
                    _images.SelectMany(i => i.Commodities)
                        .OrderBy(c => c.Position)
                        .Select((com, pos) => new KeyValuePair<int, string>(com.Id, pos.ToString())));

                _convertingBuffers = ArrayPool<byte>.Create(_bufferSize, Environment.ProcessorCount);
                _imagesPool = ArrayPoolMemoryAllocator.CreateWithAggressivePooling();
                _imagesConfig = new Configuration { MemoryAllocator = _imagesPool, MaxDegreeOfParallelism = Environment.ProcessorCount };
                Parallel.ForEach(_images.Select((img, idx) => (Image: img, Index: idx)), x => ProcessImage(x.Image, x.Index));
            }
            finally
            {
                _convertingBuffers = null;
                _imagesPool?.ReleaseRetainedResources();
                _imagesPool = null;
                _imagesConfig = null;
                _commoditiesLabels = null;
                _working = false;
            }
        }


    }
}