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
using MainUI.Controls;
using Avalonia.Media;

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

            var p = await CommodityPackage.TryOpen(folderPath);
            if(p == null)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.Ok,
                    CanResize = false,
                    ContentHeader = "Package Already Open",
                    ContentTitle = "Error",
                    Icon = MBIcon.Error,
                    ContentMessage = $"Can't open {folderPath} because this package is already open in another application.{Environment.NewLine}If you are sure its not then manually delete file {CommodityPackage.GetPackageLockPath(folderPath)} and re-try.",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInCenter = true
                }).ShowDialog(this);
                return;
            }
            var win = new CommodityImageWindow(p);
            win.Closed += (_, __) => p.Dispose();
            win.Show();
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

            //var p = await CommodityPackage.Open($@"{RepoFiles}\NewRepo");

            //var din = new CommodityImageWindow(p);
            //await din.ShowDialog(this);
            btnSettings.Content = "NOT IMPLEMENTED YET!";
            //p.Dispose();
            //GC.Collect();
        }
    }
}