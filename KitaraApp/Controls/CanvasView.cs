using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace KitaraApp.Controls
{
    public class CanvasView : Control
    {
        private WriteableBitmap _bitmap = null!;
        private uint[] _buffer = null!;

        private List<List<PixelChange>> _undoStack = new List<List<PixelChange>>();
        private List<List<PixelChange>> _redoStack = new List<List<PixelChange>>();

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

        private List<PixelChange>? _currentStrokeChanges = null;

        // Pan & zoom fields
        private double _zoom = 1.0;
        private double _panX = 0;
        private double _panY = 0;
        private Point? _panStart;
        private Point _panOrigin;

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

        private struct PixelChange
        {
            public int X;
            public int Y;
            public uint OldColor;
            public uint NewColor;
        }

        public CanvasView()
        {
            Focusable = true;
            InitializeCanvas(800, 600);

            this.AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
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
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _panStart = e.GetPosition(this);
                _panOrigin = new Point(_panX, _panY);
                e.Handled = true;
                return;
            }

            var p = e.GetPosition(this);
            int x = (int)((p.X - _panX) / _zoom);
            int y = (int)((p.Y - _panY) / _zoom);

            _isDrawing = true;
            _lastX = x;
            _lastY = y;

            _currentStrokeChanges = DrawPixel(_lastX, _lastY);

            UpdateBitmap();
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_panStart.HasValue && e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                var currentPos = e.GetPosition(this);
                _panX = _panOrigin.X + (currentPos.X - _panStart.Value.X);
                _panY = _panOrigin.Y + (currentPos.Y - _panStart.Value.Y);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            _cursorPosition = e.GetPosition(this);
            _showBrushPreview = true;

            if (_isDrawing)
            {
                int x = (int)((_cursorPosition.X - _panX) / _zoom);
                int y = (int)((_cursorPosition.Y - _panY) / _zoom);

                var changes = DrawLine(_lastX, _lastY, x, y);
                if (_currentStrokeChanges != null)
                    _currentStrokeChanges.AddRange(changes);

                _lastX = x;
                _lastY = y;

                UpdateBitmap();
            }

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _panStart = null;
                e.Handled = true;
                return;
            }

            _isDrawing = false;

            if (_currentStrokeChanges != null && _currentStrokeChanges.Count > 0)
            {
                _undoStack.Add(_currentStrokeChanges);
                _redoStack.Clear();
                _currentStrokeChanges = null;
            }

            e.Handled = true;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                BrushSize += (int)e.Delta.Y;
                BrushSize = Math.Clamp(BrushSize, 1, 64);
                InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                var oldZoom = _zoom;
                _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 0.1, 10);

                var mousePos = e.GetPosition(this);

                _panX = mousePos.X - (mousePos.X - _panX) * (_zoom / oldZoom);
                _panY = mousePos.Y - (mousePos.Y - _panY) * (_zoom / oldZoom);

                InvalidateVisual();
                e.Handled = true;
            }
        }

        private List<PixelChange> DrawPixel(int x, int y)
        {
            List<PixelChange> changes = new List<PixelChange>();

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
                        int idx = py * _canvasWidth + px;
                        uint oldColor = _buffer[idx];
                        if (oldColor != color)
                        {
                            changes.Add(new PixelChange
                            {
                                X = px,
                                Y = py,
                                OldColor = oldColor,
                                NewColor = color
                            });
                            _buffer[idx] = color;
                        }
                    }
                }
            }

            return changes;
        }

        private List<PixelChange> DrawLine(int x0, int y0, int x1, int y1)
        {
            List<PixelChange> allChanges = new List<PixelChange>();

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                var changes = DrawPixel(x0, y0);
                allChanges.AddRange(changes);

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }

            return allChanges;
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
            var matrix = new Matrix(_zoom, 0, 0, _zoom, _panX, _panY);

            // Use 'using' to push/pop transform
            using (context.PushPreTransform(matrix))
            {
                context.DrawImage(_bitmap, new Rect(0, 0, _canvasWidth, _canvasHeight));
            }

            if (_showBrushPreview)
            {
                double radius = BrushSize / 2.0 * _zoom;
                var previewPos = new Point(_cursorPosition.X, _cursorPosition.Y);

                var brushCircle = new EllipseGeometry(
                    new Rect(
                        previewPos.X - radius,
                        previewPos.Y - radius,
                        radius * 2,
                        radius * 2
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
            _undoStack.Clear();
            _redoStack.Clear();
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

            _undoStack.Clear();
            _redoStack.Clear();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.Z)
                {
                    Undo();
                    e.Handled = true;
                }
                else if (e.Key == Key.Y)
                {
                    Redo();
                    e.Handled = true;
                }
            }
        }

        public void Undo()
        {
            if (_undoStack.Count == 0)
                return;

            var lastChanges = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            foreach (var change in lastChanges)
            {
                int idx = change.Y * _canvasWidth + change.X;
                _buffer[idx] = change.OldColor;
            }

            _redoStack.Add(lastChanges);

            UpdateBitmap();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0)
                return;

            var changesToRedo = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            foreach (var change in changesToRedo)
            {
                int idx = change.Y * _canvasWidth + change.X;
                _buffer[idx] = change.NewColor;
            }

            _undoStack.Add(changesToRedo);

            UpdateBitmap();
        }
    }
}
