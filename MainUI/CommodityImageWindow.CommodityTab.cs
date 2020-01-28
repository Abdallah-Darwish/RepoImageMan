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
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using DynamicData;
using JetBrains.Annotations;

namespace MainUI
{
    public partial class CommodityImageWindow
    {
        private class CommodityTab : IDisposable
        {
            public class DgCommoditiesModel : INotifyPropertyChanged
            {
                private Commodity _commodity;

                public Commodity Commodity
                {
                    get => _commodity;
                    set
                    {
                        if (_commodity == value)
                        {
                            return;
                        }

                        if (_commodity != null)
                        {
                            _commodity.PropertyChanged -= CommodityPropertyChanged;
                        }

                        _commodity = value;
                        _commodity.PropertyChanged += CommodityPropertyChanged;
                    }
                }

                public string Name =>
                    ((_commodity as ImageCommodity)?.IsPositionHolder ?? false) ? "---" : _commodity.Name;

                public decimal Cost => _commodity.Cost;

                public decimal PartialPrice => _commodity.PartialPrice;

                public decimal WholePrice => _commodity.WholePrice;

                public int Position => _commodity.Position;
                private bool _export;

                public bool Export
                {
                    get => _export;
                    set
                    {
                        if (value == _export)
                        {
                            return;
                        }

                        _export = value;
                        OnPropertyChanged();
                    }
                }

                public event PropertyChangedEventHandler PropertyChanged;

                [NotifyPropertyChangedInvocator]
                protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }

                private void CommodityPropertyChanged(object sender, PropertyChangedEventArgs e)
                {
                    PropertyChanged?.Invoke(this, e);
                }
            }

            public class CbxMoveCommoditiesModel : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler PropertyChanged;
                public string Name => _commodity.Name;
                private Commodity _commodity;

                public Commodity Commodity
                {
                    get => _commodity;
                    set
                    {
                        if (_commodity == value)
                        {
                            return;
                        }

                        if (_commodity != null)
                        {
                            _commodity.PropertyNotificationManager.Unsubscribe(nameof(Commodity.Name),
                                CommodityPropertyChanged);
                        }

                        _commodity = value;
                        _commodity.PropertyNotificationManager.Subscribe(nameof(Commodity.Name),
                            CommodityPropertyChanged);
                    }
                }

                private void CommodityPropertyChanged(object sender, PropertyChangedEventArgs e)
                {
                    PropertyChanged?.Invoke(this, e);
                }
            }

            private readonly CommodityImageWindow _hostingWindow;
            private readonly ObservableCollection<DgCommoditiesModel> _dgCommoditiesItems;
            private readonly ObservableCollection<CbxMoveCommoditiesModel> _cbxMoveCommoditiesItems;
            private readonly Dictionary<int, DgCommoditiesModel> _dgCommoditiesModels;
            private readonly Dictionary<int, CbxMoveCommoditiesModel> _cbxMoveCommoditiesModels;
            private readonly DataGrid dgCommodities;
            private readonly TextBox txtSearch, txtCommodityName;
            private readonly NumericUpDown nudCommodityCost, nudCommodityWholePrice, nudCommodityPartialPrice;
            private readonly ComboBox cbxMoveCommodities;

            private readonly Button btnMoveSelectedCommodity,
                btnSaveCommodityToMemory,
                btnReloadCommodity,
                btnSaveCommodityToDB;

            private readonly TabItem tabCommodities;

            private readonly InputElement[] _selectedCommodityDependants;

            private readonly List<IDisposable> _eventsSubscribtions = new List<IDisposable>();

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
                cbxMoveCommodities = _hostingWindow.Get<ComboBox>(nameof(cbxMoveCommodities));
                btnMoveSelectedCommodity = _hostingWindow.Get<Button>(nameof(btnMoveSelectedCommodity));
                btnReloadCommodity = _hostingWindow.Get<Button>(nameof(btnReloadCommodity));
                btnSaveCommodityToMemory = _hostingWindow.Get<Button>(nameof(btnSaveCommodityToMemory));
                btnSaveCommodityToDB = _hostingWindow.Get<Button>(nameof(btnSaveCommodityToDB));


                _selectedCommodityDependants = new InputElement[]
                {
                    txtCommodityName, nudCommodityCost, nudCommodityPartialPrice, nudCommodityWholePrice,
                    cbxMoveCommodities, btnMoveSelectedCommodity, btnReloadCommodity, btnSaveCommodityToMemory,
                    btnSaveCommodityToDB
                };
                _dgCommoditiesModels =
                    _hostingWindow._package.Commodities.ToDictionary(c => c.Id,
                        c => new DgCommoditiesModel {Commodity = c});
                _dgCommoditiesItems =
                    new ObservableCollection<DgCommoditiesModel>(_dgCommoditiesModels.Values.OrderBy(c => c.Position));
                _cbxMoveCommoditiesModels = new Dictionary<int, CbxMoveCommoditiesModel>(
                    _hostingWindow._package.Commodities.ToDictionary(c => c.Id,
                        c => new CbxMoveCommoditiesModel {Commodity = c}));

                _cbxMoveCommoditiesItems =
                    new ObservableCollection<CbxMoveCommoditiesModel>(
                        _cbxMoveCommoditiesModels.Values.OrderBy(c => c.Commodity.Position));

                foreach (var com in _hostingWindow._package.Commodities)
                {
                    com.PropertyNotificationManager.Subscribe(nameof(Commodity.Position), CommodityOnPositionChanged);
                }

                nudCommodityCost.FormatString = nudCommodityWholePrice.FormatString =
                    nudCommodityPartialPrice.FormatString = "0.00";
                dgCommodities.SelectionChanged += DgCommoditiesOnSelectionChanged;
                dgCommodities.KeyDown += DgCommoditiesOnKeyDown;

                _hostingWindow._package.CommodityAdded += PackageOnCommodityAdded;
                _hostingWindow._package.CommodityRemoved += PackageOnCommodityRemoved;
                _eventsSubscribtions.Add(txtSearch.GetObservable(TextBox.TextProperty).Do(TxtSearchOnTextChanged)
                    .Subscribe());

                _hostingWindow.Get<MenuItem>("miCreateCommodity").Click +=
                    async (sender, args) => await CreateNewCommodity();
                _hostingWindow.Get<MenuItem>("miDeleteCommodity").Click +=
                    async (sender, args) => await DeleteSelectedCommodities();
                btnMoveSelectedCommodity.Click += BtnMoveSelectedCommodityOnClick;
                btnSaveCommodityToMemory.Click += BtnSaveCommodityToMemoryOnClick;
                btnSaveCommodityToDB.Click += BtnSaveCommodityToDBOnClick;
                btnReloadCommodity.Click += BtnReloadCommodityOnClick;

                dgCommodities.Items = _dgCommoditiesItems;
                cbxMoveCommodities.Items = _cbxMoveCommoditiesItems;

                _eventsSubscribtions.Add(_hostingWindow.GetObservable(Window.ClientSizeProperty)
                    .Do(sz => dgCommodities.Height = sz.Height - 250).Subscribe());
            }

            private void CommodityOnPositionChanged(object comObj, PropertyChangedEventArgs _)
            {
                var com = comObj as Commodity;
                var comDgModel = _dgCommoditiesModels[com.Id];
                if (_dgCommoditiesItems.Remove(comDgModel))
                {
                    AddCommoditiesToDgCommoditiesItems(new[] {comDgModel});
                }

                var comCbxModel = _cbxMoveCommoditiesModels[com.Id];
                _cbxMoveCommoditiesItems.Remove(comCbxModel);
                AddCommoditiesToCbxMoveCommoditiesItems(new[] {comCbxModel});
            }

            private async void BtnReloadCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                foreach (var comModel in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>())
                {
                    await comModel.Commodity.Reload();
                }
            }

            private DgCommoditiesModel? GetSelectedCommodity() => dgCommodities.SelectedItems.Count == 0
                ? null
                : dgCommodities.SelectedItems[0] as DgCommoditiesModel;

            private void SaveSelectedCommodityToMemory()
            {
                var selectedCommodity = GetSelectedCommodity()?.Commodity;
                if (selectedCommodity == null)
                {
                    return;
                }

                selectedCommodity.Name = txtCommodityName.Text;
                selectedCommodity.Cost = (decimal) nudCommodityCost.Value;
                selectedCommodity.WholePrice = (decimal) nudCommodityWholePrice.Value;
                selectedCommodity.PartialPrice = (decimal) nudCommodityPartialPrice.Value;
            }

            private void AddCommoditiesToCbxMoveCommoditiesItems(IEnumerable<CbxMoveCommoditiesModel> coms)
            {
                coms = coms.OrderBy(c => c.Commodity.Position);

                int i = 0, comPos;
                foreach (var com in coms)
                {
                    comPos = _cbxMoveCommoditiesItems.Count;
                    for (; i < _cbxMoveCommoditiesItems.Count; i++)
                    {
                        if (_cbxMoveCommoditiesItems[i].Commodity.Position > com.Commodity.Position)
                        {
                            comPos = i++;
                            break;
                        }
                    }

                    _cbxMoveCommoditiesItems.Insert(comPos, com);
                }
            }

            private void AddCommoditiesToDgCommoditiesItems(IEnumerable<DgCommoditiesModel> coms)
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text) == false)
                {
                    coms = coms.Where(c =>
                    {
                        try
                        {
                            return Regex.IsMatch(c.Name, txtSearch.Text);
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }

                coms = coms.OrderBy(c => c.Position);

                int i = 0, comPos;
                foreach (var com in coms)
                {
                    comPos = _dgCommoditiesItems.Count;
                    for (; i < _dgCommoditiesItems.Count; i++)
                    {
                        if (_dgCommoditiesItems[i].Position > com.Position)
                        {
                            comPos = i++;
                            break;
                        }
                    }

                    _dgCommoditiesItems.Insert(comPos, com);
                }
            }

            private async void BtnSaveCommodityToDBOnClick(object? sender, RoutedEventArgs e)
            {
                SaveSelectedCommodityToMemory();
                foreach (var comModel in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>())
                {
                    await comModel.Commodity.Save();
                }
            }

            private void BtnSaveCommodityToMemoryOnClick(object? sender, RoutedEventArgs e) =>
                SaveSelectedCommodityToMemory();

            private void BtnMoveSelectedCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                var comToMove = GetSelectedCommodity();
                var comToMoveBefore = cbxMoveCommodities.SelectedItem as CbxMoveCommoditiesModel;
                if (comToMoveBefore == null || comToMove == null)
                {
                    return;
                }

                comToMove.Commodity.SetPosition(comToMoveBefore.Commodity.Position);
            }

            private async void DgCommoditiesOnKeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyModifiers != KeyModifiers.None)
                {
                    return;
                }

                switch (e.Key)
                {
                    case Key.Insert:
                        await CreateNewCommodity();
                        break;
                    case Key.Delete:
                        await DeleteSelectedCommodities();
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
                    cbxMoveCommodities.SelectedItem = null;
                    foreach (var i in _selectedCommodityDependants)
                    {
                        i.IsEnabled = false;
                    }
                }
                else
                {
                    foreach (var i in _selectedCommodityDependants)
                    {
                        i.IsEnabled = true;
                    }

                    txtCommodityName.Text = selectedCommodity.Name;
                    nudCommodityCost.Value = (double) selectedCommodity.Cost;
                    nudCommodityWholePrice.Value = (double) selectedCommodity.WholePrice;
                    nudCommodityPartialPrice.Value = (double) selectedCommodity.PartialPrice;
                }
            }

            private void PackageOnCommodityRemoved(CommodityPackage sender, Commodity com)
            {
                _dgCommoditiesItems.Remove(_dgCommoditiesModels[com.Id]);
                _dgCommoditiesModels.Remove(com.Id);
                _cbxMoveCommoditiesItems.Remove(_cbxMoveCommoditiesModels[com.Id]);
                _cbxMoveCommoditiesModels.Remove(com.Id);
            }

            private void PackageOnCommodityAdded(CommodityPackage sender, Commodity com)
            {
                var comDgModel = new DgCommoditiesModel {Commodity = com};
                _dgCommoditiesModels.Add(com.Id, comDgModel);
                AddCommoditiesToDgCommoditiesItems(new[] {comDgModel});
                if (_dgCommoditiesItems.Contains(comDgModel))
                {
                    dgCommodities.SelectedItems.Clear();
                    dgCommodities.SelectedItems.Add(comDgModel);
                }

                var comCbxModel = new CbxMoveCommoditiesModel {Commodity = com};
                _cbxMoveCommoditiesModels.Add(com.Id, comCbxModel);
                AddCommoditiesToCbxMoveCommoditiesItems(new[] {comCbxModel});

                com.PropertyNotificationManager.Subscribe(nameof(Commodity.Position), CommodityOnPositionChanged);
            }

            private void TxtSearchOnTextChanged(string text)
            {
                _dgCommoditiesItems.Clear();
                AddCommoditiesToDgCommoditiesItems(_dgCommoditiesModels.Values);
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
                _dgCommoditiesItems.Clear();
                _dgCommoditiesModels.Clear();
                _cbxMoveCommoditiesItems.Clear();
                _cbxMoveCommoditiesModels.Clear();
                foreach (var sub in _eventsSubscribtions)
                {
                    sub.Dispose();
                }
            }
        }
    }
}