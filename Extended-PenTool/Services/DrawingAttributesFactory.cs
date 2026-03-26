using ExtendedPenTool.Abstractions;
using ExtendedPenTool.Brush;
using System.Windows.Ink;

namespace ExtendedPenTool.Services;

internal static class DrawingAttributesFactory
{
    public static DrawingAttributes Create(BrushType brushType, IBrushSettings settings) =>
        brushType switch
        {
            BrushType.Pen => CreatePen(settings),
            BrushType.Highlighter => CreateHighlighter(settings),
            BrushType.Pencil => CreatePencil(settings),
            BrushType.Eraser => CreateEraser(settings),
            _ => new DrawingAttributes(),
        };

    private static DrawingAttributes CreatePen(IBrushSettings s) => new()
    {
        Color = s.StrokeColor,
        Width = s.StrokeThickness,
        Height = s.StrokeThickness,
        StylusTip = StylusTip.Ellipse,
        FitToCurve = true,
        IgnorePressure = !s.IsPressure,
    };

    private static DrawingAttributes CreateHighlighter(IBrushSettings s) => new()
    {
        Color = s.StrokeColor,
        Width = s.StrokeThickness / 2,
        Height = s.StrokeThickness,
        IsHighlighter = true,
        StylusTip = StylusTip.Rectangle,
        FitToCurve = true,
        IgnorePressure = !s.IsPressure,
    };

    private static DrawingAttributes CreatePencil(IBrushSettings s) => new()
    {
        Color = s.StrokeColor,
        Width = s.StrokeThickness,
        Height = s.StrokeThickness,
        StylusTip = StylusTip.Ellipse,
        FitToCurve = false,
        IgnorePressure = !s.IsPressure,
    };

    private static DrawingAttributes CreateEraser(IBrushSettings s) => new()
    {
        Color = s.StrokeColor,
        Width = s.StrokeThickness,
        Height = s.StrokeThickness,
        StylusTip = StylusTip.Rectangle,
        FitToCurve = true,
        IgnorePressure = !s.IsPressure,
    };
}
