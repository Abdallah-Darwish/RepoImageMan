using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RepoImageMan;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MainUI
{
    public partial class CommodityImageWindow
    {
        public class CommodityTab : IDisposable
        {
            public sealed class DgCommoditiesModel : INotifyPropertyChanged, IDisposable
            {
                private readonly CommodityTab _hostingTab;

                public Commodity Commodity { get; }

                public bool IsImageCommodity => Commodity is ImageCommodity;

                public string Name => Commodity.Name;

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

                public int Position => Commodity.Position;

                public bool IsExported
                {
                    get => Commodity.IsExported;
                    set => Commodity.IsExported = value;
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
                            if (Regex.IsMatch(Name, _hostingTab.txtSearch.Text) == false) { return; }
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

                private void CommodityOnPropertyChanged([CallerMemberName]string propName = null)
                {
                    void Work()
                    {
                        if (propName == nameof(Commodity.Position)) { RePositionInDgItems(); }
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
                    }
                    if (Dispatcher.UIThread.CheckAccess()) { Work(); }
                    else { Work(); }
                }
                private IDisposable _commodityNotificationsSubscription;
                public DgCommoditiesModel(Commodity com, CommodityTab hostingTab)
                {
                    Commodity = com;
                    _hostingTab = hostingTab;
                    _hostingTab._dgCommoditiesItems.Add(this);
                    _hostingTab._dgCommoditiesModels.Add(this);
                    _commodityNotificationsSubscription = Commodity.Subscribe(CommodityOnPropertyChanged);
                    Commodity.Deleting += CommodityOnDeleting;
                    RePositionInDgItems();
                }

                public void SaveToMemory()
                {
                    Commodity.Name = _hostingTab.txtCommodityName.Text;
                    Commodity.Cost = (decimal)_hostingTab.nudCommodityCost.Value;
                    Commodity.WholePrice = (decimal)_hostingTab.nudCommodityWholePrice.Value;
                    Commodity.PartialPrice = (decimal)_hostingTab.nudCommodityPartialPrice.Value;
                }

                private void CommodityOnDeleting(Commodity _) => Dispose();

                public void Dispose()
                {
                    _hostingTab._dgCommoditiesModels.Remove(this);
                    _hostingTab._dgCommoditiesItems.Remove(this);
                    if (_hostingTab._commodityToMove == this) { _hostingTab.ResetCommodityToMove(); }

                    _commodityNotificationsSubscription.Dispose();
                    Commodity.Deleting -= CommodityOnDeleting;
                }
            }

            private readonly List<DgCommoditiesModel> _dgCommoditiesModels = new List<DgCommoditiesModel>();
            private readonly CommodityImageWindow _hostingWindow;

            private readonly ObservableCollection<DgCommoditiesModel> _dgCommoditiesItems = new ObservableCollection<DgCommoditiesModel>();

            private readonly DataGrid dgCommodities;
            private readonly TextBox txtSearch, txtCommodityName;

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
                miReloadSelectedCommoditiesToDb,
                miCreateCommodity;

            private readonly Button btnSaveCommodityToMemory;

            private readonly ContextMenu dgCommoditiesCTXMenu;

            private readonly TabItem tabCommodities;

            private readonly InputElement[] _selectedCommodityDependants;

            private readonly List<IDisposable> _eventsSubscriptions = new List<IDisposable>();

            private DgCommoditiesModel? _commodityToMove;

            private DateTime _commodityToMoveSelectionTime = DateTime.UtcNow - (CommodityMovingWindow * 2);

            private static readonly TimeSpan CommodityMovingWindow = TimeSpan.FromMinutes(3);

            public CommodityTab(CommodityImageWindow hostingWindow)
            {
                _hostingWindow = hostingWindow;

                tabCommodities = _hostingWindow.Get<TabItem>(nameof(tabCommodities));
                dgCommodities = _hostingWindow.Get<DataGrid>(nameof(dgCommodities));
                txtSearch = _hostingWindow.Get<TextBox>(nameof(txtSearch));
                txtCommodityName = _hostingWindow.Get<TextBox>(nameof(txtCommodityName));
                nudCommodityCost = _hostingWindow.Get<NumericUpDown>(nameof(nudCommodityCost));
                nudCommodityWholePrice = _hostingWindow.Get<NumericUpDown>(nameof(nudCommodityWholePrice));
                nudCommodityPartialPrice = _hostingWindow.Get<NumericUpDown>(nameof(nudCommodityPartialPrice));
                btnSaveCommodityToMemory = _hostingWindow.Get<Button>(nameof(btnSaveCommodityToMemory));
                miDeleteCommodity = _hostingWindow.Get<MenuItem>(nameof(miDeleteCommodity));
                miMoveCommodity = _hostingWindow.Get<MenuItem>(nameof(miMoveCommodity));
                miGoToImage = _hostingWindow.Get<MenuItem>(nameof(miGoToImage));
                miMoveAfterSelectedCommodity = _hostingWindow.Get<MenuItem>(nameof(miMoveAfterSelectedCommodity));
                miMoveBeforeSelectedCommodity = _hostingWindow.Get<MenuItem>(nameof(miMoveBeforeSelectedCommodity));
                miMoveSelectedCommodity = _hostingWindow.Get<MenuItem>(nameof(miMoveSelectedCommodity));
                miExportCommodities = _hostingWindow.Get<MenuItem>(nameof(miExportCommodities));
                miExportSelectedCommodities = _hostingWindow.Get<MenuItem>(nameof(miExportSelectedCommodities));
                miUnExportSelectedCommodities = _hostingWindow.Get<MenuItem>(nameof(miUnExportSelectedCommodities));
                miExportAllCommodities = _hostingWindow.Get<MenuItem>(nameof(miExportAllCommodities));
                miUnExportAllCommodities = _hostingWindow.Get<MenuItem>(nameof(miUnExportAllCommodities));
                miSaveCommodities = _hostingWindow.FindControl<MenuItem>(nameof(miSaveCommodities));
                miSaveAllCommoditiesToDb = _hostingWindow.FindControl<MenuItem>(nameof(miSaveAllCommoditiesToDb));
                miCreateCommodity = _hostingWindow.FindControl<MenuItem>(nameof(miCreateCommodity));
                dgCommoditiesCTXMenu = _hostingWindow.Get<ContextMenu>(nameof(dgCommoditiesCTXMenu));
                miSaveSelectedCommoditiesToDb = _hostingWindow.FindControl<MenuItem>(nameof(miSaveSelectedCommoditiesToDb));
                miReloadAllCommoditiesFromDb = _hostingWindow.FindControl<MenuItem>(nameof(miReloadAllCommoditiesFromDb));
                miReloadSelectedCommoditiesToDb = _hostingWindow.FindControl<MenuItem>(nameof(miReloadSelectedCommoditiesToDb));

                _selectedCommodityDependants = new InputElement[]
                {
                    txtCommodityName, nudCommodityCost, nudCommodityPartialPrice, nudCommodityWholePrice,btnSaveCommodityToMemory
                };
                foreach (var com in _hostingWindow._package.Commodities)
                {
                    new DgCommoditiesModel(com, this);
                }


                nudCommodityCost.FormatString = nudCommodityWholePrice.FormatString = nudCommodityPartialPrice.FormatString = "0.00";
                dgCommodities.SelectionChanged += DgCommoditiesOnSelectionChanged;
                dgCommodities.KeyDown += DgCommoditiesOnKeyDown;
                dgCommodities.CellPointerPressed += DgCommodities_CellPointerPressed;
                _hostingWindow._package.CommodityAdded += Package_CommodityAdded;

                dgCommoditiesCTXMenu.ContextMenuOpening += DgCommoditiesCTXMenuOnContextMenuOpening;
                miCreateCommodity.Click += async (sender, args) => await CreateNewCommodity();
                miExportAllCommodities.Click += MiExportAllCommodities_Click;
                miUnExportAllCommodities.Click += MiUnExportAllCommodities_Click;
                miExportSelectedCommodities.Click += MiExportSelectedCommodities_Click;
                miUnExportSelectedCommodities.Click += MiUnExportSelectedCommodities_Click;
                miDeleteCommodity.Click += async (sender, args) => await DeleteSelectedCommodities();
                miMoveSelectedCommodity.Click += MiMoveSelectedCommodity_Click;
                miSaveAllCommoditiesToDb.Click += MiSaveAllCommoditiesToDb_Click;
                miSaveSelectedCommoditiesToDb.Click += MiSaveSelectedCommoditiesToDb_Click;
                miReloadAllCommoditiesFromDb.Click += MiReloadAllCommoditiesFromDb_Click;
                miReloadSelectedCommoditiesToDb.Click += MiReloadSelectedCommoditiesToDb_Click;
                miMoveBeforeSelectedCommodity.Click += MiMoveBeforeSelectedCommodity_Click;
                miMoveAfterSelectedCommodity.Click += MiMoveAfterSelectedCommodity_Click;
                miGoToImage.Click += MiGoToImage_Click;
                btnSaveCommodityToMemory.Click += BtnSaveCommodityToMemory_Click;

                dgCommodities.Items = _dgCommoditiesItems;

                _eventsSubscriptions.Add(_hostingWindow.GetObservable(Window.ClientSizeProperty).Subscribe(sz => dgCommodities.Height = sz.Height - 230));
                _eventsSubscriptions.Add(txtSearch.GetObservable(TextBox.TextProperty).Subscribe(TxtSearchOnTextChanged));
            }

            private void MiExportAllCommodities_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { com.IsExported = true; }
            }

            internal void GoToCommodity(Commodity com)
            {
                var comModel = _dgCommoditiesItems.FirstOrDefault<DgCommoditiesModel?>(c => c.Commodity.Id == com.Id);
                if (comModel == null) { return; }
                _hostingWindow.Activate();
                tabCommodities.IsSelected = true;
                dgCommodities.SelectedItems.Clear();
                dgCommodities.SelectedItems.Add(comModel);
                dgCommodities.ScrollIntoView(comModel, null);
            }

            private void DgCommodities_CellPointerPressed(object? _, DataGridCellPointerPressedEventArgs e)
            {
                if (!e.PointerPressedEventArgs.GetCurrentPoint(null).Properties.IsRightButtonPressed ||
                    !(e.Row?.DataContext is DgCommoditiesModel selectedCom) ||
                    dgCommodities.SelectedItems.Contains(selectedCom))
                    return;
                dgCommodities.SelectedItems.Add(selectedCom);
            }

            private void MiMoveSelectedCommodity_Click(object? sender, RoutedEventArgs e)
            {
                _commodityToMove = GetSelectedCommodity();
                _commodityToMoveSelectionTime = DateTime.UtcNow;
            }

            private void GoToSelectedCommodityImage()
            {
                if (dgCommodities.SelectedItems.Count != 1) { return; }

                var selectedCom = GetSelectedCommodity();
                if (!(selectedCom.Commodity is ImageCommodity selectedImageCom)) { return; }

                _hostingWindow._imageTab.GoToCommodity(selectedImageCom);
            }

            private void MiGoToImage_Click(object? sender, RoutedEventArgs e) => GoToSelectedCommodityImage();

            private async void MiReloadSelectedCommoditiesToDb_Click(object? sender, RoutedEventArgs e)
            {
                await dgCommodities.SelectedItems.Cast<DgCommoditiesModel>().ForEachAsync(com => com.Commodity.Reload());
            }

            private async void MiReloadAllCommoditiesFromDb_Click(object? sender, RoutedEventArgs e)
            {
                await _dgCommoditiesItems.ForEachAsync(com => com.Commodity.Reload());
            }

            private async void MiSaveSelectedCommoditiesToDb_Click(object? sender, RoutedEventArgs e)
            {
                await dgCommodities.SelectedItems.Cast<DgCommoditiesModel>().ForEachAsync(com => com.Commodity.Save());
            }

            private async void MiSaveAllCommoditiesToDb_Click(object? sender, RoutedEventArgs e)
            {
                await _dgCommoditiesItems.ForEachAsync(com => com.Commodity.Save());
            }

            private void MiUnExportSelectedCommodities_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>()) { com.IsExported = false; }
            }

            private void MiExportSelectedCommodities_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>()) { com.IsExported = true; }
            }

            private void MiUnExportAllCommodities_Click(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { com.IsExported = false; }
            }

            private void MiExportAllCommoditiesOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var com in _dgCommoditiesItems) { com.IsExported = true; }
            }

            private async void MiMoveAfterSelectedCommodity_Click(object? sender, RoutedEventArgs e)
            {
                var selectedCom = GetSelectedCommodity()!;

                if (_commodityToMove == null || _commodityToMove == selectedCom) { return; }

                int newPos = _commodityToMove.Position < selectedCom.Position
                                 ? selectedCom.Position
                                 : selectedCom.Position + 1;

                await _commodityToMove.Commodity.SetPosition(newPos);
                ResetCommodityToMove();
            }

            private async void MiMoveBeforeSelectedCommodity_Click(object? sender, RoutedEventArgs e)
            {
                var selectedCom = GetSelectedCommodity()!;

                if (_commodityToMove == null || _commodityToMove == selectedCom) { return; }

                int newPos = _commodityToMove.Position > selectedCom.Position
                                 ? selectedCom.Position
                                 : selectedCom.Position - 1;

                await _commodityToMove.Commodity.SetPosition(newPos);
                ResetCommodityToMove();
            }

            private void DgCommoditiesCTXMenuOnContextMenuOpening(object sender, CancelEventArgs e)
            {
                if (DateTime.UtcNow - _commodityToMoveSelectionTime > CommodityMovingWindow) { ResetCommodityToMove(); }
                miMoveCommodity.IsVisible = dgCommodities.SelectedItems.Count == 1;
                miGoToImage.IsVisible = dgCommodities.SelectedItems.Count == 1 &&
                                        (dgCommodities.SelectedItems[0] as DgCommoditiesModel)!.IsImageCommodity;
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
                if (miDeleteCommodity.IsVisible) { miDeleteCommodity.Header = $"Delete(DEL) {selectedCom!.ShortName}"; }

                if (miMoveBeforeSelectedCommodity.IsVisible)
                {
                    miMoveBeforeSelectedCommodity.Header = $"Move {_commodityToMove!.ShortName} Before {selectedCom!.ShortName}";
                }

                if (miMoveAfterSelectedCommodity.IsVisible)
                {
                    miMoveAfterSelectedCommodity.Header = $"Move {_commodityToMove!.ShortName} After {selectedCom!.ShortName}";
                }

                if (miMoveCommodity.IsVisible) { miMoveSelectedCommodity.Header = $"Move {selectedCom!.ShortName}"; }
            }

            private void ResetCommodityToMove()
            {
                _commodityToMove = null;
                _commodityToMoveSelectionTime = DateTime.UtcNow - (CommodityMovingWindow * 2);
            }


            private DgCommoditiesModel? GetSelectedCommodity() => dgCommodities.SelectedItems.Count == 0
                                                                      ? null
                                                                      : dgCommodities.SelectedItems[0] as DgCommoditiesModel;


            private void BtnSaveCommodityToMemory_Click(object? sender, RoutedEventArgs e) => GetSelectedCommodity()?.SaveToMemory();

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
                    txtCommodityName.Text = "";
                    nudCommodityCost.Value = nudCommodityWholePrice.Value = nudCommodityPartialPrice.Value = 0;
                    foreach (var i in _selectedCommodityDependants) { i.IsEnabled = false; }
                }
                else
                {
                    foreach (var i in _selectedCommodityDependants) { i.IsEnabled = true; }

                    txtCommodityName.Text = selectedCommodity.Name;
                    nudCommodityCost.Value = (double)selectedCommodity.Cost;
                    nudCommodityWholePrice.Value = (double)selectedCommodity.WholePrice;
                    nudCommodityPartialPrice.Value = (double)selectedCommodity.PartialPrice;
                }
            }

            private void Package_CommodityAdded(CommodityPackage _, Commodity com) => new DgCommoditiesModel(com, this);

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