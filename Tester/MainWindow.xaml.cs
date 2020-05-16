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
using RepoImageMan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Image = Avalonia.Controls.Image;

namespace Tester
{
    public class MainWindow : Window
    {
        private readonly static string RepoFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "RepoFiles");
        private Image img;
        private Button btnCreatePackage, btnOpenPackage, btnBindImage;
        private ContentControl txtInfo;
        private CommodityPackage? _package;

        private readonly SixLabors.ImageSharp.Image<Rgba32> _handleImage =
            SixLabors.ImageSharp.Image.Load<Rgba32>($@"{RepoFiles}\Arrows1.png");

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
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void CreatePackage(object? sender, RoutedEventArgs e)
        {
            await OldDbConverter.Convert($@"{RepoFiles}\OldRepo\Repository\Commodities.json",
                $@"{RepoFiles}\OldRepo\Repository\Images.json",
                $@"{RepoFiles}\OldRepo\Repository\cat",
                $@"{RepoFiles}\NewRepo",
                10);
        }

        private async void OpenPackage(object? sender, RoutedEventArgs e)
        {
            _package = await CommodityPackage.TryOpen($@"{RepoFiles}\NewRepo");
        }

        private void BindImage(object? sender, RoutedEventArgs e)
        {
           
        }

        private void OnImageClicked(object? sender, PointerPressedEventArgs e)
        {
            
        }
    }
}