using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using IBitmap = Avalonia.Media.Imaging.IBitmap;

namespace MainUI
{
    public static class Extensions
    {
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

        public static IBitmap LoadResizedBitmap(this Stream originalImageStream, Avalonia.Size sz)
        {
            using var img = SixLabors.ImageSharp.Image.Load(originalImageStream);
            img.Mutate(c => c.Resize(new SixLabors.Primitives.Size((int)sz.Width, (int)sz.Height)));
            using var resizedImageStream = new MemoryStream(img.Height * img.Width * (img.PixelType.BitsPerPixel / 8) + 20);
            img.SaveAsBmp(resizedImageStream);
            resizedImageStream.Position = 0;
            return new Avalonia.Media.Imaging.Bitmap(resizedImageStream);
        }
    }
}