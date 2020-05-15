using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Threading;
using IBitmap = Avalonia.Media.Imaging.IBitmap;
using Avalonia.Media.Imaging;
using System.Reflection.Metadata;

namespace MainUI
{
    public static class Extensions
    {
        public static Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> body, int degreeOfParallelism = 0)
        {
            if (degreeOfParallelism <= 0) { degreeOfParallelism = Environment.ProcessorCount; }
            var sourceEnum = source.GetEnumerator();
            var tasks = new Task[degreeOfParallelism];
            SpinLock sourceLock = new SpinLock(false);
            for (int i = 0; i < degreeOfParallelism; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    bool _ = false;
                    sourceLock.Enter(ref _);
                    T sourceItem;
                    try
                    {
                        if (sourceEnum.MoveNext() == false) { return; }
                        sourceItem = sourceEnum.Current;
                    }
                    finally { sourceLock.Exit(); }
                    await body(sourceItem).ConfigureAwait(false);
                });
            }
            return Task.WhenAll(tasks).ContinueWith(_ => sourceEnum.Dispose());
        }
        public static IBitmap Resize(this IBitmap bmp, Avalonia.Size sz)
        {
            using var imgStream = new MemoryStream(bmp.PixelSize.Height * bmp.PixelSize.Width);
            bmp.Save(imgStream);
            using var img = SixLabors.ImageSharp.Image.Load(imgStream);
            img.Mutate(c => c.Resize(new SixLabors.Primitives.Size((int)sz.Width, (int)sz.Height)));
            imgStream.Position = 0;
            img.SaveAsBmp(imgStream);
            imgStream.SetLength(imgStream.Position);
            imgStream.Position = 0;
            return new Avalonia.Media.Imaging.Bitmap(imgStream);
        }
        public static SixLabors.Primitives.PointF ToSixLabors(this in Avalonia.Point p) => new SixLabors.Primitives.PointF((float)p.X, (float)p.Y);
        public static SixLabors.Primitives.SizeF ToSixLabors(this in Avalonia.Size sz) => new SixLabors.Primitives.SizeF((float)sz.Width, (float)sz.Height);
        //TODO: reimplimint me without sixlabors or with WriteableBmp
        public static IBitmap LoadResizedBitmap(this Stream originalImageStream, Avalonia.PixelSize sz)
        {
            using var img = new Bitmap(originalImageStream);
            var res = new RenderTargetBitmap(sz);
            using var ctx = res.CreateDrawingContext(null);
            ctx.DrawImage(img.PlatformImpl, 1.0, new Avalonia.Rect(default, img.PixelSize.ToSize(1.0)), new Avalonia.Rect(default, sz.ToSize(1.0)));
            return res;
        }

        public static SixLabors.ImageSharp.Color ToSixLabors(this in Avalonia.Media.Color c) => SixLabors.ImageSharp.Color.FromRgba(c.R, c.G, c.B, c.A);
        public static Avalonia.Media.Color ToAvalonia(this in SixLabors.ImageSharp.Color c)
        {
            var cp = c.ToPixel<SixLabors.ImageSharp.PixelFormats.Rgba32>();
            return Avalonia.Media.Color.FromArgb(cp.A, cp.R, cp.G, cp.B);
        }

    }
}