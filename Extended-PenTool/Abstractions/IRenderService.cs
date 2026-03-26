using System.Windows.Media.Imaging;

namespace ExtendedPenTool.Abstractions;

internal interface IRenderService : IDisposable
{
    BitmapSource Render();
}
