using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Linq;
using SkiaSharp;
namespace Scratch
{
    public class MainWindow : Window
    {
        private readonly Button btn;
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            btn = this.FindControl<Button>(nameof(btn));
           
        }

        private void GO(object sender, RoutedEventArgs e)
        {
            const string dir = @"C:\Users\Darwish\Desktop\RepoFiles\NewRepo";
            var info = new SKImageInfo(256, 256);
            using (var surface = SKSurface.Create(info))
            {
                SKCanvas canvas = surface.Canvas;

                canvas.Clear(SKColors.White);

                // configure our brush
                var redBrush = new SKPaint
                {
                    Color = new SKColor(0xff, 0, 0),
                    IsStroke = true
                };
                var blueBrush = new SKPaint
                {
                    Color = new SKColor(0, 0, 0xff),
                    IsStroke = true
                };

                for (int i = 0; i < 64; i += 8)
                {
                    var rect = new SKRect(i, i, 256 - i - 1, 256 - i - 1);
                    canvas.DrawRect(rect, (i % 16 == 0) ? redBrush : blueBrush);
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
