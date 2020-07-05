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
using System.Net.WebSockets;
using System.Threading.Tasks;

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
            Parallel.ForEach(System.IO.Directory.GetFiles(dir, "*.jpg"), f =>
            {
                var nf = Path.ChangeExtension(f, ".bmp");
                using (var img = SixLabors.ImageSharp.Image.Load(f))
                using (var fs = new FileStream(nf, FileMode.Create, FileAccess.ReadWrite))
                {
                    img.SaveAsBmp(fs);
                }
                File.Delete(f);
            });
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
