using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tester
{
    public class DesigningWindow : Window
    {
        public DesigningWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}