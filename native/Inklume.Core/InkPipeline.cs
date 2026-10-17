namespace Inklume.Core;

/// <summary>
/// Coordinates are stored in world space. Samples are resampled by screen-space distance
/// to avoid visible gaps on fast stylus movements without inserting thousands of points.
/// Input may come from any pointing device, and the original pressure and tilt survive edits.
/// </summary>
public static class InkPipeline
{
    public const int MaxSamples = 200_000;

    public static bool Append(List<InkPoint> points, InkPoint incoming, double zoom)
    {
        if (!double.IsFinite(incoming.X) || !double.IsFinite(incoming.Y) || points.Count >= MaxSamples) return false;
        incoming.Pressure = Math.Clamp(float.IsFinite(incoming.Pressure) ? incoming.Pressure : .5f, .05f, 1f);
        incoming.TiltX = float.IsFinite(incoming.TiltX) ? Math.Clamp(incoming.TiltX, -90f, 90f) : 0;
        incoming.TiltY = float.IsFinite(incoming.TiltY) ? Math.Clamp(incoming.TiltY, -90f, 90f) : 0;
        if (points.Count == 0) { points.Add(incoming); return true; }
        var last = points[^1];
        var distance = double.Hypot(incoming.X - last.X, incoming.Y - last.Y) * Math.Clamp(zoom, .1, 8);
        if (distance < .65) return false;
        // Preserve endpoint position for cursor tracking; only interpolate large gaps.
        var splits = Math.Clamp((int)Math.Ceiling(distance / 4), 1, 72);
        splits = Math.Min(splits, MaxSamples - points.Count);
        for (var j = 1; j <= splits; j++)
        {
            var t = (double)j / splits;
            points.Add(Interpolate(last, incoming, t));
        }
        return true;
    }

    public static InkPoint Interpolate(InkPoint a, InkPoint b, double t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        (float)(a.Pressure + (b.Pressure - a.Pressure) * t),
        (float)(a.TiltX + (b.TiltX - a.TiltX) * t),
        (float)(a.TiltY + (b.TiltY - a.TiltY) * t));

    /// <summary>Returns null if nothing was erased; a possibly empty list otherwise.</summary>
    public static List<BoardElement>? PartialErase(BoardElement element, double x, double y, double radius)
    {
        if (element.Kind != ElementKind.Stroke || element.Points.Count == 0) return null;
        radius = Math.Max(.5, radius + element.Thickness * .35);
        if (!element.Bounds.Inflate(radius).Contains(x, y)) return null;
        var chunks = new List<List<InkPoint>>();
        var current = new List<InkPoint>();
        bool erased = false;
        var previous = element.Points[0];
        bool keep = double.Hypot(previous.X - x, previous.Y - y) > radius;
        if (keep) current.Add(previous); else erased = true;
        for (var i = 1; i < element.Points.Count; i++)
        {
            var next = element.Points[i];
            var segment = double.Hypot(next.X - previous.X, next.Y - previous.Y);
            var count = Math.Clamp((int)Math.Ceiling(segment / Math.Max(1, radius / 2)), 1, 1024);
            for (var s = 1; s <= count; s++)
            {
                var pt = s == count ? next : Interpolate(previous, next, (double)s / count);
                bool nextKeep = double.Hypot(pt.X - x, pt.Y - y) > radius;
                if (nextKeep)
                {
                    // A first point after the hole starts a new disconnected stroke.
                    current.Add(pt);
                }
                else
                {
                    erased = true;
                    if (current.Count != 0) { chunks.Add(current); current = new(); }
                }
                keep = nextKeep;
            }
            previous = next;
        }
        if (current.Count > 0) chunks.Add(current);
        if (!erased) return null;
        return chunks.Where(c => c.Count > 0).Select(chunk => new BoardElement
        {
            Kind = ElementKind.Stroke, Color = element.Color, Fill = element.Fill,
            Thickness = element.Thickness, Points = chunk,
            X = chunk.Min(p => p.X), Y = chunk.Min(p => p.Y),
            Width = Math.Max(.1, chunk.Max(p => p.X) - chunk.Min(p => p.X)),
            Height = Math.Max(.1, chunk.Max(p => p.Y) - chunk.Min(p => p.Y))
        }).ToList();
    }
}