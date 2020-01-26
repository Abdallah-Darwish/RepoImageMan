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
            var dbOfd = new OpenFileDialog
            {
                Title = "Database file.",
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Extensions = new List<string>
                        {
                            CommodityPackage.DbExtension
                        },
                        Name = $"Package Database({CommodityPackage.DbExtension})"
                    }
                }
            };
            var dbPath = (await dbOfd.ShowAsync(this))?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                return;
            }

            var archiveOfd = new OpenFileDialog
            {
                Title = "Archive file.",
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Extensions = new List<string>
                        {
                            CommodityPackage.ArchiveExtension
                        },
                        Name = $"Package Archive({CommodityPackage.ArchiveExtension})"
                    }
                }
            };
            var archivePath = (await archiveOfd.ShowAsync(this))?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return;
            }

            //Open Pack
        }

        private async void BtnCreatePack_Click(object? sender, RoutedEventArgs e)
        {
            var dbSfd = new SaveFileDialog
            {
                Title = "Database file",
                DefaultExtension = CommodityPackage.DbExtension,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Extensions = new List<string>
                        {
                            CommodityPackage.DbExtension
                        },
                        Name = $"Package Database({CommodityPackage.DbExtension})"
                    }
                }
            };
            var dbPath = await dbSfd.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                return;
            }

            var archiveSfd = new SaveFileDialog
            {
                Title = "Archive file",
                DefaultExtension = CommodityPackage.ArchiveExtension,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Extensions = new List<string>
                        {
                            CommodityPackage.ArchiveExtension
                        },
                        Name = $"Package Archive({CommodityPackage.ArchiveExtension})"
                    }
                }
            };
            var archivePath = await archiveSfd.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return;
            }

            //Create Pack
        }

        private async void BtnSettings_Click(object? sender, RoutedEventArgs e)
        {
            var p = await CommodityPackage.Open(@"/home/abdullah/Desktop/new_repo/db.sqlite",
                @"/home/abdullah/Desktop/new_repo/pkg.zip");

            CommodityImageWindow ein = new CommodityImageWindow(p);
            ein.Show();
            btnSettings.Content = "NOT IMPLEMENTED YET!";
        }
    }
}