using ExtendedPenTool.Abstractions;
using ExtendedPenTool.Plugin;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace ExtendedPenTool.Services;

internal sealed class RenderService : IRenderService
{
    private readonly PenShapeParameter parameter;
    private readonly IEditorInfo info;
    private readonly ITimelineSourceAndDevices source;

    public RenderService(PenShapeParameter parameter, IEditorInfo info)
    {
        this.parameter = parameter;
        this.info = info;
        source = info.CreateTimelineVideoSource();
    }

    public BitmapSource Render()
    {
        parameter.IsEditing = true;
        try
        {
            var time = info.ItemPosition.Time < TimeSpan.Zero
                ? info.ItemPosition.Time
                : info.ItemDuration.Time < info.ItemPosition.Time
                    ? info.VideoInfo.GetTimeFrom(info.ItemPosition.Frame + info.ItemDuration.Frame - 1)
                    : info.TimelinePosition.Time;

            source.Update(time, TimelineSourceUsage.Paused);
            return source.RenderBitmapSource();
        }
        finally
        {
            parameter.IsEditing = false;
        }
    }

    public void Dispose()
    {
        (source as IDisposable)?.Dispose();
    }
}
