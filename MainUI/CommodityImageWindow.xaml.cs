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
using DynamicData;
using JetBrains.Annotations;

namespace MainUI
{
  

    public partial class CommodityImageWindow : Window
    {
        private readonly CommodityPackage _package;
        private readonly CommodityTab _commodityTab;
        public CommodityImageWindow()
        {
        }

        private IDisposable x;
        public CommodityImageWindow(CommodityPackage package)
        {
            InitializeComponent();
            _package = package;
            _commodityTab = new CommodityTab(this);

            Closed += (sender, args) => _package.Dispose();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}