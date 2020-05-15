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
using Avalonia.Input;
using DynamicData;
using JetBrains.Annotations;

namespace MainUI
{
    public partial class CommodityImageWindow : Window
    {
        private readonly CommodityPackage _package;
        private readonly CommodityTab _commodityTab;
        private readonly ImageTab _imageTab;

        public CommodityImageWindow() { }

        public CommodityImageWindow(CommodityPackage package)
        {
            InitializeComponent();
            _package = package;
            _commodityTab = new CommodityTab(this);
            _imageTab = new ImageTab(this);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        ulong _lastImageLeftClickStamp = 0;
        public void TvImages_ImageClicked(object? sender, PointerPressedEventArgs e)
        {
            var p = e.GetCurrentPoint(null);
            if (p.Properties.IsRightButtonPressed)
            {
                _imageTab.TvImages_ImageRightClicked(sender, e);
                e.Handled = true;
            }
            else if (p.Properties.IsLeftButtonPressed)
            {
                if (e.Timestamp - _lastImageLeftClickStamp <= 300)
                {
                    _lastImageLeftClickStamp = 0;
                    _imageTab.TvImages_ImageDoubleClicked(sender, e);
                    e.Handled = true;
                }
                _lastImageLeftClickStamp = e.Timestamp;
            }


        }
    }
}