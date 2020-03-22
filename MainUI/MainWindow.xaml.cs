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

namespace MainUI
{
    public class MainWindow : Window
    {
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        private readonly Button btnOpenPack, btnCreatePack, btnSettings;

        public MainWindow()
        {
            InitializeComponent();
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
            var p = await CommodityPackage.Open(@"/home/abdullah/Desktop/repo_files/new_repo/db.sqlite",
                @"/home/abdullah/Desktop/repo_files/new_repo/pkg.zip");

            var ein = new CommodityImageWindow(p);
            ein.Closed += (o, args) => p.Dispose();
            ein.Show();
            btnSettings.Content = "NOT IMPLEMENTED YET!";
        }
    }
}