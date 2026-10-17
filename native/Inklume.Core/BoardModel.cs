using System.Text.Json.Serialization;

namespace Inklume.Core;

public enum ElementKind { Stroke, Rectangle, Ellipse, Line, Arrow, Sticky, Text, Image }

public sealed class InkPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public float Pressure { get; set; } = .5f;
    public float TiltX { get; set; }
    public float TiltY { get; set; }

    public InkPoint() { }
    public InkPoint(double x, double y, float pressure = .5f, float tiltX = 0, float tiltY = 0)
    { X = x; Y = y; Pressure = pressure; TiltX = tiltX; TiltY = tiltY; }
}

public readonly record struct WorldRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Intersects(WorldRect b) => X <= b.Right && Right >= b.X && Y <= b.Bottom && Bottom >= b.Y;
    public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;
    public WorldRect Inflate(double pad) => new(X - pad, Y - pad, Width + pad * 2, Height + pad * 2);
}

public sealed class BoardElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ElementKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 150;
    public double Height { get; set; } = 100;
    public string Color { get; set; } = "#243148";
    public string Fill { get; set; } = "#FFEFAD";
    public double Thickness { get; set; } = 3;
    public string Text { get; set; } = "";
    public List<InkPoint> Points { get; set; } = new();
    // Keep legacy raster data intact even before the image renderer is implemented.
    public string? EmbeddedImage { get; set; }

    [JsonIgnore]
    public WorldRect Bounds
    {
        get
        {
            if (Kind == ElementKind.Stroke && Points.Count > 0)
            {
                var minX = Points.Min(p => p.X); var minY = Points.Min(p => p.Y);
                return new(minX, minY, Math.Max(.1, Points.Max(p => p.X) - minX),
                    Math.Max(.1, Points.Max(p => p.Y) - minY));
            }
            return new(Math.Min(X, X + Width), Math.Min(Y, Y + Height),
                Math.Abs(Width), Math.Abs(Height));
        }
    }
}

public sealed class BoardDocument
{
    public string Format { get; set; } = "inklume.board.v1";
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled board";
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Background { get; set; } = "dots";
    public List<BoardElement> Elements { get; set; } = new();
}

public sealed class BoardViewport
{
    public double Zoom { get; private set; } = 1;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }

    public (double X, double Y) ToWorld(double x, double y) => ((x - OffsetX) / Zoom, (y - OffsetY) / Zoom);
    public (double X, double Y) ToScreen(double x, double y) => (x * Zoom + OffsetX, y * Zoom + OffsetY);
    public WorldRect Visible(double width, double height)
    {
        var p = ToWorld(0, 0);
        return new(p.X, p.Y, width / Zoom, height / Zoom);
    }
    public void Pan(double dx, double dy) { OffsetX += dx; OffsetY += dy; }
    public void ZoomAround(double factor, double screenX, double screenY)
    {
        var anchor = ToWorld(screenX, screenY);
        Zoom = Math.Clamp(Zoom * factor, .1, 8);
        OffsetX = screenX - anchor.X * Zoom;
        OffsetY = screenY - anchor.Y * Zoom;
    }
    public void Reset() { Zoom = 1; OffsetX = OffsetY = 0; }
    public void SetFit(WorldRect bounds, double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        Zoom = Math.Clamp(Math.Min((width - 100) / Math.Max(1, bounds.Width),
            (height - 100) / Math.Max(1, bounds.Height)), .1, 8);
        OffsetX = width / 2 - (bounds.X + bounds.Width / 2) * Zoom;
        OffsetY = height / 2 - (bounds.Y + bounds.Height / 2) * Zoom;
    }
}