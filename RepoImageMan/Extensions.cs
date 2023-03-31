using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SkiaSharp;

namespace RepoImageMan
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
                    while (true)
                    {
                        bool _ = false;
                        sourceLock.Enter(ref _);
                        T sourceItem;
                        try
                        {
                            if (!sourceEnum.MoveNext()) { return; }
                            sourceItem = sourceEnum.Current;
                        }
                        finally { sourceLock.Exit(); }
                        await body(sourceItem).ConfigureAwait(false);
                    }
                });
            }
            return Task.WhenAll(tasks).ContinueWith(_ => sourceEnum.Dispose());
        }
        public static Avalonia.PixelSize ToAvalonia(this SixLabors.ImageSharp.Size sz) => new(sz.Width, sz.Height);
        public static SixLabors.ImageSharp.PointF ToSixLaborsPointF(this System.Drawing.Point p) => new(p.X, p.Y);

        public static SixLabors.ImageSharp.Size ToSize(this SixLabors.ImageSharp.SizeF sz) => (SixLabors.ImageSharp.Size)sz;
        public static SixLabors.ImageSharp.PointF Scale(this SixLabors.ImageSharp.PointF p, SixLabors.ImageSharp.SizeF scale)
            => new SixLabors.ImageSharp.PointF(p.X * scale.Width, p.Y * scale.Height);

        public static Avalonia.Point Scale(this in Avalonia.Point p, in Avalonia.Size scale)
            => new(p.X * scale.Width, p.Y * scale.Height);

        public static SixLabors.ImageSharp.Point Scale(this SixLabors.ImageSharp.Point p, SixLabors.ImageSharp.SizeF scale)
            => new SixLabors.ImageSharp.Point((int)(p.X * scale.Width), (int)(p.Y * scale.Height));

        public static float Average(this SixLabors.ImageSharp.SizeF sz) => (sz.Height + sz.Width) / 2f;
        public static double Average(this in Avalonia.Size sz) => (sz.Height + sz.Width) / 2.0;

        public static SixLabors.ImageSharp.Size ToSixLabors(this in Avalonia.PixelSize sz) => new(sz.Width, sz.Height);
        public static SixLabors.ImageSharp.SizeF ToSixLabors(this in Avalonia.Size sz) => new((float)sz.Width, (float)sz.Height);


        public static Font WithSize(this Font f, float sz) => new(f!.FamilyName, sz, f.Style);

        public static Font Scale(this Font f, float scale) => f.WithSize(f.Size * scale);

        public static void Invoke(this Avalonia.Threading.Dispatcher d, Action a)
        {
            if (d.CheckAccess()) { a(); }
            else { d.Post(a); }
        }

        internal static SKFontStyle ToSK(this FontStyle s) => (s) switch
        {
            FontStyle.Regular => SKFontStyle.Normal,
            FontStyle.Bold => SKFontStyle.Bold,
            FontStyle.Italic => SKFontStyle.Italic,
            FontStyle.Bold | FontStyle.Italic => SKFontStyle.BoldItalic,
            _ => throw new ArgumentOutOfRangeException(nameof(s))
        };
    }
}
