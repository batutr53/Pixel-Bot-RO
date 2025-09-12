namespace PixelAutomation.Tool.Overlay.WPF.Helpers;

public static class PointConverter
{
    public static System.Drawing.Point ToDrawingPoint(this System.Windows.Point wpfPoint)
    {
        return new System.Drawing.Point((int)wpfPoint.X, (int)wpfPoint.Y);
    }

    public static System.Windows.Point ToWpfPoint(this System.Drawing.Point drawingPoint)
    {
        return new System.Windows.Point(drawingPoint.X, drawingPoint.Y);
    }
}