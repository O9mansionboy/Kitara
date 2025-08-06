using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;

namespace KitaraApp
{
    public partial class MainWindow : Window
    {
        private bool _isEraser = false;

        public MainWindow()
        {
            InitializeComponent();

            ClearButton.Click += (_, __) => Canvas.Clear();

            // Tool buttons: explicit tool selection
            PenButton.Click += (_, __) => SetTool(false);
            EraserButton.Click += (_, __) => SetTool(true);

            RedSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    UpdateBrushColor();
            };
            GreenSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    UpdateBrushColor();
            };
            BlueSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    UpdateBrushColor();
            };

            SaveButton.Click += async (_, __) => await SaveCanvas();
            LoadButton.Click += async (_, __) => await LoadImage();

            // Initialize with pen tool selected
            SetTool(false);

            UpdateBrushColor();
        }

        private void SetTool(bool isEraser)
        {
            _isEraser = isEraser;
            Canvas.IsEraser = isEraser;

            // Visual feedback for buttons
            PenButton.Background = isEraser ? Brushes.Transparent : Brushes.DimGray;
            EraserButton.Background = isEraser ? Brushes.DimGray : Brushes.Transparent;
        }

        void UpdateBrushColor()
        {
            var r = (byte)RedSlider.Value;
            var g = (byte)GreenSlider.Value;
            var b = (byte)BlueSlider.Value;

            Canvas.SetDrawColor(Color.FromRgb(r, g, b));

            ColorPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
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
