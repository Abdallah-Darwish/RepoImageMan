using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RepoImageMan;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.VisualTree;
using DynamicData;
using JetBrains.Annotations;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using SharpDX;

namespace MainUI
{
    public partial class CommodityImageWindow
    {
        //TODO: make position holders name color gray
        private class CommodityTab : IDisposable
        {
            public sealed class DgCommoditiesModel : INotifyPropertyChanged, IDisposable
            {
                private readonly CommodityTab _hostingTab;

                public Commodity Commodity { get; }

                public bool IsImageCommodity => Commodity is ImageCommodity;
                public bool IsPositionHolder => (Commodity as ImageCommodity)?.IsPositionHolder ?? false;

                public string Name =>
                    IsPositionHolder ? "---" : Commodity.Name;

                public string ShortName
                {
                    get
                    {
                        var name = Name;
                        return name.Length <= 10 ? name : $"{name.Substring(0, 7)}...";
                    }
                }

                public decimal Cost => Commodity.Cost;

                public decimal PartialPrice => Commodity.PartialPrice;

                public decimal WholePrice => Commodity.WholePrice;

                public  int  Position => Commodity.Position;
                private bool _export;

                public bool Export
                {
                    get => _export;
                    set
                    {
                        if (value == _export) { return; }

                        _export = value;
                        OnPropertyChanged();
                    }
                }

                /// <summary>
                /// Adds the item if it fits the search pattern and removes it otherwise.
                /// If item is added its added in the correct position.
                /// </summary>
                public void RePositionInDgItems()
                {
                    _hostingTab._dgCommoditiesItems.Remove(this);
                    if (!string.IsNullOrWhiteSpace(_hostingTab.txtSearch.Text))
                    {
                        try
                        {
                            if (IsPositionHolder || !Regex.IsMatch(Name, _hostingTab.txtSearch.Text)) { return; }
                        }
                        catch { return; }
                    }

                    int i = 0;
                    for (; i < _hostingTab._dgCommoditiesItems.Count; i++)
                    {
                        if (_hostingTab._dgCommoditiesItems[i].Position > Position) { break; }
                    }

                    _hostingTab._dgCommoditiesItems.Insert(i, this);
                }

                public event PropertyChangedEventHandler PropertyChanged;

                [NotifyPropertyChangedInvocator]
                private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

                private void CommodityOnPropertyChanged(object _, PropertyChangedEventArgs e)
                {
                    if (e.PropertyName == nameof(Commodity.Position)) { RePositionInDgItems(); }

                    PropertyChanged?.Invoke(this, e);
                }

                public DgCommoditiesModel(Commodity com, CommodityTab hostingTab)
                {
                    _hostingTab               =  hostingTab;
                    Commodity                 =  com;
                    Commodity.PropertyChanged += CommodityOnPropertyChanged;
                    Commodity.Deleting        += CommodityOnDeleting;
                    RePositionInDgItems();
                }

                public void SaveToMemory()
                {
                    Commodity.Name         = _hostingTab.txtCommodityName.Text;
                    Commodity.Cost         = (decimal) _hostingTab.nudCommodityCost.Value;
                    Commodity.WholePrice   = (decimal) _hostingTab.nudCommodityWholePrice.Value;
                    Commodity.PartialPrice = (decimal) _hostingTab.nudCommodityPartialPrice.Value;
                }

                private void CommodityOnDeleting(Commodity _) => Dispose();

                public void Dispose()
                {
                    _hostingTab._dgCommoditiesModels.Remove(this);
                    _hostingTab._dgCommoditiesItems.Remove(this);
                    if (_hostingTab._commodityToMove == this) { _hostingTab.ResetCommodityToMove(); }

                    Commodity.PropertyChanged -= CommodityOnPropertyChanged;
                    Commodity.Deleting        -= CommodityOnDeleting;
                }
            }

            private readonly List<DgCommoditiesModel> _dgCommoditiesModels;
            private readonly CommodityImageWindow     _hostingWindow;

            private readonly ObservableCollection<DgCommoditiesModel> _dgCommoditiesItems =
                new ObservableCollection<DgCommoditiesModel>();

            private readonly DataGrid dgCommodities;
            private readonly TextBox  txtSearch, txtCommodityName;

            private readonly NumericUpDown nudCommodityCost,
                                           nudCommodityWholePrice,
                                           nudCommodityPartialPrice;

            private readonly MenuItem miMoveCommodity,
                                      miMoveSelectedCommodity,
                                      miMoveBeforeSelectedCommodity,
                                      miMoveAfterSelectedCommodity,
                                      miDeleteCommodity,
                                      miGoToImage,
                                      miExportCommodities,
                                      miExportSelectedCommodities,
                                      miExportAllCommodities,
                                      miUnExportAllCommodities,
                                      miUnExportSelectedCommodities,
                                      miSaveCommodities,
                                      miSaveAllCommoditiesToDb,
                                      miSaveSelectedCommoditiesToDb,
                                      miReloadAllCommoditiesFromDb,
                                      miReloadSelectedCommoditiesToDb;

            private readonly Button btnSaveCommodityToMemory;

            private readonly ContextMenu dgCommoditiesCTXMenu;

            private readonly TabItem tabCommodities;

            private readonly InputElement[] _selectedCommodityDependants;

            private readonly List<IDisposable> _eventsSubscriptions = new List<IDisposable>();

            private DgCommoditiesModel? _commodityToMove;

            private DateTime _commodityToMoveSelectionTime =
                DateTime.UtcNow - (CommodityMovingWindow * 2);

            private static readonly TimeSpan CommodityMovingWindow = TimeSpan.FromMinutes(3);

            public CommodityTab(CommodityImageWindow hostingWindow)
            {
                _hostingWindow = hostingWindow;

                tabCommodities                = _hostingWindow.Get<TabItem>(nameof(tabCommodities));
                dgCommodities                 = _hostingWindow.Get<DataGrid>(nameof(dgCommodities));
                txtSearch                     = _hostingWindow.Get<TextBox>(nameof(txtSearch));
                txtCommodityName              = _hostingWindow.Get<TextBox>(nameof(txtCommodityName));
                nudCommodityCost              = _hostingWindow.Get<NumericUpDown>(nameof(nudCommodityCost));
                nudCommodityWholePrice        = _hostingWindow.Get<NumericUpDown>(nameof(nudCommodityWholePrice));
                nudCommodityPartialPrice      = _hostingWindow.Get<NumericUpDown>(nameof(nudCommodityPartialPrice));
                btnSaveCommodityToMemory      = _hostingWindow.Get<Button>(nameof(btnSaveCommodityToMemory));
                miDeleteCommodity             = _hostingWindow.Get<MenuItem>(nameof(miDeleteCommodity));
                miMoveCommodity               = _hostingWindow.Get<MenuItem>(nameof(miMoveCommodity));
                miGoToImage                   = _hostingWindow.Get<MenuItem>(nameof(miGoToImage));
                miMoveAfterSelectedCommodity  = _hostingWindow.Get<MenuItem>(nameof(miMoveAfterSelectedCommodity));
                miMoveBeforeSelectedCommodity = _hostingWindow.Get<MenuItem>(nameof(miMoveBeforeSelectedCommodity));
                miMoveSelectedCommodity       = _hostingWindow.Get<MenuItem>(nameof(miMoveSelectedCommodity));
                miExportCommodities           = _hostingWindow.Get<MenuItem>(nameof(miExportCommodities));
                miExportSelectedCommodities   = _hostingWindow.Get<MenuItem>(nameof(miExportSelectedCommodities));
                miUnExportSelectedCommodities = _hostingWindow.Get<MenuItem>(nameof(miUnExportSelectedCommodities));
                miExportAllCommodities        = _hostingWindow.Get<MenuItem>(nameof(miExportAllCommodities));
                miUnExportAllCommodities      = _hostingWindow.Get<MenuItem>(nameof(miUnExportAllCommodities));
                miSaveCommodities             = _hostingWindow.FindControl<MenuItem>(nameof(miSaveCommodities));
                miSaveAllCommoditiesToDb      = _hostingWindow.FindControl<MenuItem>(nameof(miSaveAllCommoditiesToDb));
                dgCommoditiesCTXMenu =
                    _hostingWindow.Get<Avalonia.Controls.ContextMenu>(nameof(dgCommoditiesCTXMenu));
                miSaveSelectedCommoditiesToDb =
                    _hostingWindow.FindControl<MenuItem>(nameof(miSaveSelectedCommoditiesToDb));
                miReloadAllCommoditiesFromDb =
                    _hostingWindow.FindControl<MenuItem>(nameof(miReloadAllCommoditiesFromDb));
                miReloadSelectedCommoditiesToDb =
                    _hostingWindow.FindControl<MenuItem>(nameof(miReloadSelectedCommoditiesToDb));

                _selectedCommodityDependants = new InputElement[]
                {
                    txtCommodityName, nudCommodityCost, nudCommodityPartialPrice, nudCommodityWholePrice,
                    btnSaveCommodityToMemory
                };
                _dgCommoditiesModels =
                    _hostingWindow._package.Commodities.Select(c => new DgCommoditiesModel(c, this)).ToList();


                nudCommodityCost.FormatString = nudCommodityWholePrice.FormatString =
                                                    nudCommodityPartialPrice.FormatString = "0.00";
                dgCommodities.SelectionChanged         += DgCommoditiesOnSelectionChanged;
                dgCommodities.KeyDown                  += DgCommoditiesOnKeyDown;
                dgCommodities.CellPointerPressed       += DgCommoditiesOnCellPointerPressed;
                _hostingWindow._package.CommodityAdded += PackageOnCommodityAdded;

                dgCommoditiesCTXMenu.ContextMenuOpening += DgCommoditiesCTXMenuOnContextMenuOpening;
                _hostingWindow.Get<MenuItem>("miCreateCommodity").Click +=
                    async (sender, args) => await CreateNewCommodity();
                miExportAllCommodities.Click          += MiExportAllCommoditiesOnClick;
                miUnExportAllCommodities.Click        += MiUnExportAllCommoditiesOnClick;
                miExportSelectedCommodities.Click     += MiExportSelectedCommoditiesOnClick;
                miUnExportSelectedCommodities.Click   += MiUnExportSelectedCommoditiesOnClick;
                miDeleteCommodity.Click               += async (sender, args) => await DeleteSelectedCommodities();
                miMoveSelectedCommodity.Click         += MiMoveSelectedCommodityOnClick;
                miSaveAllCommoditiesToDb.Click        += MiSaveAllCommoditiesToDbOnClick;
                miSaveSelectedCommoditiesToDb.Click   += MiSaveSelectedCommoditiesToDbOnClick;
                miReloadAllCommoditiesFromDb.Click    += MiReloadAllCommoditiesFromDbOnClick;
                miReloadSelectedCommoditiesToDb.Click += MiReloadSelectedCommoditiesToDbOnClick;
                miMoveBeforeSelectedCommodity.Click   += MiMoveBeforeSelectedCommodityOnClick;
                miMoveAfterSelectedCommodity.Click    += MiMoveAfterSelectedCommodityOnClick;
                miGoToImage.Click                     += MiGoToImageOnClick;
                btnSaveCommodityToMemory.Click        += BtnSaveCommodityToMemoryOnClick;

                dgCommodities.Items = _dgCommoditiesItems;

                _eventsSubscriptions.Add(_hostingWindow.GetObservable(Window.ClientSizeProperty)
                                                       .Do(sz => dgCommodities.Height = sz.Height - 200).Subscribe());
                _eventsSubscriptions.Add(txtSearch.GetObservable(TextBox.TextProperty).Do(TxtSearchOnTextChanged)
                                                  .Subscribe());
            }

            internal void GoToCommodity(Commodity com)
            {
                var comModel = _dgCommoditiesItems.FirstOrDefault<DgCommoditiesModel?>(c => c.Commodity.Id == com.Id);
                if (comModel == null) { return; }

                tabCommodities.IsSelected = true;
                dgCommodities.SelectedItems.Clear();
                dgCommodities.SelectedItems.Add(comModel);
            }

            private void DgCommoditiesOnCellPointerPressed(object? _, DataGridCellPointerPressedEventArgs e)
            {
                if (!e.PointerPressedEventArgs.GetCurrentPoint(null).Properties.IsRightButtonPressed ||
                    !(e.Row?.DataContext is DgCommoditiesModel selectedCom) ||
                    dgCommodities.SelectedItems.Contains(selectedCom))
                    return;
                dgCommodities.SelectedItems.Add(selectedCom);
            }

            private void MiMoveSelectedCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                _commodityToMove              = GetSelectedCommodity();
                _commodityToMoveSelectionTime = DateTime.UtcNow;
            }

            private void GoToSelectedCommodityImage()
            {
                if (dgCommodities.SelectedItems.Count != 1) { return; }

                var selectedCom = GetSelectedCommodity();
                if (!(selectedCom.Commodity is ImageCommodity selectedImageCom)) { return; }

                _hostingWindow._imageTab.GoToCommodity(selectedImageCom);
            }

            private void MiGoToImageOnClick(object? sender, RoutedEventArgs e) => GoToSelectedCommodityImage();

            private async void MiReloadSelectedCommoditiesToDbOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>())
                {
                    await com.Commodity.Reload();
                }
            }

            private async void MiReloadAllCommoditiesFromDbOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { await com.Commodity.Reload(); }
            }

            private async void MiSaveSelectedCommoditiesToDbOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>())
                {
                    await com.Commodity.Save();
                }
            }

            private async void MiSaveAllCommoditiesToDbOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { await com.Commodity.Save(); }
            }

            private void MiUnExportSelectedCommoditiesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>()) { com.Export = false; }
            }

            private void MiExportSelectedCommoditiesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>()) { com.Export = true; }
            }

            private void MiUnExportAllCommoditiesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { com.Export = false; }
            }

            private void MiExportAllCommoditiesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { com.Export = true; }
            }

            private void MiMoveAfterSelectedCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                var selectedCom = GetSelectedCommodity();

                if (_commodityToMove == null || _commodityToMove == selectedCom) { return; }

                int newPos = _commodityToMove.Position < selectedCom.Position
                                 ? selectedCom.Position
                                 : selectedCom.Position + 1;

                _commodityToMove?.Commodity.SetPosition(newPos);
                ResetCommodityToMove();
            }

            private void MiMoveBeforeSelectedCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                var selectedCom = GetSelectedCommodity();

                if (_commodityToMove == null || _commodityToMove == selectedCom) { return; }

                int newPos = _commodityToMove.Position > selectedCom.Position
                                 ? selectedCom.Position
                                 : selectedCom.Position - 1;

                _commodityToMove?.Commodity.SetPosition(newPos);
                ResetCommodityToMove();
            }

            private void DgCommoditiesCTXMenuOnContextMenuOpening(object sender, CancelEventArgs e)
            {
                if (DateTime.UtcNow - _commodityToMoveSelectionTime > CommodityMovingWindow) { ResetCommodityToMove(); }
                miMoveCommodity.IsVisible = dgCommodities.SelectedItems.Count == 1;
                miGoToImage.IsVisible = dgCommodities.SelectedItems.Count == 1 &&
                                        (dgCommodities.SelectedItems[0] as DgCommoditiesModel).IsImageCommodity;
                miSaveCommodities.IsVisible = miExportCommodities.IsVisible = _dgCommoditiesItems.Count > 0;
                //put us in some collection
                miExportSelectedCommodities.IsVisible =
                    miUnExportSelectedCommodities.IsVisible =
                        miSaveSelectedCommoditiesToDb.IsVisible =
                            miReloadSelectedCommoditiesToDb.IsVisible = dgCommodities.SelectedItems.Count > 0;
                var selectedCom = GetSelectedCommodity();
                miDeleteCommodity.IsVisible = selectedCom != null;
                miMoveAfterSelectedCommodity.IsVisible = miMoveBeforeSelectedCommodity.IsVisible =
                                                             _commodityToMove != null &&
                                                             selectedCom != _commodityToMove;
                if (miDeleteCommodity.IsVisible) { miDeleteCommodity.Header = $"Delete(DEL) {selectedCom.ShortName}"; }

                if (miMoveBeforeSelectedCommodity.IsVisible)
                {
                    miMoveBeforeSelectedCommodity.Header =
                        $"Move {_commodityToMove.ShortName} Before {selectedCom.ShortName}";
                }

                if (miMoveAfterSelectedCommodity.IsVisible)
                {
                    miMoveAfterSelectedCommodity.Header =
                        $"Move {_commodityToMove.ShortName} After {selectedCom.ShortName}";
                }

                if (miMoveCommodity.IsVisible) { miMoveSelectedCommodity.Header = $"Move {selectedCom.ShortName}"; }
            }

            private void ResetCommodityToMove()
            {
                _commodityToMove              = null;
                _commodityToMoveSelectionTime = DateTime.UtcNow - (CommodityMovingWindow * 2);
            }


            private DgCommoditiesModel? GetSelectedCommodity() => dgCommodities.SelectedItems.Count == 0
                                                                      ? null
                                                                      : dgCommodities.SelectedItems[0] as
                                                                            DgCommoditiesModel;


            private void BtnSaveCommodityToMemoryOnClick(object? sender, RoutedEventArgs e) =>
                GetSelectedCommodity()?.SaveToMemory();

            private async void DgCommoditiesOnKeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyModifiers != KeyModifiers.None) { return; }

                switch (e.Key)
                {
                    case Key.Insert:
                        await CreateNewCommodity();
                        break;
                    case Key.Delete:
                        await DeleteSelectedCommodities();
                        break;
                    //FOR SOME GOD KNOWS REASON THIS DOESN'T ARRIVE
                    case Key.Right:
                        GoToSelectedCommodityImage();
                        break;
                }
            }

            private void DgCommoditiesOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
            {
                e.Handled = true;
                var selectedCommodity = GetSelectedCommodity();
                if (selectedCommodity == null)
                {
                    txtCommodityName.Text  = "";
                    nudCommodityCost.Value = nudCommodityWholePrice.Value = nudCommodityPartialPrice.Value = 0;
                    foreach (var i in _selectedCommodityDependants) { i.IsEnabled                          = false; }
                }
                else
                {
                    foreach (var i in _selectedCommodityDependants) { i.IsEnabled = true; }

                    txtCommodityName.Text          = selectedCommodity.Name;
                    nudCommodityCost.Value         = (double) selectedCommodity.Cost;
                    nudCommodityWholePrice.Value   = (double) selectedCommodity.WholePrice;
                    nudCommodityPartialPrice.Value = (double) selectedCommodity.PartialPrice;
                }
            }

            private void PackageOnCommodityAdded(CommodityPackage _, Commodity com)
            {
                _dgCommoditiesModels.Add(new DgCommoditiesModel(com, this));
            }

            private void TxtSearchOnTextChanged(string _)
            {
                foreach (var com in _dgCommoditiesModels) { com.RePositionInDgItems(); }
            }

            private async Task CreateNewCommodity() => await _hostingWindow._package.AddCommodity();

            private async Task DeleteSelectedCommodities()
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>().ToArray())
                {
                    await com.Commodity.Delete();
                }
            }

            public void Dispose()
            {
                foreach (var com in _dgCommoditiesModels) { com.Dispose(); }

                _dgCommoditiesItems.Clear();
                _dgCommoditiesModels.Clear();
                foreach (var sub in _eventsSubscriptions) { sub.Dispose(); }
            }
        }
    }
}