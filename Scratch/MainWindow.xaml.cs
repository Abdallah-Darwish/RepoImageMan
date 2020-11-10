using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Scratch
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
