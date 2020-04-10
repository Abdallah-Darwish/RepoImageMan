using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MainUI
{
    public class DesigningWindow : Window
    {
        public DesigningWindow()
        {
            this.InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
