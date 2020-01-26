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
        private class CommodityTab
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

            private readonly CommodityImageWindow _hostingWindow;
            private readonly ObservableCollection<DgCommoditiesModel> _dgCommoditiesItems;
            private readonly Dictionary<int, DgCommoditiesModel> _commoditiesModels;
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
                _commoditiesModels =
                    _hostingWindow._package.Commodities.ToDictionary(c => c.Id,
                        c => new DgCommoditiesModel {Commodity = c});
                _dgCommoditiesItems = new ObservableCollection<DgCommoditiesModel>(_commoditiesModels.Values);

                nudCommodityCost.FormatString = nudCommodityWholePrice.FormatString =
                    nudCommodityPartialPrice.FormatString = "0.00";
                dgCommodities.SelectionChanged += DgCommoditiesOnSelectionChanged;
                dgCommodities.KeyDown += DgCommoditiesOnKeyDown;

                _hostingWindow._package.CommodityAdded += PackageOnCommodityAdded;
                _hostingWindow._package.CommodityRemoved += PackageOnCommodityRemoved;
                GC.KeepAlive(txtSearch.GetObservable(TextBox.TextProperty).Do(TxtSearchOnTextChanged).Subscribe());

                _hostingWindow.Get<MenuItem>("miCreateCommodity").Click +=
                    async (sender, args) => await CreateNewCommodity();
                _hostingWindow.Get<MenuItem>("miDeleteCommodity").Click +=
                    async (sender, args) => await DeleteSelectedCommodities();
                btnMoveSelectedCommodity.Click += BtnMoveSelectedCommodityOnClick;

                dgCommodities.Items = _dgCommoditiesItems;

                GC.KeepAlive(_hostingWindow.GetObservable(Window.ClientSizeProperty)
                    .Do(sz => dgCommodities.Height = sz.Height - 250).Subscribe());
            }

            private void BtnMoveSelectedCommodityOnClick(object? sender, RoutedEventArgs e)
            {
                throw new NotImplementedException();
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
                    default: break;
                }
            }

            private void DgCommoditiesOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
            {
                e.Handled = true;
                if (dgCommodities.SelectedItem == null)
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
                    btnMoveSelectedCommodity.IsEnabled = true;
                    txtCommodityName.IsReadOnly = nudCommodityCost.IsReadOnly =
                        nudCommodityWholePrice.IsReadOnly = nudCommodityPartialPrice.IsReadOnly = false;

                    var selectedCommodity = dgCommodities.SelectedItem as DgCommoditiesModel;
                    txtCommodityName.Text = selectedCommodity.Name;
                    nudCommodityCost.Value = (double) selectedCommodity.Cost;
                    nudCommodityWholePrice.Value = (double) selectedCommodity.WholePrice;
                    nudCommodityPartialPrice.Value = (double) selectedCommodity.PartialPrice;
                }
            }

            private void PackageOnCommodityRemoved(CommodityPackage sender, Commodity com)
            {
                _dgCommoditiesItems.Remove(_commoditiesModels[com.Id]);
                _commoditiesModels.Remove(com.Id);
            }

            private void PackageOnCommodityAdded(CommodityPackage sender, Commodity com)
            {
                var comModel = new DgCommoditiesModel {Commodity = com};
                _commoditiesModels.Add(com.Id, comModel);
                if (string.IsNullOrWhiteSpace(txtSearch.Text) || Regex.IsMatch(com.Name, txtSearch.Text))
                {
                    _dgCommoditiesItems.Add(comModel);
                    dgCommodities.SelectedItem = comModel;
                }
            }

            private void TxtSearchOnTextChanged(string text)
            {
                _dgCommoditiesItems.Clear();
                if (string.IsNullOrWhiteSpace(text))
                {
                    _dgCommoditiesItems.AddRange(_commoditiesModels.Values);
                }
                else
                {
                    _dgCommoditiesItems.AddRange(_commoditiesModels.Values.Where(c =>
                        Regex.IsMatch(c.Name, text)));
                }
            }

            private async Task CreateNewCommodity() => await _hostingWindow._package.AddCommodity();

            private async Task DeleteSelectedCommodities()
            {
                foreach (var com in dgCommodities.SelectedItems.Cast<DgCommoditiesModel>().ToArray())
                {
                    await com.Commodity.Delete();
                }
            }
        }
    }
}