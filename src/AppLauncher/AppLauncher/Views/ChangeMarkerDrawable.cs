using AppLauncher.Models;

namespace AppLauncher.Views;

public sealed class ChangeMarkerDrawable : IDrawable
{
    private static readonly Color AdditionColor = Color.FromArgb("#3FB950");
    private static readonly Color RemovalColor = Color.FromArgb("#E5534B");
    private static readonly Color TrackColor = Color.FromArgb("#171A20");
    private static readonly Color ViewportColor = Color.FromArgb("#3A414C");

    public IReadOnlyList<ChangeMarker> Markers { get; set; } = Array.Empty<ChangeMarker>();

    public double ViewportStart { get; set; }

    public double ViewportEnd { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = TrackColor;
        canvas.FillRectangle(dirtyRect);

        if (this.Markers.Count == 0)
        {
            return;
        }

        float trackTop = dirtyRect.Top + 2;
        float trackHeight = dirtyRect.Height - 4;

        if (trackHeight <= 0)
        {
            return;
        }

        if (this.ViewportEnd > this.ViewportStart)
        {
            canvas.FillColor = ViewportColor;
            float viewportTop = trackTop + (float)(this.ViewportStart * trackHeight);
            float viewportHeight = (float)((this.ViewportEnd - this.ViewportStart) * trackHeight);
            canvas.FillRectangle(dirtyRect.Left, viewportTop, dirtyRect.Width, Math.Max(viewportHeight, 6));
        }

        float markerWidth = dirtyRect.Width - 4;

        foreach (ChangeMarker marker in this.Markers)
        {
            canvas.FillColor = marker.IsAddition ? AdditionColor : RemovalColor;

            float top = trackTop + (float)(marker.Start * trackHeight);
            float height = (float)((marker.End - marker.Start) * trackHeight);

            canvas.FillRoundedRectangle(dirtyRect.Left + 2, top, markerWidth, Math.Max(height, 2.5f), 1.5f);
        }
    }
}
