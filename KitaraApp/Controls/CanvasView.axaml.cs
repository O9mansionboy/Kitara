using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;

namespace KitaraApp.Controls
{
    public class CanvasView : Control
    {
        private WriteableBitmap _bitmap;
        private uint[] _buffer;
        private bool _isDrawing;
        private int _lastX, _lastY;
        private const int CanvasWidth = 800;
        private const int CanvasHeight = 600;
        private const uint DefaultDrawColor = 0xFF000000; // Black
        private const uint ClearColor = 0xFFFFFFFF; // White

        private uint _drawColor = DefaultDrawColor;
        private int _brushSize = 4;
        private bool _isEraser = false;

        public int BrushSize
        {
            get => _brushSize;
            set { _brushSize = Math.Max(1, Math.Min(64, value)); }
        }

        public bool IsEraser
        {
            get => _isEraser;
            set => _isEraser = value;
        }

        public CanvasView()
        {
            Focusable = true;
            _bitmap = new WriteableBitmap(new PixelSize(CanvasWidth, CanvasHeight), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);
            _buffer = new uint[CanvasWidth * CanvasHeight];
            for (int i = 0; i < _buffer.Length; i++)
                _buffer[i] = ClearColor;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var p = e.GetPosition(this);
            _isDrawing = true;
            _lastX = (int)p.X;
            _lastY = (int)p.Y;
            DrawPixel(_lastX, _lastY);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_isDrawing) return;
            var p = e.GetPosition(this);
            int x = (int)p.X;
            int y = (int)p.Y;
            DrawLine(_lastX, _lastY, x, y);
            _lastX = x;
            _lastY = y;
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _isDrawing = false;
            e.Handled = true;
        }

        private void DrawPixel(int x, int y)
        {
            uint color = _isEraser ? ClearColor : _drawColor;
            int r = _brushSize / 2;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = x + dx;
                    int py = y + dy;
                    if (px < 0 || py < 0 || px >= CanvasWidth || py >= CanvasHeight)
                        continue;
                    // Circle brush
                    if (dx * dx + dy * dy <= r * r)
                        _buffer[py * CanvasWidth + px] = color;
                }
            }
            UpdateBitmap();
        }

        // Bresenham's line algorithm for smooth drawing
        private void DrawLine(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;
            while (true)
            {
                DrawPixel(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void UpdateBitmap()
        {
            using (var fb = _bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    Array.ConvertAll(_buffer, val => unchecked((int)val)),
                    0,
                    fb.Address,
                    _buffer.Length
                );
            }
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (_bitmap != null)
            {
                context.DrawImage(_bitmap, new Rect(0, 0, CanvasWidth, CanvasHeight));
            }
        }

        /// <summary>
        /// Clears the canvas to blank (all pixels transparent/white).
        /// </summary>
        public void Clear()
        {
            Console.WriteLine("[DEBUG] Clear called");
            for (int i = 0; i < _buffer.Length; i++)
                _buffer[i] = ClearColor;
            UpdateBitmap();
        }
    }
}
