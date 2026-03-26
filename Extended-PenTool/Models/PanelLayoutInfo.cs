namespace ExtendedPenTool.Models;

internal sealed class PanelLayoutInfo
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsVisible { get; set; } = true;
    public int ZIndex { get; set; }
    public bool IsTranslucent { get; set; }
    public bool IsAlwaysOnTop { get; set; }
}
