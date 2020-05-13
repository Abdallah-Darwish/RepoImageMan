using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Scratch
{
    public class MainWindow : Window
    {
        private readonly Canvas grd;
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            grd = this.FindControl<Canvas>(nameof(grd));
        }

        private void GO(object sender, RoutedEventArgs e)
        {
            var y = grd.TransformedBounds;

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
