using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using IBitmap = Avalonia.Media.Imaging.IBitmap;

namespace MainUI
{
    public static class Extensions
    {
        public static SixLabors.ImageSharp.PointF ToSixLabors(this in Avalonia.Point p) => new((float)p.X, (float)p.Y);
        public static SixLabors.ImageSharp.PointF ToSixLabors(this in Avalonia.Size sz) => new((float)sz.Width, (float)sz.Height);
        public static IBitmap LoadResizedBitmap(this Stream originalImageStream, Avalonia.PixelSize sz)
        {
            using var img = new Bitmap(originalImageStream);
            var res = new RenderTargetBitmap(sz);
            using var ctx = res.CreateDrawingContext(null);
            ctx.DrawBitmap(img.PlatformImpl, 1.0, new Avalonia.Rect(default, img.PixelSize.ToSize(1.0)), new Avalonia.Rect(default, sz.ToSize(1.0)));
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