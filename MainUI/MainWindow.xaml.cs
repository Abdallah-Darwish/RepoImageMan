using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia.DTO;
using RepoImageMan;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using MBIcon = MessageBox.Avalonia.Enums.Icon;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MainUI
{
    public class MainWindow : Window
    {
        private readonly static string RepoFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "RepoFiles");
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        private readonly Button btnOpenPack, btnCreatePack, btnSettings;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            btnOpenPack = this.Get<Button>(nameof(btnOpenPack));
            btnCreatePack = this.Get<Button>(nameof(btnCreatePack));
            btnSettings = this.Get<Button>(nameof(btnSettings));
        }

        private async void BtnOpenPack_Click(object? sender, RoutedEventArgs e)
        {
            var folderOfd = new OpenFolderDialog { Title = "Package Folder" };
            var folderPath = await folderOfd.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(folderPath)) { return; }

            //Open Pack
        }

        private async void BtnCreatePack_Click(object? sender, RoutedEventArgs e)
        {
            var folderOfd = new OpenFolderDialog { Title = "Package Folder" };
            var folderPath = await folderOfd.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(folderPath)) { return; }
            //Create Pack
        }

        private async void BtnSettings_Click(object? sender, RoutedEventArgs e)
        {
        
            var p = await CommodityPackage.Open($@"{RepoFiles}\NewRepo", SixLabors.ImageSharp.Image.Load($@"{RepoFiles}\Arrows1.png"));

            //var ein = new CommodityImageWindow(p);
            //ein.Closed +=  (o, args) => p.Dispose();
            //ein.Show();
            var rand = new Random();
            var images = p.Images.Where(i => i.Commodities.Any()).ToArray();
            images[rand.Next(images.Length)].TryDesign<SixLabors.ImageSharp.PixelFormats.Rgba32>(out var img);
            var din = new DesigningWindow(img!);
            await din.ShowDialog(this);
            btnSettings.Content = "NOT IMPLEMENTED YET!";
            p.Dispose();
        }
    }
}