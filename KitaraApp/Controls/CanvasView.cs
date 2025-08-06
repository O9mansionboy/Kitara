using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KitaraApp.Controls
{
    public class CanvasView : Control
    {
        private WriteableBitmap _bitmap;
        private uint[] _buffer;

        private bool _isDrawing;
        private int _lastX, _lastY;

        private int _canvasWidth;
        private int _canvasHeight;

        private const uint DefaultDrawColor = 0xFF000000; // Black
        private const uint ClearColor = 0xFFFFFFFF;       // White

        private uint _drawColor = DefaultDrawColor;
        private int _brushSize = 4;
        private bool _isEraser = false;

        private Point _cursorPosition;
        private bool _showBrushPreview = false;


        public int BrushSize
        {
            get => _brushSize;
            set => _brushSize = Math.Clamp(value, 1, 64);
        }

        public bool IsEraser
        {
            get => _isEraser;
            set => _isEraser = value;
        }

        public CanvasView()
        {
            Focusable = true;
            InitializeCanvas(800, 600);
        }

        private void InitializeCanvas(int width, int height)
        {
            _canvasWidth = width;
            _canvasHeight = height;

            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888);

            _buffer = new uint[width * height];
            Array.Fill(_buffer, ClearColor);

            UpdateBitmap();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var p = e.GetPosition(this);
            _isDrawing = true;
            _lastX = (int)p.X;
            _lastY = (int)p.Y;
            DrawPixel(_lastX, _lastY);
            UpdateBitmap();
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            _cursorPosition = e.GetPosition(this);
            _showBrushPreview = true;

            if (_isDrawing)
            {
                int x = (int)_cursorPosition.X;
                int y = (int)_cursorPosition.Y;

                DrawLine(_lastX, _lastY, x, y);
                _lastX = x;
                _lastY = y;

                UpdateBitmap();
            }

            InvalidateVisual();
            e.Handled = true;
        }



        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _isDrawing = false;
            e.Handled = true;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                BrushSize += (int)e.Delta.Y; // Up = increase
                BrushSize = Math.Clamp(BrushSize, 1, 64);
                InvalidateVisual(); // refresh the preview circle
                e.Handled = true;
            }
        }


        private void DrawPixel(int x, int y)
        {
            uint color = _isEraser ? ClearColor : _drawColor;
            int r = _brushSize / 2;

            int minX = Math.Max(0, x - r);
            int maxX = Math.Min(_canvasWidth - 1, x + r);
            int minY = Math.Max(0, y - r);
            int maxY = Math.Min(_canvasHeight - 1, y + r);

            int rr = r * r;

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    int dx = px - x;
                    int dy = py - y;

                    if (dx * dx + dy * dy <= rr)
                    {
                        _buffer[py * _canvasWidth + px] = color;
                    }
                }
            }
        }

        private void DrawLine(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                DrawPixel(x0, y0);
                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void UpdateBitmap()
        {
            using var fb = _bitmap.Lock();

            byte[] bytes = new byte[_buffer.Length * sizeof(uint)];
            Buffer.BlockCopy(_buffer, 0, bytes, 0, bytes.Length);
            Marshal.Copy(bytes, 0, fb.Address, bytes.Length);

            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            context.DrawImage(_bitmap, new Rect(0, 0, _canvasWidth, _canvasHeight));
            if (_showBrushPreview)
            {
                double radius = BrushSize / 2.0;
                var brushCircle = new EllipseGeometry(
                    new Rect(
                        _cursorPosition.X - radius,
                        _cursorPosition.Y - radius,
                        BrushSize, BrushSize
                    )
                );

                context.DrawGeometry(
                    null,
                    new Pen(Brushes.Gray, 1, dashStyle: new DashStyle(new[] { 2.0, 2.0 }, 0)),
                    brushCircle
                );
            }
        }

        public void Clear()
        {
            Array.Fill(_buffer, ClearColor);
            UpdateBitmap();
        }

        public void SetDrawColor(Color color)
        {
            _drawColor = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        public void SaveAsPng(Stream stream)
        {
            _bitmap.Save(stream);
        }

        public void LoadImage(Stream stream)
        {
            var bitmap = new Bitmap(stream);

            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;
            int stride = width * 4;
            int bufferSize = stride * height;

            IntPtr unmanagedBuffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                bitmap.CopyPixels(
                    new PixelRect(0, 0, width, height),
                    unmanagedBuffer,
                    bufferSize,
                    stride);

                byte[] bytes = new byte[bufferSize];
                Marshal.Copy(unmanagedBuffer, bytes, 0, bufferSize);

                InitializeCanvas(width, height);
                Buffer.BlockCopy(bytes, 0, _buffer, 0, bufferSize);
                UpdateBitmap();
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedBuffer);
            }

            InvalidateVisual();
        }
    }
}
