using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;

namespace KitaraApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ClearButton.Click += (_, __) => Canvas.Clear();
            EraserButton.Click += (_, __) => Canvas.IsEraser = !Canvas.IsEraser;

            BrushSizeSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    Canvas.BrushSize = (int)BrushSizeSlider.Value;
            };

            RedSlider.PropertyChanged += (_, __) => UpdateBrushColor();
            GreenSlider.PropertyChanged += (_, __) => UpdateBrushColor();
            BlueSlider.PropertyChanged += (_, __) => UpdateBrushColor();

            SaveButton.Click += async (_, __) => await SaveCanvas();
            LoadButton.Click += async (_, __) => await LoadImage();

            // Initialize brush color
            UpdateBrushColor();
        }

        void UpdateBrushColor()
        {
            var r = (byte)RedSlider.Value;
            var g = (byte)GreenSlider.Value;
            var b = (byte)BlueSlider.Value;

            Canvas.SetDrawColor(Color.FromRgb(r, g, b));
        }

        private async Task SaveCanvas()
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Image",
                SuggestedFileName = "drawing.png",
                FileTypeChoices = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } }
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                Canvas.SaveAsPng(stream);
            }
        }

        private async Task LoadImage()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Image",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } } }
            });

            if (files.Count > 0)
            {
                await using var stream = await files[0].OpenReadAsync();
                Canvas.LoadImage(stream);
            }
        }
    }
}
