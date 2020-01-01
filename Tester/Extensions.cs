using System.IO;

namespace Tester
{
    static class Extensions
    {
        public static System.Windows.Media.Imaging.BitmapImage ToWPF(this SixLabors.ImageSharp.Image img)
        {
            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
            using (var imgStream = new MemoryStream(img.Height * img.Width * (img.PixelType.BitsPerPixel / 8) + 3000))
            {
                img.Save(imgStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                imgStream.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = imgStream;
                bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }

        public static SixLabors.Primitives.SizeF ToSixLabors(this System.Windows.Size sz) => new SixLabors.Primitives.SizeF((float)sz.Width, (float)sz.Height);
        public static SixLabors.Primitives.PointF ToSixLabors(this System.Windows.Point p) => new SixLabors.Primitives.PointF((float)p.X, (float)p.Y);
    }
}
