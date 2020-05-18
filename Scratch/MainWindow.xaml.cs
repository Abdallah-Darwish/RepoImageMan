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

namespace Scratch
{
    public class MainWindow : Window
    {
        private readonly Canvas grd;
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
           
        }

        private void GO(object sender, RoutedEventArgs e)
        {
            const string dir = @"C:\Users\Darwish\Desktop\xxx";
            foreach (var p in Directory.GetFiles(dir, "*.jpg").OrderBy(p => p))
            {
                using var img = SixLabors.ImageSharp.Image.Load(p);
                if(img.Width < img.Height)
                {
                    img.Mutate(c => c.Rotate(RotateMode.Rotate270));
                    img.Save(p);
                    break;
                }
            }

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
