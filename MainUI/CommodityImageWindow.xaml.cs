using System.Collections.ObjectModel;
using System.Dynamic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MainUI
{
    public class CommodityImageWindow : Window
    {
        private DataGrid dgCommodities;
        public CommodityImageWindow()
        {
            InitializeComponent();
            dgCommodities = this.Get<DataGrid>(nameof(dgCommodities));
            var coms =  new ObservableCollection<object>();
            coms.Add(new {Export = true, Name = "FUCK XAML"} );
            coms.Add(new {Export = false, Name = "FUCK Avalonia"} );
            dgCommodities.Items = coms;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}