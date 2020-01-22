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
                        Name = "Package Database"
                    }
                }
            };
            var dbPath = (await dbOfd.ShowAsync(this)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dbPath) || File.Exists(dbPath) == false)
            {
                MessageBoxManager.GetMessageBoxStandardWindow("Error", "Invalid package database path.", ButtonEnum.Ok,
                    MBIcon.Error);
            }
            else
            {
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
                            Name = "Package Archive"
                        }
                    }
                };
                var archivePath = (await archiveOfd.ShowAsync(this)).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(archivePath) || File.Exists(archivePath) == false)
                {
                    MessageBoxManager.GetMessageBoxStandardWindow("Error", "Invalid package archive path.",
                        ButtonEnum.Ok,
                        MBIcon.Error);
                }
                else
                {
                    //Open Pack
                }
            }
        }

        private async void BtnCreatePack_Click(object? sender, RoutedEventArgs e)
        {
            var dbSfd = new SaveFileDialog
            {
                Title = "Database file.",
                DefaultExtension = CommodityPackage.DbExtension,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter
                    {
                        Extensions = new List<string>
                        {
                            CommodityPackage.DbExtension
                        },
                        Name = "Package Database"
                    }
                }
            };
            var dbPath = await dbSfd.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                MessageBoxManager.GetMessageBoxStandardWindow("Error", "Invalid package database path.",
                    ButtonEnum.Ok,
                    MBIcon.Error);
            }
            else
            {
                var archiveSfd = new SaveFileDialog
                {
                    Title = "Archive file.",
                    DefaultExtension = CommodityPackage.ArchiveExtension,
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter
                        {
                            Extensions = new List<string>
                            {
                                CommodityPackage.ArchiveExtension
                            },
                            Name = "Package Archive"
                        }
                    }
                };
                var archivePath = await archiveSfd.ShowAsync(this);
                if (string.IsNullOrWhiteSpace(archivePath))
                {
                    MessageBoxManager.GetMessageBoxStandardWindow("Error", "Invalid package archive path.",
                        ButtonEnum.Ok,
                        MBIcon.Error);
                }
                else
                {
                    //Create Pack
                }
            }
        }

        private void BtnSettings_Click(object? sender, RoutedEventArgs e)
        {
            btnSettings.Content = "NOT IMPLEMENTED YET!";
        }
    }
}