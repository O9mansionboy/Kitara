using Avalonia.Controls;
// For contributors: MainWindow hosts the main UI. See Controls/CanvasView for drawing logic.
namespace KitaraApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ClearButton.Click += (_, __) => Canvas.Clear();
        BrushSizeSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Value")
                Canvas.BrushSize = (int)BrushSizeSlider.Value;
        };
        EraserButton.Click += (_, __) =>
        {
            Canvas.IsEraser = !Canvas.IsEraser;
            EraserButton.Content = Canvas.IsEraser ? "Brush" : "Eraser";
        };
    }
}