using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RepoImageMan.Processors
{
    public class ImagesCatalogProcessor<TPixel> where TPixel : struct, IPixel<TPixel>
    {
        public readonly struct ProcessedCImage
        {
            public readonly CImage OriginalImage;
            public readonly Stream ProcessedImage;
            public readonly int Position;

            public ProcessedCImage(CImage originalImage, Stream processedImage, int position)
            {
                OriginalImage = originalImage;
                ProcessedImage = processedImage;
                Position = position;
            }
        }

        private readonly CImage[] _images;

        //must support concurrent calls for OnNext
        private readonly IObserver<ProcessedCImage> _imagesConsumer;
        private IImmutableDictionary<int, string> _commoditiesLabels;
        private readonly IImmutableDictionary<int, int> _imagesPositions;
        private readonly RotateMode _rotationMode = RotateMode.None;

        public ImagesCatalogProcessor(ReadOnlyMemory<CImage> images, IObserver<ProcessedCImage> imagesConsumer,
            RotateMode rotationMode)
        {
            _rotationMode = rotationMode;
            _imagesConsumer = imagesConsumer;

            _images = images.ToArray()
                .OrderBy(i => i.Commodities.Count > 0 ? i.Commodities.Min(c => c.Position) : int.MaxValue)
                .ToArray();
            _imagesPositions =
                ImmutableDictionary<int, int>.Empty.AddRange(_images.Select((img, pos) =>
                    new KeyValuePair<int, int>(img.Id, pos)));
        }

        private void ProcessImage(CImage image)
        {
            Image<TPixel> processingImage;
            using (var imageStream = image.OpenStream())
            {
                processingImage = Image.Load<TPixel>(imageStream);
            }

            using var processedImageStream = new MemoryStream();
            using (processingImage)
            {
                processingImage.Mutate(c =>
                {
                    c.Contrast(image.Contrast).Brightness(image.Brightness);
                    foreach (var com in image.Commodities)
                    {
                        c.DrawText(_commoditiesLabels[com.Id], com.Font, com.LabelColor, com.Location);
                    }

                    c.Rotate(_rotationMode);
                });

                processingImage.SaveAsJpeg(processedImageStream);
            }

            processedImageStream.Position = 0;
            _imagesConsumer.OnNext(new ProcessedCImage(image, processedImageStream, _imagesPositions[image.Id]));
        }

        public void Process()
        {
            _commoditiesLabels = ImmutableDictionary<int, string>.Empty.AddRange(
                _images.SelectMany(i => i.Commodities)
                    .OrderBy(i => i.Position)
                    .Select((com, pos) => new KeyValuePair<int, string>(com.Id, pos.ToString())));
            Parallel.ForEach(_images, ProcessImage);
        }
    }
}