using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Inklume.Core;

namespace Inklume.App;

public enum CanvasTool { Pen, Highlighter, Select, Pan, Eraser, Rectangle, Ellipse, Sticky, Text }

/// <summary>Single Skia-backed Avalonia visual; no SVG/DOM tree is rebuilt while drawing.
/// Only spatially visible elements are submitted to the native drawing context.</summary>
public sealed class BoardCanvas : Control
{
    public BoardEngine Engine { get; private set; }
    public BoardViewport View { get; } = new();
    public CanvasTool Tool { get; set; } = CanvasTool.Pen;
    public string InkColor { get; set; } = "#283955";
    public double InkWidth { get; set; } = 3;
    public string? SelectedId { get; private set; }
    public event Action? ViewChanged;
    private BoardElement? _activeElement;
    private BoardElement? _movingElement;
    private List<InkPoint>? _originalPoints;
    private double _startX, _startY, _originalX, _originalY;
    private Point _lastScreen;
    private bool _dragging, _panning, _changed;

    public BoardCanvas(BoardEngine engine)
    {
        Engine = engine;
        Focusable = true;
        ClipToBounds = true;
    }

    public void ReplaceDocument(BoardDocument board)
    {
        Engine = new BoardEngine(board);
        SelectedId = null; View.Reset(); InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void Redraw() => InvalidateVisual();
    private Point Screen(InkPoint p) { var q = View.ToScreen(p.X, p.Y); return new(q.X, q.Y); }
    private Point Screen(double x, double y) { var q = View.ToScreen(x, y); return new(q.X, q.Y); }
    private Rect ScreenRect(WorldRect b)
    {
        var origin = Screen(b.X, b.Y);
        return new Rect(origin.X, origin.Y, Math.Max(1, b.Width * View.Zoom), Math.Max(1, b.Height * View.Zoom));
    }
    private static IBrush SafeBrush(string value, IBrush fallback)
    {
        try { return new SolidColorBrush(Color.Parse(value)); }
        catch (FormatException) { return fallback; }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        var zoom = View.Zoom;
        var spacing = 24 * zoom;
        if (spacing >= 11)
        {
            var dotBrush = new SolidColorBrush(Color.Parse("#DDE3EF"));
            var x = ((View.ToScreen(0, 0).X % spacing) + spacing) % spacing;
            var y = ((View.ToScreen(0, 0).Y % spacing) + spacing) % spacing;
            for (var px = x; px < Bounds.Width; px += spacing)
            for (var py = y; py < Bounds.Height; py += spacing)
                context.DrawEllipse(dotBrush, null, new Point(px, py), 1, 1);
        }

        var visible = View.Visible(Bounds.Width, Bounds.Height).Inflate(30 / zoom);
        var candidates = Engine.Visible(visible).ToList();
        // The just-created/moving element is not in the spatial index until pointer-up.
        if (_activeElement != null && candidates.All(x => x.Id != _activeElement.Id)) candidates.Add(_activeElement);
        if (_movingElement != null && candidates.All(x => x.Id != _movingElement.Id)) candidates.Add(_movingElement);
        foreach (var obj in candidates)
            if (obj.Bounds.Intersects(visible) || obj == _activeElement || obj == _movingElement)
                DrawElement(context, obj);

        if (SelectedId != null)
        {
            var selected = Engine.Document.Elements.FirstOrDefault(e => e.Id == SelectedId);
            if (selected != null)
            {
                var pen = new Pen(SafeBrush("#6153D8", Brushes.Blue), 1.6) { DashStyle = DashStyle.Dash };
                context.DrawRectangle(null, pen, ScreenRect(selected.Bounds.Inflate(6 / zoom)));
            }
        }
    }

    private void DrawElement(DrawingContext context, BoardElement e)
    {
        var stroke = SafeBrush(e.Color, Brushes.DarkSlateBlue);
        var fill = SafeBrush(e.Fill, Brushes.LightYellow);
        var pen = new Pen(stroke, Math.Max(.75, e.Thickness * View.Zoom));
        var rect = ScreenRect(e.Bounds);
        switch (e.Kind)
        {
            case ElementKind.Stroke:
                for (var i = 1; i < e.Points.Count; i++)
                {
                    var p1 = e.Points[i - 1]; var p2 = e.Points[i];
                    // Keep pressure samples; renderer varies local width without mutating source points.
                    var pressure = Math.Clamp((p1.Pressure + p2.Pressure) / 2f, .1f, 1f);
                    var width = Math.Max(.75, e.Thickness * View.Zoom * (.55 + .9 * pressure));
                    context.DrawLine(new Pen(stroke, width) { LineCap = PenLineCap.Round }, Screen(p1), Screen(p2));
                }
                if (e.Points.Count == 1)
                    context.DrawEllipse(stroke, null, Screen(e.Points[0]), e.Thickness * View.Zoom / 2, e.Thickness * View.Zoom / 2);
                break;
            case ElementKind.Rectangle: context.DrawRectangle(fill, pen, rect, 7 * View.Zoom); break;
            case ElementKind.Ellipse: context.DrawEllipse(fill, pen, rect.Center, rect.Width / 2, rect.Height / 2); break;
            case ElementKind.Line:
            case ElementKind.Arrow:
                context.DrawLine(pen, Screen(e.X, e.Y), Screen(e.X + e.Width, e.Y + e.Height));
                break;
            case ElementKind.Sticky:
                context.DrawRectangle(fill, null, rect, 9 * View.Zoom);
                DrawText(context, e.Text, rect.X + 12 * View.Zoom, rect.Y + 12 * View.Zoom, 16 * View.Zoom, Brushes.DarkSlateGray);
                break;
            case ElementKind.Text:
                DrawText(context, e.Text, rect.X, rect.Y, 21 * View.Zoom, stroke);
                break;
            case ElementKind.Image:
                context.DrawRectangle(Brushes.GhostWhite, new Pen(Brushes.LightGray, 1), rect);
                DrawText(context, "Image · preserved", rect.X + 8, rect.Y + 8, 13, Brushes.Gray);
                break;
        }
    }

    private static void DrawText(DrawingContext ctx, string text, double x, double y, double size, IBrush foreground)
    {
        var lines = (text ?? "").Split('\n').Take(8);
        var offset = 0d;
        foreach (var line in lines)
        {
            var formatted = new FormattedText(line, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), Math.Clamp(size, 8, 84), foreground);
            ctx.DrawText(formatted, new Point(x, y + offset));
            offset += size * 1.33;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pp = e.GetCurrentPoint(this);
        if (!pp.Properties.IsLeftButtonPressed && !pp.Properties.IsMiddleButtonPressed) return;
        _lastScreen = pp.Position;
        _panning = Tool == CanvasTool.Pan || pp.Properties.IsMiddleButtonPressed;
        _dragging = true; _changed = false;
        e.Pointer.Capture(this);
        var at = View.ToWorld(_lastScreen.X, _lastScreen.Y);
        _startX = at.X; _startY = at.Y;
        if (_panning) return;
        if (Tool == CanvasTool.Select)
        {
            var hit = Engine.HitTest(at.X, at.Y, 8, View.Zoom);
            SelectedId = hit?.Id;
            _movingElement = hit;
            if (hit != null)
            {
                Engine.BeginEdit(); _originalX = hit.X; _originalY = hit.Y;
                if (hit.Kind == ElementKind.Stroke) _originalPoints = hit.Points.Select(p => new InkPoint(p.X, p.Y, p.Pressure, p.TiltX, p.TiltY)).ToList();
            }
            InvalidateVisual(); return;
        }
        Engine.BeginEdit();
        if (Tool == CanvasTool.Eraser)
        {
            var hit = Engine.HitTest(at.X, at.Y, 12, View.Zoom);
            if (hit != null) { _changed = Engine.Remove(hit.Id); SelectedId = null; InvalidateVisual(); }
            return;
        }
        _activeElement = new BoardElement
        {
            Kind = Tool switch
            {
                CanvasTool.Rectangle => ElementKind.Rectangle, CanvasTool.Ellipse => ElementKind.Ellipse,
                CanvasTool.Sticky => ElementKind.Sticky, CanvasTool.Text => ElementKind.Text,
                _ => ElementKind.Stroke
            },
            X = at.X, Y = at.Y, Width = 1, Height = 1, Thickness = Tool == CanvasTool.Highlighter ? InkWidth * 3 : InkWidth,
            Color = Tool == CanvasTool.Highlighter ? "#97A4EB" : InkColor,
            Fill = Tool == CanvasTool.Sticky ? "#FFEFAD" : "#FFFFFF",
            Text = Tool == CanvasTool.Sticky ? "New note" : Tool == CanvasTool.Text ? "Text" : ""
        };
        if (_activeElement.Kind == ElementKind.Stroke) _activeElement.Points.Add(Sample(e, at));
        else if (_activeElement.Kind is ElementKind.Sticky or ElementKind.Text)
        {
            _activeElement.Width = 190; _activeElement.Height = 125;
        }
        Engine.Add(_activeElement); _changed = true; InvalidateVisual();
    }

    private InkPoint Sample(PointerEventArgs e, (double X, double Y) at)
    {
        var point = e.GetCurrentPoint(this);
        var props = point.Properties;
        return new InkPoint(at.X, at.Y, point.Pointer.Type == PointerType.Pen ? Math.Clamp(props.Pressure, .1f, 1f) : .5f,
            props.XTilt, props.YTilt);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        var now = e.GetPosition(this);
        var at = View.ToWorld(now.X, now.Y);
        if (_panning)
        {
            View.Pan(now.X - _lastScreen.X, now.Y - _lastScreen.Y);
            _lastScreen = now; InvalidateVisual(); ViewChanged?.Invoke(); return;
        }
        if (_movingElement != null)
        {
            var dx = at.X - _startX; var dy = at.Y - _startY;
            _movingElement.X = _originalX + dx; _movingElement.Y = _originalY + dy;
            if (_originalPoints != null)
                _movingElement.Points = _originalPoints.Select(p => new InkPoint(p.X + dx, p.Y + dy, p.Pressure, p.TiltX, p.TiltY)).ToList();
            _changed = true;
        }
        else if (_activeElement != null)
        {
            if (_activeElement.Kind == ElementKind.Stroke)
            {
                var last = _activeElement.Points[^1];
                if (Math.Hypot(at.X - last.X, at.Y - last.Y) > .5 / View.Zoom)
                    _activeElement.Points.Add(Sample(e, at));
            }
            else if (_activeElement.Kind is ElementKind.Rectangle or ElementKind.Ellipse)
            {
                _activeElement.X = Math.Min(_startX, at.X);
                _activeElement.Y = Math.Min(_startY, at.Y);
                _activeElement.Width = Math.Max(1, Math.Abs(at.X - _startX));
                _activeElement.Height = Math.Max(1, Math.Abs(at.Y - _startY));
            }
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        e.Pointer.Capture(null);
        _dragging = _panning = false;
        _activeElement = _movingElement = null;
        _originalPoints = null;
        if (_changed) Engine.CompleteEdit();
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this);
        View.ZoomAround(Math.Pow(1.12, e.Delta.Y), p.X, p.Y);
        InvalidateVisual(); ViewChanged?.Invoke();
        e.Handled = true;
    }
}
