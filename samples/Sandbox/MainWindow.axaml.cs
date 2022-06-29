using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Linq;
using System;

namespace Sandbox
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.AttachDevTools();

            var items = Enumerable.Range(0, 500).Select(x => new Image() { Source = LoadBitmap("avares://Sandbox/Assets/delicate-arch-896885_640.jpg") }).ToArray();
            this.GetControl<ItemsRepeater>("repeater").Items = items;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private IBitmap LoadBitmap(string uri)
        {
            var assets = AvaloniaLocator.Current!.GetService<IAssetLoader>()!;
            return new Bitmap(assets.Open(new Uri(uri)));
        }
    }
}
