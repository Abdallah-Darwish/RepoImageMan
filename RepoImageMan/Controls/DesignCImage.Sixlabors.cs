using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Avalonia.Media.Imaging;
using System;
using SixLabors.ImageSharp.Processing;
using TPixel = SixLabors.ImageSharp.PixelFormats.Rgba32;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using Avalonia.Threading;

namespace RepoImageMan.Controls
{
    partial class DesignCImage
    {
        /// <summary>
        /// Contains the original image stored in <see cref="SixLabors.ImageSharp.Image"/> stream with no mods at all.
        /// </summary>
        private Image<TPixel> _bmp;

        private IBitmap _resizedBmp, _modedBmp;

        private void ApplyContrastBrightness()
        {
            _modedBmp?.Dispose();
            using var modBmp = _bmp.Clone(c => c.Contrast(Image.Contrast)
                                                 .Brightness(Image.Brightness)
                                                 .Resize((int)InstanceSize.Width, (int)InstanceSize.Height));
            var modBmpMem = modBmp.GetPixelRowMemory(0);
            var newBmp = new WriteableBitmap(new Avalonia.PixelSize(modBmp.Width, modBmp.Height), default, Avalonia.Platform.PixelFormat.Rgba8888);
            using (var newBmpBuffer = newBmp.Lock())
            {
                Span<TPixel> newBmpSpan, modBmpSpan;
                unsafe
                {
                    fixed (TPixel* modBmpBuf = modBmpMem.Span)
                    {
                        modBmpSpan = new Span<TPixel>(modBmpBuf, modBmp.Height * modBmp.Width);
                        newBmpSpan = new Span<TPixel>(newBmpBuffer.Address.ToPointer(), newBmpBuffer.RowBytes * newBmp.PixelSize.Height);

                        modBmpSpan.CopyTo(newBmpSpan);
                    }
                }
            }
            _modedBmp = newBmp;
        }
        private void ResizeBmp()
        {
            _resizedBmp?.Dispose();
            var newBmp = new RenderTargetBitmap(new Avalonia.PixelSize((int)InstanceSize.Width, (int)InstanceSize.Height));
            using var ctx = newBmp.CreateDrawingContext(null);

            ctx.DrawBitmap(_modedBmp.PlatformImpl, 1.0,
                new Avalonia.Rect(new Avalonia.Point(), _modedBmp.PixelSize.ToSize(1.0)),
                new Avalonia.Rect(new Avalonia.Point(), newBmp.PixelSize.ToSize(1.0)));
            _resizedBmp = newBmp;
        }
        private void UpdateBmp(CImage _)
        {
            using (var newBmpStream = Image.OpenStream())
            {
                _bmp = SixLabors.ImageSharp.Image.Load<TPixel>(newBmpStream);
            }
            ApplyContrastBrightness();
            ResizeBmp();
            SafeInvalidate();
        }
    }
}
