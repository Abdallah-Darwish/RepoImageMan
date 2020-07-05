using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using Avalonia;
using RepoImageMan;
using RepoImageMan.Processors;
namespace MainUI.Processors
{
    //If there is any new requirments just rewrite this
    class DirectoryImagesCatalogProcessor : ImagesCatalogProcessorBase, IObservable<(CImage Image, int Count)>
    {
        private readonly ISubject<(CImage Image, int Count)> _subj = new Subject<(CImage Image, int Count)>();
        private readonly PixelSize _maxImageSize;
        private readonly int _imageQuality;
        private readonly string _savingDir;

        public DirectoryImagesCatalogProcessor(ReadOnlyMemory<CImage> images, string savingDir, PixelSize? maxImageSize = default, int imageQuality = 75) : base(images, true)
        {
            if (maxImageSize.HasValue && maxImageSize?.Width <= 0 || maxImageSize?.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxImageSize), $"{nameof(maxImageSize)} must be > [0, 0].");
            }
            if (imageQuality <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(imageQuality), $"{nameof(imageQuality)} must be > 0.");
            }
            if (string.IsNullOrWhiteSpace(savingDir))
            {
                throw new ArgumentException($"{nameof(savingDir)} can't be null or empty.");
            }
            if (Directory.Exists(savingDir) == false)
            {
                throw new DirectoryNotFoundException($"Can't find directory {savingDir}.");
            }
            _maxImageSize = maxImageSize ?? new PixelSize(int.MaxValue, int.MaxValue);
            _imageQuality = imageQuality;
            _savingDir = savingDir;
        }



        protected override int GetImageQuality(CImage image) => _imageQuality;
        protected override PixelSize GetImageSize(CImage image) => new PixelSize(Math.Min(image.Size.Width, _maxImageSize.Width), Math.Min(image.Size.Height, _maxImageSize.Height));

        protected override Stream GetImageStream(CImage image, int pos)
        {
            string imagePath = Path.Combine(_savingDir, $"{pos}.jpg");
            if (File.Exists(imagePath)) { File.Delete(imagePath); }
            return new FileStream(imagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        }
        private volatile int _processedImagesCount = 0;
        protected override void OnImageProcessed(CImage image, int pos, Stream imageStream)
        {
            imageStream.SetLength(imageStream.Position);
            imageStream.Flush();
            imageStream.Dispose();
            var cnt = Interlocked.Increment(ref _processedImagesCount);
            _subj.OnNext((image, cnt));
        }
        protected override void OnCompleted() => _subj.OnCompleted();

        public IDisposable Subscribe(IObserver<(CImage Image, int Count)> observer) => _subj.Subscribe(observer);
    }
}
