using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using System;

namespace RepoImageMan
{
    public static class Extensions
    {
        public static Avalonia.PixelSize ToAvalonia(this SixLabors.Primitives.Size sz) => new Avalonia.PixelSize(sz.Width, sz.Height);
        public static SixLabors.Primitives.PointF ToSixLaborsPointF(this System.Drawing.Point p) => new SixLabors.Primitives.PointF(p.X, p.Y);

        public static SixLabors.Primitives.Size ToSize(this SixLabors.Primitives.SizeF sz) => (SixLabors.Primitives.Size)sz;
        public static SixLabors.Primitives.PointF Scale(this SixLabors.Primitives.PointF p, SixLabors.Primitives.SizeF scale)
            => new SixLabors.Primitives.PointF(p.X * scale.Width, p.Y * scale.Height);

        public static Avalonia.Point Scale(this in Avalonia.Point p, in Avalonia.Size scale)
            => new Avalonia.Point(p.X * scale.Width, p.Y * scale.Height);

        public static SixLabors.Primitives.Point Scale(this SixLabors.Primitives.Point p, SixLabors.Primitives.SizeF scale)
            => new SixLabors.Primitives.Point((int)(p.X * scale.Width), (int)(p.Y * scale.Height));

        public static float Average(this SixLabors.Primitives.SizeF sz) => (sz.Height + sz.Width) / 2f;
        public static double Average(this in Avalonia.Size sz) => (sz.Height + sz.Width) / 2.0;

        public static SixLabors.Primitives.Size ToSixLabors(this in Avalonia.PixelSize sz) => new SixLabors.Primitives.Size(sz.Width, sz.Height);
        public static SixLabors.Primitives.SizeF ToSixLabors(this in Avalonia.Size sz) => new SixLabors.Primitives.SizeF((float)sz.Width, (float)sz.Height);

        public static WriteableBitmap ToAvalonia(this Image img)
        {
            using var rgbaImg = img.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
            var renderedImagePixels = System.Runtime.InteropServices.MemoryMarshal.AsBytes(rgbaImg.GetPixelSpan());

            var bmp = new WriteableBitmap(img.Size().ToAvalonia(), default, Avalonia.Platform.PixelFormat.Rgba8888);
            using var bmpBuffer = bmp.Lock();

            Span<byte> bmpBufferSpan;
            unsafe
            {
                bmpBufferSpan = new Span<byte>(bmpBuffer.Address.ToPointer(), bmpBuffer.Size.Height * bmpBuffer.RowBytes);
            }

            renderedImagePixels.CopyTo(bmpBufferSpan);
            return bmp;
        }
    }
}
