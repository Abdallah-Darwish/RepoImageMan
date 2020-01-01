using RepoImageMan;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Linq;
using System.Windows;

namespace Tester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string BaseImagePath = @"C:\Users\abdal\Desktop\BaseImage.jpg";
        const string dbPath = @"C:\Users\abdal\Desktop\imagesDb.sqlite";
        const string PackagePath = @"C:\Users\abdal\Desktop\imagesPackage.zip";
        const string HandleImagePath = @"C:\Users\abdal\Desktop\Arrows.png";
        const string RenderedImagePath = @"C:\Users\abdal\Desktop\RE.jpg";
        public MainWindow()
        {
            InitializeComponent();
        }

        DesignCImage<Rgba32>? _image;
        CommodityPackage? _package;
        private async void btnCreatePackage_Click(object sender, RoutedEventArgs e)
        {
            _package?.Dispose();
            using var img = Image.Load<Rgba32>(BaseImagePath);
            using var pack = await CommodityPackage.Create(dbPath, PackagePath);
            var packImage = await pack.AddImage();
            using (var pImageStream = packImage.OpenStream())
            {
                img.SaveAsJpeg(pImageStream);
            }
            packImage.Refresh();
            var com = await pack.AddCommodity();
            com.Name = "Adam";
            await com.Save();
            for (int i = 0; i < 10; i++)
            {
                await pack.AddCommodity();
            }
            var imgCom = await packImage.AddCommodity();
            imgCom.Name = "Image commodity";
            imgCom.Font = imgCom.Font.Family.CreateFont(400f);
            imgCom.Location = new SixLabors.Primitives.PointF(100f, 100f);
            await imgCom.Save();
            await packImage.Save();
        }

        private async void btnBindFirstImage_Click(object sender, RoutedEventArgs e)
        {
            _package!.Images[0].TryDesign(out _image, new SixLabors.Primitives.Size((int)imgBox.Width, (int)imgBox.Height), Image.Load<Rgba32>(HandleImagePath));
            _image!.ImageUpdated += Image_ImageUpdated;
            Image_ImageUpdated(_image!);
        }

        private void Image_ImageUpdated(DesignCImage<Rgba32> sender)
        {
            imgBox.Source = sender.RenderedImage.ToWPF();
        }

        private async void btnOpenPackage_Click(object sender, RoutedEventArgs e)
        {
            _package = await CommodityPackage.Open(dbPath, PackagePath);
        }

        private void imgBox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_image == null) { return; }
            var p = e.GetPosition(imgBox).ToSixLabors();
            var x = _image.FirstOnPoint(p);
            if (x != null)
            {
                //MessageBox.Show($"Its in commodity {x.Commodity.Name}", "Hit", MessageBoxButton.OK, MessageBoxImage.Information);
                x.IsSurrounded = true;
            }
            var y = _image.Commodities.FirstOrDefault(c => c.IsInHandle(p));

            if (y != null)
            {
                MessageBox.Show($"Its in commoditie's {y.Commodity.Name} handle", "Hit", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnSaveCustom_Click(object sender, RoutedEventArgs e)
        {
            if (_image == null) { return; }
            var img = _image.RenderedImage.Clone();
            img.Mutate(c => c.Brightness(1.3f));
            img.Save(RenderedImagePath);
        }
    }
}
