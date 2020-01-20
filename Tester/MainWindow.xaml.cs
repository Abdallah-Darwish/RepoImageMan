using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Direct2D1;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using JetBrains.Annotations;
using RepoImageMan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Image = Avalonia.Controls.Image;

namespace Tester
{
    public class MainWindow : Window
    {
        private Image img;
        private Button btnCreatePackage, btnOpenPackage, btnBindImage;
        private ContentControl txtInfo;
        private CommodityPackage? _package;
        private DesignCImage<Rgba32>? _image;

        private readonly SixLabors.ImageSharp.Image<Rgba32> _handleImage =
            SixLabors.ImageSharp.Image.Load<Rgba32>(@"/home/abdullah/Desktop/RepoImageMan/Documents/Arrows1.png");

        public MainWindow()
        {
            InitializeComponent();

            img = this.Get<Image>("img");
            btnCreatePackage = this.Get<Button>("btnCreatePackage");
            btnOpenPackage = this.Get<Button>("btnOpenPackage");
            btnBindImage = this.Get<Button>("btnBindImage");
            txtInfo = this.Get<ContentControl>("txtInfo");
            btnCreatePackage.Click += CreatePackage;
            btnOpenPackage.Click += OpenPackage;
            btnBindImage.Click += BindImage;
            img.PointerPressed += OnImageClicked;
            {
                var ops = new JsonSerializerOptions {Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)};
                var x = JsonSerializer.Deserialize<OCImage[]>(
                    File.ReadAllText(@"/home/abdullah/Desktop/repo/Images.json"),
                    ops);
                txtInfo.Content =
                    $"{{{string.Join(", ", x.GroupBy(a => a.QualityLevel).Select(a => $"{a.Key}: {a.Count()}"))}}}";
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void CreatePackage([CanBeNull] object sender, RoutedEventArgs e)
        {
            await OldDbConverter.Convert(@"/home/abdullah/Desktop/repo/Commodities.json",
                @"/home/abdullah/Desktop/repo/Images.json",
                @"/home/abdullah/Desktop/repo/cat/",
                @"/home/abdullah/Desktop/new_repo/db.sqlite",
                @"/home/abdullah/Desktop/new_repo/pkg.zip",
                10);
        }

        private async void OpenPackage(object? sender, RoutedEventArgs e)
        {
            _package = await CommodityPackage.Open(@"/home/abdullah/Desktop/new_repo/db.sqlite",
                @"/home/abdullah/Desktop/new_repo/pkg.zip", _handleImage);
        }

        private void BindImage(object? sender, RoutedEventArgs e)
        {
            _image?.Dispose();
            var rand = new Random();
            var sz = new SixLabors.Primitives.Size((int) img.Width, (int) img.Height);
            var bindImage = _package.Images[rand.Next(_package.Images.Count)];
            bindImage.TryDesign(out _image, sz);
            _image.ImageUpdated += OnImageUpdated;
            OnImageUpdated(_image);
        }

        private void OnImageUpdated(DesignCImage<Rgba32> sender)
        {
            using var ms = new MemoryStream();
            sender.RenderedImage.SaveAsBmp(ms);
            ms.Position = 0;
            img.Source = new Bitmap(ms);
        }

        private void OnImageClicked(object? sender, PointerPressedEventArgs e)
        {
            if (_image == null)
            {
                return;
            }

            var p = e.GetPosition(img);
            var sc = _image.FirstOnPoint(new PointF((float) p.X, (float) p.Y));
            string con = "";
            if (sc == null)
            {
                con = "On none";
            }
            else
            {
                con = $"On Commodity{sc.Commodity.Name}";
                sc.IsSurrounded = true;
            }

            txtInfo.Content = con;
        }
    }
}