using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
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
    public abstract class ImagesCatalogProcessorBase
    {
        private readonly ReadOnlyMemory<CImage> _images;

        private readonly RotateMode _rotationMode = RotateMode.None;
        protected virtual int? GetImageQuality(CImage image) => null;
        protected virtual Avalonia.PixelSize GetImageSize(CImage image) => image.Size;
        protected virtual Stream GetImageStream(CImage image, int pos) => new MemoryStream();
        protected virtual CImage[] Sort(ReadOnlyMemory<CImage> images) => images.ToArray()
                .OrderBy(i => i.Commodities.Count > 0 ? i.Commodities.Min(c => c.Position) : int.MaxValue)
                .ThenBy(i => i.Id)
                .ToArray();
        protected virtual string GetCommodityLabel(Commodity com) => _commoditiesLabels[com.Id];
        //must support concurrent calls
        protected abstract void OnImageProcessed(CImage image, int pos, Stream imageStream);
        protected virtual void OnCompleted() { }

        protected ImagesCatalogProcessorBase(ReadOnlyMemory<CImage> images, int bufferSize = 50000000, RotateMode rotationMode = RotateMode.None)
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
            _images = images;
        }
        private IImmutableDictionary<int, string> _commoditiesLabels;
        private readonly int _bufferSize = 50000000;
        private ArrayPool<byte> _convertingBuffers;
        private ArrayPoolMemoryAllocator _imagesPool;
        private Configuration _imagesConfig;
        private Task ProcessImage1(CImage image, int pos)
        {
            //CONVERT FROM JPG TO ISLAM
            var convertingBuffer = _convertingBuffers.Rent(_bufferSize);
            var convertingStream = new MemoryStream(convertingBuffer);

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
                            Text = GetCommodityLabel(com),
                            Wrapping = TextWrapping.NoWrap,
                            Typeface = com.Font.ToTypeFace(),
                            TextAlignment = TextAlignment.Left
                        };
                        ctx.DrawText(new SolidColorBrush(com.LabelColor), com.Location, txt.PlatformImpl);
                    }
                }
                bmp.Save(convertingStream);
            }
            convertingStream.SetLength(convertingStream.Position);
            convertingStream.Position = 0;
            return ProcessImage2(image, pos, convertingStream, convertingBuffer);
        }
        private Task ProcessImage2(CImage image, int pos, MemoryStream process1Stream, byte[] process1Buffer)
        {
            return Task.Run(() =>
            {

                Stream resStream = null;
                try
                {
                    using (process1Stream)
                    {
                        resStream = GetImageStream(image, pos);
                        using (var processingImage = Image.Load(_imagesConfig, process1Stream, new SixLabors.ImageSharp.Formats.Png.PngDecoder()))
                        {
                            processingImage.Mutate(c => c.Resize(GetImageSize(image).ToSixLabors()).Rotate(_rotationMode));

                            var encoder = new JpegEncoder
                            {
                                Quality = GetImageQuality(image)
                            };
                            processingImage.Save(resStream, encoder);
                        }
                        OnImageProcessed(image, pos, resStream);
                    }
                }
                catch
                {
                    resStream?.Dispose();
                    throw;
                }
                finally
                {
                    _convertingBuffers.Return(process1Buffer);
                }
            });
        }
        private volatile bool _started = false;
        /// <summary>
        /// WILL BLOCK TF OUT OF UI-THREAD
        /// </summary>
        public async Task Start()
        {
            Dispatcher.UIThread.VerifyAccess();
            if (_started == true) { throw new InvalidOperationException("This processor has already started."); }
            try
            {
                _started = true;
                var sortedImages = Sort(_images);
                _commoditiesLabels = ImmutableDictionary<int, string>.Empty.AddRange(
                    sortedImages.SelectMany(i => i.Commodities)
                        .OrderBy(c => c.Position)
                        .Select((com, pos) => new KeyValuePair<int, string>(com.Id, pos.ToString())));
                _convertingBuffers = ArrayPool<byte>.Create(_bufferSize, Environment.ProcessorCount * 2);
                _imagesPool = ArrayPoolMemoryAllocator.CreateWithAggressivePooling();
                _imagesConfig = new Configuration { MemoryAllocator = _imagesPool, MaxDegreeOfParallelism = Environment.ProcessorCount };
                int pos = 0;
                var processing2Tasks = new Task[sortedImages.Length];
                foreach (var img in sortedImages)
                {
                    processing2Tasks[pos] = ProcessImage1(img, pos);
                    pos++;
                }
                await Task.WhenAll(processing2Tasks);

            }
            finally
            {
                _convertingBuffers = null;
                _imagesPool?.ReleaseRetainedResources();
                _imagesPool = null;
                _imagesConfig = null;
                _commoditiesLabels = null;
                OnCompleted();
            }
        }


    }
}