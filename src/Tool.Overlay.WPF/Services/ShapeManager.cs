using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PixelAutomation.Tool.Overlay.WPF.Models;
using PixelAutomation.Tool.Overlay.WPF.Helpers;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class ShapeManager : IDisposable
{
    private readonly Canvas _canvas;
    private readonly List<ProbeShape> _shapes = new();
    private ProbeShape? _selectedShape;
    private System.Windows.Point _dragStart;
    private bool _isDragging;
    
    public bool SnapEnabled { get; set; }
    public int SnapGrid { get; set; } = 5;
    public ProbeShape? SelectedShape => _selectedShape;

    public ShapeManager(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
    }

    public void AddPointProbe(string name, int x, int y, int box = 5)
    {
        var probe = new ProbeShape
        {
            Name = name,
            Kind = "point",
            X = x,
            Y = y,
            Box = box
        };

        var ellipse = new Ellipse
        {
            Width = box * 2,
            Height = box * 2,
            Stroke = Brushes.Lime,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(32, 0, 255, 0))
        };

        Canvas.SetLeft(ellipse, x - box);
        Canvas.SetTop(ellipse, y - box);
        
        probe.Visual = ellipse;
        _canvas.Children.Add(ellipse);
        _shapes.Add(probe);
        
        ellipse.Tag = probe;
        ellipse.MouseLeftButtonDown += Shape_MouseLeftButtonDown;
    }

    public void AddRectProbe(string name, int x, int y, int width, int height)
    {
        var probe = new ProbeShape
        {
            Name = name,
            Kind = "rect",
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(32, 0, 255, 255))
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        
        probe.Visual = rect;
        _canvas.Children.Add(rect);
        _shapes.Add(probe);
        
        rect.Tag = probe;
        rect.MouseLeftButtonDown += Shape_MouseLeftButtonDown;
        
        AddResizeHandles(probe);
    }

    private void AddResizeHandles(ProbeShape probe)
    {
        if (probe.Kind != "rect" || probe.Visual == null)
            return;

        var handles = new[]
        {
            new { X = 0.0, Y = 0.0 },
            new { X = 0.5, Y = 0.0 },
            new { X = 1.0, Y = 0.0 },
            new { X = 0.0, Y = 0.5 },
            new { X = 1.0, Y = 0.5 },
            new { X = 0.0, Y = 1.0 },
            new { X = 0.5, Y = 1.0 },
            new { X = 1.0, Y = 1.0 }
        };

        foreach (var handle in handles)
        {
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Cursor = Cursors.SizeAll,
                Visibility = Visibility.Collapsed
            };

            var x = probe.X + (probe.Width ?? 0) * handle.X - 4;
            var y = probe.Y + (probe.Height ?? 0) * handle.Y - 4;
            
            Canvas.SetLeft(ellipse, x);
            Canvas.SetTop(ellipse, y);
            
            _canvas.Children.Add(ellipse);
            probe.Handles.Add(ellipse);
        }
    }

    public void UpdateProbeValues(IntPtr hwnd)
    {
        
    }

    public List<ProbeShape> GetAllShapes()
    {
        return new List<ProbeShape>(_shapes);
    }

    public void ClearAll()
    {
        foreach (var shape in _shapes)
        {
            if (shape.Visual != null)
                _canvas.Children.Remove(shape.Visual);
            
            foreach (var handle in shape.Handles)
                _canvas.Children.Remove(handle);
        }
        
        _shapes.Clear();
        _selectedShape = null;
    }

    public void DeleteSelected()
    {
        if (_selectedShape != null)
        {
            if (_selectedShape.Visual != null)
                _canvas.Children.Remove(_selectedShape.Visual);
            
            foreach (var handle in _selectedShape.Handles)
                _canvas.Children.Remove(handle);
            
            _shapes.Remove(_selectedShape);
            _selectedShape = null;
        }
    }

    private void SelectShape(ProbeShape? shape)
    {
        if (_selectedShape != null)
        {
            SetShapeSelected(_selectedShape, false);
        }

        _selectedShape = shape;

        if (_selectedShape != null)
        {
            SetShapeSelected(_selectedShape, true);
        }
    }

    private void SetShapeSelected(ProbeShape shape, bool selected)
    {
        if (shape.Visual is Shape visual)
        {
            visual.Stroke = selected ? Brushes.Red : (shape.Kind == "point" ? Brushes.Lime : Brushes.Cyan);
            visual.StrokeThickness = selected ? 3 : 2;
        }

        foreach (var handle in shape.Handles)
        {
            handle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Shape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ProbeShape shape)
        {
            SelectShape(shape);
            _dragStart = e.GetPosition(_canvas);
            _isDragging = true;
            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == _canvas)
        {
            SelectShape(null);
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _selectedShape != null && _selectedShape.Visual != null)
        {
            var currentPos = e.GetPosition(_canvas);
            var deltaX = currentPos.X - _dragStart.X;
            var deltaY = currentPos.Y - _dragStart.Y;

            var newX = _selectedShape.X + deltaX;
            var newY = _selectedShape.Y + deltaY;

            if (SnapEnabled)
            {
                newX = Math.Round(newX / SnapGrid) * SnapGrid;
                newY = Math.Round(newY / SnapGrid) * SnapGrid;
            }

            _selectedShape.X = (int)newX;
            _selectedShape.Y = (int)newY;

            if (_selectedShape.Kind == "point")
            {
                Canvas.SetLeft(_selectedShape.Visual, newX - _selectedShape.Box);
                Canvas.SetTop(_selectedShape.Visual, newY - _selectedShape.Box);
            }
            else
            {
                Canvas.SetLeft(_selectedShape.Visual, newX);
                Canvas.SetTop(_selectedShape.Visual, newY);
            }

            UpdateHandlePositions(_selectedShape);
            _dragStart = currentPos;
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            Mouse.Capture(null);
        }
    }

    private void UpdateHandlePositions(ProbeShape shape)
    {
        if (shape.Kind != "rect" || shape.Handles.Count != 8)
            return;

        var positions = new[]
        {
            new { X = 0.0, Y = 0.0 },
            new { X = 0.5, Y = 0.0 },
            new { X = 1.0, Y = 0.0 },
            new { X = 0.0, Y = 0.5 },
            new { X = 1.0, Y = 0.5 },
            new { X = 0.0, Y = 1.0 },
            new { X = 0.5, Y = 1.0 },
            new { X = 1.0, Y = 1.0 }
        };

        for (int i = 0; i < shape.Handles.Count; i++)
        {
            var x = shape.X + (shape.Width ?? 0) * positions[i].X - 4;
            var y = shape.Y + (shape.Height ?? 0) * positions[i].Y - 4;
            
            Canvas.SetLeft(shape.Handles[i], x);
            Canvas.SetTop(shape.Handles[i], y);
        }
    }

    public void Dispose()
    {
        _canvas.MouseLeftButtonDown -= Canvas_MouseLeftButtonDown;
        _canvas.MouseMove -= Canvas_MouseMove;
        _canvas.MouseLeftButtonUp -= Canvas_MouseLeftButtonUp;
    }
}