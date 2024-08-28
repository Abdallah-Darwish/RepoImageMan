using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MainUI.Processors;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using OfficeOpenXml;
using RepoImageMan;

namespace MainUI
{
    public partial class CommodityImageWindow
    {
        public class OtherTab
        {
            private readonly TextBox txtExportCatalogPath, txtExportExcelPath;
            private readonly CheckBox chkRotateCatalog, chkExportCost, chkExportWholePrice, chkExportCashPrice, chkExportPartialPrice;
            private readonly NumericUpDown nudMaxImageWidth, nudMaxImageHeight, nudImageQuality;
            private readonly Button btnBrowseExportCatalog, btnBrowseExportExcel, btnExportCatalog, btnExportExcel/*, btnTidyPackage*/;
            private readonly ProgressBar pbOtherProgress;
            private readonly CommodityImageWindow _hostingWindow;

            private readonly InputElement[] _inputControls;
            public OtherTab(CommodityImageWindow hostingWindow)
            {
                _hostingWindow = hostingWindow;

                txtExportCatalogPath = _hostingWindow.FindControl<TextBox>(nameof(txtExportCatalogPath));
                txtExportExcelPath = _hostingWindow.FindControl<TextBox>(nameof(txtExportExcelPath));

                chkRotateCatalog = _hostingWindow.FindControl<CheckBox>(nameof(chkRotateCatalog));
                chkExportCost = _hostingWindow.FindControl<CheckBox>(nameof(chkExportCost));
                chkExportWholePrice = _hostingWindow.FindControl<CheckBox>(nameof(chkExportWholePrice));
                chkExportCashPrice = _hostingWindow.FindControl<CheckBox>(nameof(chkExportCashPrice));
                chkExportPartialPrice = _hostingWindow.FindControl<CheckBox>(nameof(chkExportPartialPrice));

                nudMaxImageWidth = _hostingWindow.FindControl<NumericUpDown>(nameof(nudMaxImageWidth));
                nudMaxImageHeight = _hostingWindow.FindControl<NumericUpDown>(nameof(nudMaxImageHeight));
                nudImageQuality = _hostingWindow.FindControl<NumericUpDown>(nameof(nudImageQuality));

                btnBrowseExportCatalog = _hostingWindow.FindControl<Button>(nameof(btnBrowseExportCatalog));
                btnBrowseExportExcel = _hostingWindow.FindControl<Button>(nameof(btnBrowseExportExcel));
                btnExportCatalog = _hostingWindow.FindControl<Button>(nameof(btnExportCatalog));
                btnExportExcel = _hostingWindow.FindControl<Button>(nameof(btnExportExcel));
                //btnTidyPackage = _hostingWindow.FindControl<Button>(nameof(btnTidyPackage));

                pbOtherProgress = _hostingWindow.FindControl<ProgressBar>(nameof(pbOtherProgress));

                _inputControls = new InputElement[]
                {
                    txtExportCatalogPath, txtExportExcelPath,
                    chkExportCost, chkExportPartialPrice, chkExportWholePrice, chkRotateCatalog,
                    nudImageQuality, nudMaxImageHeight, nudMaxImageWidth,
                    btnBrowseExportCatalog, btnBrowseExportExcel, btnExportCatalog, btnExportExcel,
                    _hostingWindow.tabs
                };

                btnBrowseExportCatalog.Click += BtnBrowseExportCatalog_Click;
                btnExportCatalog.Click += BtnExportCatalog_Click;

                btnBrowseExportExcel.Click += BtnBrowseExportExcel_Click;
                btnExportExcel.Click += BtnExportExcel_Click;

                //btnTidyPackage.Click += BtnTidyPackage_Click;
            }

            //private async void BtnTidyPackage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            //{
            //    DisableInputs();
            //    try
            //    {
            //        await _hostingWindow._package.Tidy();
            //    }
            //    finally
            //    {
            //        EnableInputs();
            //    }
            //}

            private async void BtnExportExcel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            {
                DisableInputs();
                try
                {
                    var filePath = txtExportExcelPath.Text;

                    try
                    {
                        if (File.Exists(filePath)) { File.Delete(filePath); }
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            CanResize = false,
                            ContentTitle = "Error",
                            ContentHeader = "Inavlid Path",
                            Icon = MessageBox.Avalonia.Enums.Icon.Error,
                            ContentMessage = $"Path \"{filePath}\" is invalid.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ShowInCenter = true
                        }).ShowDialog(_hostingWindow);
                        return;
                    }
                    using var pack = new ExcelPackage(new FileInfo(filePath));
                    using var ws = pack.Workbook.Worksheets.Add("Prices");
                    var coms = _hostingWindow._package.Commodities.Where(c => c.IsExported).OrderBy(c => c.Position).ToArray();


                    pbOtherProgress.Maximum = coms.Length;
                    pbOtherProgress.Value = 0;
                    ws.View.RightToLeft = true;

                    bool exCost = chkExportCost.IsChecked ?? false,
                      exWhole = chkExportWholePrice.IsChecked ?? false,
                      exPartial = chkExportPartialPrice.IsChecked ?? false,
                      exCash = chkExportCashPrice.IsChecked ?? false;

                    int col = 1, row = 1, pos = 0;
                    ws.Cells[row, col++].Value = "الرقم";
                    ws.Cells[row, col++].Value = "الإسم";
                    if (exCost)
                    {
                        ws.Cells[row, col++].Value = "التكلفة";
                    }
                    if (exWhole)
                    {
                        ws.Cells[row, col++].Value = "سعر الجملة";
                    }
                    if (exCash)
                    {
                        ws.Cells[row, col++].Value = "سعر الكاش";
                    }
                    if (exPartial)
                    {
                        ws.Cells[row, col++].Value = "سعر التفرقة";
                    }

                    foreach (var com in coms)
                    {
                        col = 1;
                        row++;
                        pos++;
                        ws.Cells[row, col++].Value = pos;
                        ws.Cells[row, col++].Value = com.Name;
                        if (exCost)
                        {
                            ws.Cells[row, col++].Value = com.Cost;
                        }
                        if (exWhole)
                        {
                            ws.Cells[row, col++].Value = com.WholePrice;
                        }
                        if (exCash)
                        {
                            ws.Cells[row, col++].Value = com.CashPrice;
                        }
                        if (exPartial)
                        {
                            ws.Cells[row, col++].Value = com.PartialPrice;
                        }
                        pbOtherProgress.Value++;
                    }
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();
                    await pack.SaveAsync();

                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        CanResize = false,
                        ContentHeader = "Done Exporting",
                        ContentTitle = "Done",
                        Icon = MessageBox.Avalonia.Enums.Icon.Info,
                        ContentMessage = $"Finished exporting {_hostingWindow._package.Commodities.Count} commodities.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        ShowInCenter = true
                    }).ShowDialog(_hostingWindow);

                    pbOtherProgress.Value = 0;
                }
                finally
                {
                    EnableInputs();
                }
            }


            private async void BtnBrowseExportExcel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            {
                var fileSfd = new SaveFileDialog
                {
                    DefaultExtension = "xlsx",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter{Extensions = new List<string>{ "xlsx" }, Name = "Excel Worksheet"}
                    },
                    Title = "Excel File",
                };
                var filePath = await fileSfd.ShowAsync(_hostingWindow);
                txtExportExcelPath.Text = filePath is null ? "" : filePath;
            }

            private void DisableInputs()
            {
                foreach (var ipt in _inputControls)
                {
                    ipt.IsEnabled = false;
                }
            }
            private void EnableInputs()
            {
                foreach (var ipt in _inputControls)
                {
                    ipt.IsEnabled = true;
                }
            }

            private async void BtnExportCatalog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            {
                async Task Finish()
                {
                    pbOtherProgress.Value = pbOtherProgress.Maximum;
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        CanResize = false,
                        ContentHeader = "Done Exporting",
                        ContentTitle = "Done",
                        Icon = MessageBox.Avalonia.Enums.Icon.Info,
                        ContentMessage = $"Finished exporting {_hostingWindow._package.Images.Count} images.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        ShowInCenter = true
                    }).ShowDialog(_hostingWindow);
                    GC.Collect();
                    pbOtherProgress.Value = 0;
                }
                DisableInputs();
                try
                {
                    var folderPath = txtExportCatalogPath.Text;
                    try
                    {
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }
                    }
                    catch
                    {
                        await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            CanResize = false,
                            ContentTitle = "Error",
                            ContentHeader = "Inavlid Directory",
                            Icon = MessageBox.Avalonia.Enums.Icon.Error,
                            ContentMessage = $"Directory \"{folderPath}\" is invalid.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ShowInCenter = true
                        }).ShowDialog(_hostingWindow);
                        return;
                    }
                    void Export()
                    {
                        var proc = new DirectoryImagesCatalogProcessor(_hostingWindow._package.Images.ToArray(), folderPath, new PixelSize((int)nudMaxImageWidth.Value, (int)nudMaxImageHeight.Value), (int)nudImageQuality.Value, chkRotateCatalog.IsChecked ?? false);
                        using var procEventsSub = proc.Do(_ =>
                        {
                            Dispatcher.UIThread.Invoke(() => pbOtherProgress.Value++);
                        })
                            .Finally(async () => await Dispatcher.UIThread.InvokeAsync(Finish)).Subscribe();
                        proc.Start();
                    }
                    pbOtherProgress.Maximum = _hostingWindow._package.Images.Count;
                    pbOtherProgress.Value = 0;

                    ///Had to do it like this, so Parallel.ForEcah inside <see cref="RepoImageMan.Processors.ImagesCatalogProcessorBase"/> doesn't block the UI thread.
                    await Task.Factory.StartNew(Export, TaskCreationOptions.LongRunning);
                }
                finally
                {
                    EnableInputs();
                }
            }


            private async void BtnBrowseExportCatalog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            {
                var folderOfd = new OpenFolderDialog { Title = "Catalog Folder" };
                var folderPath = await folderOfd.ShowAsync(_hostingWindow);
                txtExportCatalogPath.Text = folderPath is null ? "" : folderPath;
            }
        }
    }
}
