using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using RepoImageMan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Diagnostics;
using System.Linq;
using Avalonia.Interactivity;
using System.ComponentModel;
using MainUI.Controls;
using Avalonia.Threading;
using RepoImageMan.Controls;
using ReactiveUI;
using Avalonia.LogicalTree;
/*TODO
 1- Use dispatcher in this class and in DesignCImage
 2- Impliment Keys.
     
     */
namespace MainUI
{
    public class DesigningWindow : Window
    {
        private readonly List<IDisposable> _eventsSubscriptions = new List<IDisposable>();
        private readonly DesignCImage playground;
        private readonly MenuItem miDeleteSelectedCommodity, miGoToSelectedCommodity, miGoToImage, miReloadSelectedCommodity, miSaveImage, miReloadImage, miSaveAllCommodities, miReloadAllCommodities, miSaveSelectedCommodity;
        private readonly ContextMenu imgPlaygroundCTXMenu;
        private readonly ColorBox cbLabelColor;
        private readonly FontBox fbLabelFont;
        private readonly NumericUpDown nudImageContrast, nudImageBrightness, nudLabelSize;

        private string GetCommdoityShortName(ImageCommodity com)
        {
            var name = com.Name;
            return name.Length <= 10 ? name : $"{name.Substring(0, 7)}...";
        }

        private IDisposable[] _selectedCommodityNotificationSubs = new IDisposable[0];
        private void HandleSelectedCommodityChanged(ImageCommodity? com)
        {
            foreach (var sub in _selectedCommodityNotificationSubs)
            {
                sub.Dispose();
            }
            if (com == null) { return; }

            _selectedCommodityNotificationSubs = new IDisposable[]
            {
                com.Where(pn => pn == nameof(ImageCommodity.LabelColor))
                   .Subscribe(_ => cbLabelColor.SelectedColor = com.LabelColor),
                com.Where(pn => pn == nameof(ImageCommodity.Font))
                   .Subscribe(_ =>
                   {
                       fbLabelFont.SelectedFontFamily = com.Font.ToFontFamily();
                       nudLabelSize.Value = com.Font.Size;
                   })
            };
            nudLabelSize.Value = com.Font.Size;
            cbLabelColor.SelectedColor = com.LabelColor;
            fbLabelFont.SelectedFontFamily = com.Font.ToFontFamily();


        }
        public DesigningWindow() : this(null, null, null) { }
        private CImage _image;
        private CommodityImageWindow.ImageTab _imageTab;
        private CommodityImageWindow.CommodityTab _commodityTab;
        public DesigningWindow(CImage image, CommodityImageWindow.ImageTab imageTab, CommodityImageWindow.CommodityTab commodityTab)
        {
            _image = image;
            _imageTab = imageTab;
            _commodityTab = commodityTab;
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            playground = this.FindControl<DesignCImage>(nameof(playground));

            imgPlaygroundCTXMenu = this.FindControl<ContextMenu>(nameof(imgPlaygroundCTXMenu));
            cbLabelColor = this.FindControl<ColorBox>(nameof(cbLabelColor));
            fbLabelFont = this.FindControl<FontBox>(nameof(fbLabelFont));
            nudLabelSize = this.FindControl<NumericUpDown>(nameof(nudLabelSize));
            nudImageContrast = this.FindControl<NumericUpDown>(nameof(nudImageContrast));
            nudImageBrightness = this.FindControl<NumericUpDown>(nameof(nudImageBrightness));
            miGoToImage = this.FindControl<MenuItem>(nameof(miGoToImage));
            miSaveImage = this.FindControl<MenuItem>(nameof(miSaveImage));
            miReloadImage = this.FindControl<MenuItem>(nameof(miReloadImage));
            miDeleteSelectedCommodity = this.FindControl<MenuItem>(nameof(miDeleteSelectedCommodity));
            miReloadSelectedCommodity = this.FindControl<MenuItem>(nameof(miReloadSelectedCommodity));
            miSaveSelectedCommodity = this.FindControl<MenuItem>(nameof(miSaveSelectedCommodity));
            miSaveAllCommodities = this.FindControl<MenuItem>(nameof(miSaveAllCommodities));
            miReloadAllCommodities = this.FindControl<MenuItem>(nameof(miReloadAllCommodities));
            miGoToSelectedCommodity = this.FindControl<MenuItem>(nameof(miGoToSelectedCommodity));
            _eventsSubscriptions.Add(this.GetObservable(Window.ClientSizeProperty).Subscribe(sz =>
            {
                //I Have to do it manually because StackPanel will call NeasureOverride in "DesignCImage" with {INF, INF}.
                playground.Height = sz.Height - 130;
            }));
            _eventsSubscriptions.Add(playground.GetObservable(DesignCImage.SelectedCommodityProperty)
                                               .Subscribe(HandleSelectedCommodityChanged));


            _eventsSubscriptions.Add(cbLabelColor.GetObservable(ColorBox.SelectedColorProperty).Subscribe(c =>
            {
                if (playground.SelectedCommodity == null) { return; }
                playground.SelectedCommodity.LabelColor = c;
            }));
            _eventsSubscriptions.Add(fbLabelFont.GetObservable(FontBox.SelectedFontFamilyProperty).Subscribe(f =>
            {
                var sc = playground.SelectedCommodity;
                if (sc == null) { return; }
                playground.SelectedCommodity.Font = new Font(f.Name, sc.Font.Size, sc.Font.Style);
            }));
            nudImageContrast.Value = _image.Contrast;
            nudImageBrightness.Value = _image.Brightness;
            nudLabelSize.ValueChanged += NudLabelSize_ValueChanged;
            nudImageContrast.ValueChanged += NudImageContrast_ValueChanged;
            nudImageBrightness.ValueChanged += NudImageBrightness_ValueChanged;

            imgPlaygroundCTXMenu.ContextMenuOpening += ImgPlaygroundCTXMenu_ContextMenuOpening;
            miDeleteSelectedCommodity.Click += MiDeleteSelectedCommodity_Click;
            miReloadSelectedCommodity.Click += MiReloadSelectedCommodity_Click;
            miSaveSelectedCommodity.Click += MiSaveSelectedCommodity_Click;
            miReloadAllCommodities.Click += MiReloadAllCommodities_Click;
            miSaveAllCommodities.Click += MiSaveAllCommodities_Click;
            miSaveImage.Click += MiSaveImage_Click;
            miReloadImage.Click += MiReloadImage_Click;
            miGoToImage.Click += MiGoToImage_Click;
            miGoToSelectedCommodity.Click += MiGoToSelectedCommodity_Click;
        }

        private void MiGoToSelectedCommodity_Click(object? sender, RoutedEventArgs e) => _commodityTab.GoToCommodity(playground.SelectedCommodity!);

        private void MiGoToImage_Click(object? sender, RoutedEventArgs e) => _imageTab.GoToImage(playground.Image);

        private async void MiReloadImage_Click(object? sender, RoutedEventArgs e) => await playground.Image.Reload();

        private async void MiSaveImage_Click(object? sender, RoutedEventArgs e) => await playground.Image.Save();

        private async void MiSaveAllCommodities_Click(object? sender, RoutedEventArgs e) => await playground.Image.Commodities.ForEachAsync(com => com.Save());
        private async void MiReloadAllCommodities_Click(object? sender, RoutedEventArgs e) => await playground.Image.Commodities.ForEachAsync(com => com.Reload());

        private async void MiSaveSelectedCommodity_Click(object? sender, RoutedEventArgs e) => await playground.SelectedCommodity!.Save();

        private void ImgPlaygroundCTXMenu_ContextMenuOpening(object sender, CancelEventArgs e)
        {
            var sc = playground.SelectedCommodity;
            miDeleteSelectedCommodity.IsVisible = miReloadSelectedCommodity.IsVisible = miGoToSelectedCommodity.IsVisible = miSaveSelectedCommodity.IsVisible = sc != null;
            miReloadAllCommodities.IsVisible = miSaveAllCommodities.IsVisible = playground.Image.Commodities.Count > 0;
            if (miDeleteSelectedCommodity.IsVisible)
            {
                miDeleteSelectedCommodity.Header = $"Delete(DEL) {GetCommdoityShortName(sc!)}";
            }
            if (miSaveSelectedCommodity.IsVisible)
            {
                miSaveSelectedCommodity.Header = $"Save(CTRL+S) {GetCommdoityShortName(sc!)}";
            }
            if (miReloadSelectedCommodity.IsVisible)
            {
                miReloadSelectedCommodity.Header = $"Reload(CTRL+R) {GetCommdoityShortName(sc!)}";
            }
            if (miGoToSelectedCommodity.IsVisible)
            {
                miGoToSelectedCommodity.Header = $"Go to {GetCommdoityShortName(sc!)}";
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            playground.Init(_image);
        }
        private void NudImageBrightness_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => playground.Image.Brightness = (float)e.NewValue;

        private void NudImageContrast_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => playground.Image.Contrast = (float)e.NewValue;

        private void NudLabelSize_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            var sc = playground.SelectedCommodity;
            if (sc == null) { return; }
            sc.Font = new Font(sc.Font.FamilyName, (float)e.NewValue, sc.Font.Style);
        }

        private async void MiReloadSelectedCommodity_Click(object? sender, RoutedEventArgs e) => await playground.SelectedCommodity.Reload();

        private async void MiDeleteSelectedCommodity_Click(object? sender, RoutedEventArgs e) => await playground.SelectedCommodity.Delete();


        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            playground.Dispose();
        }
        
    }
}
