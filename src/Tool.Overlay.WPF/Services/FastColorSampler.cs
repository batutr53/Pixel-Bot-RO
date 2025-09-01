using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class FastColorSampler : IDisposable
{
    private Bitmap? _cachedBitmap;
    private Graphics? _graphics;
    private IntPtr _hdcDest;
    private DateTime _lastCaptureTime;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMilliseconds(50); // Cache for 50ms
    
    [DllImport("gdi32.dll")]
    static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
    
    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    private const uint SRCCOPY = 0x00CC0020;
    
    public FastColorSampler()
    {
        _cachedBitmap = new Bitmap(1920, 1080); // Max resolution
        _graphics = Graphics.FromImage(_cachedBitmap);
        _hdcDest = _graphics.GetHdc();
    }
    
    public void CaptureWindow(IntPtr hwnd)
    {
        // Check if cache is still valid
        if (DateTime.Now - _lastCaptureTime < _cacheExpiry)
            return;
            
        // Get window dimensions
        User32.GetClientRect(hwnd, out var rect);
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        
        if (width <= 0 || height <= 0) return;
        
        // Capture the window
        IntPtr hdcSrc = GetDC(hwnd);
        if (hdcSrc != IntPtr.Zero)
        {
            BitBlt(_hdcDest, 0, 0, width, height, hdcSrc, 0, 0, SRCCOPY);
            ReleaseDC(hwnd, hdcSrc);
            _lastCaptureTime = DateTime.Now;
        }
    }
    
    public Color GetColorAt(int x, int y)
    {
        if (_cachedBitmap == null || x < 0 || y < 0 || x >= _cachedBitmap.Width || y >= _cachedBitmap.Height)
            return Color.Black;
            
        return _cachedBitmap.GetPixel(x, y);
    }
    
    public bool GetMultipleColors(Point[] points, out Color[] colors)
    {
        colors = new Color[points.Length];
        
        if (_cachedBitmap == null)
            return false;
            
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].X >= 0 && points[i].Y >= 0 && 
                points[i].X < _cachedBitmap.Width && points[i].Y < _cachedBitmap.Height)
            {
                colors[i] = _cachedBitmap.GetPixel(points[i].X, points[i].Y);
            }
            else
            {
                colors[i] = Color.Black;
            }
        }
        
        return true;
    }
    
    public void Dispose()
    {
        if (_graphics != null)
        {
            _graphics.ReleaseHdc(_hdcDest);
            _graphics.Dispose();
        }
        _cachedBitmap?.Dispose();
    }
}