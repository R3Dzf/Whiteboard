using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Inklume.Core;

namespace Inklume.App;

public enum CanvasTool { Select, Pen, Highlighter, Eraser, Hand, Rectangle, Ellipse, Line, Arrow, Sticky, Text }

/// <summary>One native drawing surface with viewport culling. Gesture mutations touch only
/// the active object; the spatial index is rebuilt at transaction boundaries.</summary>
public sealed class BoardCanvas : Control
{
    private static readonly Dictionary<string, IBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Source, Bitmap Image)> _bitmaps = new();
    private readonly HashSet<string> _selected = new();
    private readonly Dictionary<(string Color, int WidthBucket), Pen> _segmentPens = new();
    private readonly Dictionary<string, BoardElement> _moveOrigins = new();
    private readonly List<BoardElement> _movingReferences = new();
    public BoardEngine Engine { get; private set; }
    public BoardViewport View { get; } = new();
    public CanvasTool Tool { get; set; } = CanvasTool.Pen;
    public string InkColor { get; set; } = "#283955";
    public double InkWidth { get; set; } = 3;
    public bool ShowGrid { get; set; } = true;
    public bool IsDark { get; set; }
    public IReadOnlyCollection<string> Selection => _selected;
    public event Action? ViewChanged;
    public event Action? SelectionChanged;
    private BoardElement? _activeElement;
    private Point _lastScreen;
    private Point _dragOrigin;
    private WorldRect? _marquee;
    private bool _dragging, _panning, _changed, _historyStarted, _moving;

    public BoardCanvas(BoardEngine engine)
    {
        Engine = engine; Focusable = true; ClipToBounds = true;
    }
    public void ReplaceDocument(BoardDocument board)
    {
        ClearBitmapCache(); Engine = new BoardEngine(board); _selected.Clear();
        View.Reset(); InvalidateVisual(); ViewChanged?.Invoke(); SelectionChanged?.Invoke();
    }
    public void Redraw() => InvalidateVisual();
    public void SetTool(CanvasTool tool) { Tool = tool; InvalidateVisual(); }
    public void FitToContent()
    {
        var elements = Engine.Document.Elements;
        if (elements.Count == 0) { View.Reset(); InvalidateVisual(); ViewChanged?.Invoke(); return; }
        var minX = elements.Min(e => e.Bounds.X); var minY = elements.Min(e => e.Bounds.Y);
        var maxX = elements.Max(e => e.Bounds.Right); var maxY = elements.Max(e => e.Bounds.Bottom);
        var width = Math.Max(1, maxX - minX); var height = Math.Max(1, maxY - minY);
        View.SetFit(new WorldRect(minX, minY, width, height), Bounds.Width, Bounds.Height);
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void SelectAll() { _selected.Clear(); foreach (var e in Engine.Document.Elements) _selected.Add(e.Id); SelectionChanged?.Invoke(); InvalidateVisual(); }
    public bool DeleteSelection()
    {
        if (_selected.Count == 0) return false;
        Engine.BeginEdit(); var count = Engine.RemoveMany(_selected);
        _selected.Clear(); Engine.CompleteEdit(); SelectionChanged?.Invoke(); InvalidateVisual();
        return count > 0;
    }
    public void UpdateSelectedText(string text)
    {
        var targets = Engine.Document.Elements.Where(e => _selected.Contains(e.Id) && e.Kind is ElementKind.Sticky or ElementKind.Text).ToList();
        if (targets.Count == 0 || targets.All(e => e.Text == text)) return;
        Engine.BeginEdit(); foreach (var e in targets) e.Text = text[..Math.Min(text.Length, 10_000)];
        Engine.CompleteEdit(); InvalidateVisual();
    }
    public void DuplicateSelection()
    {
        if (_selected.Count == 0) return;
        var copies = Engine.Document.Elements.Where(e => _selected.Contains(e.Id)).Select(e => BoardCodec.CloneElement(e)).ToList();
        if (copies.Count == 0) return;
        Engine.BeginEdit(); _selected.Clear();
        foreach (var item in copies)
        {
            item.Id = Guid.NewGuid().ToString("N"); item.X += 30; item.Y += 30;
            foreach (var p in item.Points) { p.X += 30; p.Y += 30; }
            Engine.Add(item); _selected.Add(item.Id);
        }
        Engine.CompleteEdit(); SelectionChanged?.Invoke(); InvalidateVisual();
    }
    private static Point ToScreen(BoardViewport v, double x, double y) { var p = v.ToScreen(x, y); return new(p.X, p.Y); }
    private Point Screen(InkPoint p) => ToScreen(View, p.X, p.Y);
    private Point Screen(double x, double y) => ToScreen(View, x, y);
    private Rect ScreenRect(WorldRect b)
    {
        var p = Screen(b.X, b.Y);
        return new Rect(p.X, p.Y, Math.Max(.01, b.Width * View.Zoom), Math.Max(.01, b.Height * View.Zoom));
    }
    private Pen SegmentPen(string color, double width)
    {
        var bucket = Math.Clamp((int)Math.Round(width * 4), 2, 1024);
        var key = (color, bucket);
        if (_segmentPens.TryGetValue(key, out var cached)) return cached;
        var pen = new Pen(Brush(color, Brushes.DarkSlateBlue), bucket / 4d) { LineCap = PenLineCap.Round };
        if (_segmentPens.Count > 2048) _segmentPens.Clear();
        _segmentPens[key] = pen;
        return pen;
    }
    private static IBrush Brush(string value, IBrush fallback)
    {
        if (BrushCache.TryGetValue(value, out var existing)) return existing;
        try { var color = new SolidColorBrush(Color.Parse(value)); if (BrushCache.Count < 256) BrushCache[value] = color; return color; }
        catch { return fallback; }
    }
    private void ClearBitmapCache() { foreach (var item in _bitmaps.Values) item.Image.Dispose(); _bitmaps.Clear(); }
    private Bitmap? ResolveBitmap(BoardElement e)
    {
        var value = e.EmbeddedImage;
        if (string.IsNullOrEmpty(value) || value.Length > 12_000_000 || !value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return null;
        if (_bitmaps.TryGetValue(e.Id, out var cached)) return cached.Source == value ? cached.Image : ReplaceBitmap();
        return ReplaceBitmap();
        Bitmap? ReplaceBitmap()
        {
            if (_bitmaps.Remove(e.Id, out var old)) old.Image.Dispose();
            try
            {
                var comma = value.IndexOf(',');
                if (comma < 0 || !value[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
                var raw = Convert.FromBase64String(value[(comma + 1)..]);
                using var stream = new MemoryStream(raw);
                var bitmap = new Bitmap(stream);
                if (bitmap.PixelSize.Width > 8192 || bitmap.PixelSize.Height > 8192) { bitmap.Dispose(); return null; }
                _bitmaps[e.Id] = (value, bitmap); return bitmap;
            }
            catch { return null; }
        }
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(IsDark ? Brush("#1A2233", Brushes.Black) : Brush("#FAFBFE", Brushes.White), new Rect(Bounds.Size));
        var zoom = View.Zoom; var spacing = 24 * zoom;
        // Cap the background-dot cost independently of zoom and viewport size.
        if (ShowGrid && spacing >= 19 && Bounds.Width * Bounds.Height / (spacing * spacing) < 9500)
        {
            var dots = Brush(IsDark ? "#394457" : "#D9DEEC", Brushes.LightGray);
            var x = ((View.OffsetX % spacing) + spacing) % spacing;
            var y = ((View.OffsetY % spacing) + spacing) % spacing;
            for (var px = x; px < Bounds.Width; px += spacing)
            for (var py = y; py < Bounds.Height; py += spacing) context.DrawEllipse(dots, null, new Point(px, py), 1, 1);
        }
        var visible = View.Visible(Bounds.Width, Bounds.Height).Inflate(60 / zoom);
        var candidates = Engine.Visible(visible).ToList();
        // During idle drawing, never scan the whole document just to find live objects.
        // Changed objects are held by reference until the index is refreshed at pointer-up.
        if (_activeElement != null && candidates.All(c => c.Id != _activeElement.Id)) candidates.Add(_activeElement);
        foreach (var obj in _movingReferences)
            if (candidates.All(c => c.Id != obj.Id)) candidates.Add(obj);
        foreach (var obj in candidates)
            if (obj.Bounds.Intersects(visible) || _moveOrigins.ContainsKey(obj.Id) || ReferenceEquals(obj, _activeElement)) DrawElement(context, obj);
        var outline = new Pen(Brush("#7660E8", Brushes.Blue), 1.5) { DashStyle = DashStyle.Dash };
        if (_selected.Count > 0)
            foreach (var obj in Engine.Document.Elements.Where(o => _selected.Contains(o.Id)))
                context.DrawRectangle(null, outline, ScreenRect(obj.Bounds.Inflate(5 / zoom)));
        if (_marquee is { } m)
            context.DrawRectangle(Brush("#E8E6FF", Brushes.LightBlue), outline, ScreenRect(m));
    }
    private void DrawElement(DrawingContext ctx, BoardElement e)
    {
        var stroke = Brush(e.Color, Brushes.DarkSlateBlue); var fill = Brush(e.Fill, Brushes.LightYellow);
        var thickness = Math.Max(.7, e.Thickness * View.Zoom);
        var pen = new Pen(stroke, thickness) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var r = ScreenRect(e.Bounds);
        switch (e.Kind)
        {
            case ElementKind.Stroke:
            {
                var pts = e.Points;
                for (var i = 1; i < pts.Count; i++)
                {
                    var sampleFrom = pts[i-1]; var sampleTo = pts[i];
                    var pressure = Math.Clamp((sampleFrom.Pressure + sampleTo.Pressure) / 2, .1f, 1f);
                    var width = Math.Max(.65, thickness * (.45 + pressure * .9));
                    ctx.DrawLine(SegmentPen(e.Color, width), Screen(sampleFrom), Screen(sampleTo));
                }
                if (pts.Count == 1) ctx.DrawEllipse(stroke, null, Screen(pts[0]), thickness / 2, thickness / 2);
                break;
            }
            case ElementKind.Rectangle: ctx.DrawRectangle(fill, pen, r, 6 * View.Zoom); break;
            case ElementKind.Ellipse: ctx.DrawEllipse(fill, pen, r.Center, r.Width / 2, r.Height / 2); break;
            case ElementKind.Line: case ElementKind.Arrow:
                var a = Screen(e.X, e.Y); var b = Screen(e.X + e.Width, e.Y + e.Height);
                ctx.DrawLine(pen, a, b);
                if (e.Kind == ElementKind.Arrow)
                {
                    var angle = Math.Atan2(b.Y-a.Y,b.X-a.X); var length = Math.Clamp(thickness * 3+8,10,26);
                    ctx.DrawLine(pen,b,new Point(b.X-length*Math.Cos(angle-.5),b.Y-length*Math.Sin(angle-.5)));
                    ctx.DrawLine(pen,b,new Point(b.X-length*Math.Cos(angle+.5),b.Y-length*Math.Sin(angle+.5)));
                }
                break;
            case ElementKind.Sticky:
                ctx.DrawRectangle(fill, null, r, 10 * View.Zoom);
                DrawText(ctx, e.Text, r.X + 15 * View.Zoom, r.Y + 15 * View.Zoom, 17 * View.Zoom, Brush("#293248", Brushes.Black));
                break;
            case ElementKind.Text: DrawText(ctx, e.Text, r.X, r.Y, 22 * View.Zoom, stroke); break;
            case ElementKind.Image:
                var img = ResolveBitmap(e);
                if (img != null) ctx.DrawImage(img, r);
                else { ctx.DrawRectangle(Brush("#F5F6FC", Brushes.White), new Pen(Brush("#BDC5D8", Brushes.Gray), 1), r); DrawText(ctx, "Image preview unavailable", r.X+12, r.Y+12, 12, Brushes.Gray); }
                break;
        }
    }
    private static void DrawText(DrawingContext ctx, string text, double x, double y, double size, IBrush foreground)
    {
        var lineHeight = Math.Clamp(size,8,84) * 1.3;
        foreach (var (line, index) in (text ?? "").Split('\n').Take(12).Select((line, index) => (line, index)))
        {
            var formatted = new FormattedText(line, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), Math.Clamp(size,8,84), foreground);
            ctx.DrawText(formatted, new Point(x, y + index * lineHeight));
        }
    }
    private void StartHistory() { if (_historyStarted) return; Engine.BeginEdit(); _historyStarted = true; }
    private InkPoint Sample(PointerEventArgs e, (double X, double Y) at)
    {
        var props = e.GetCurrentPoint(this).Properties;
        return new InkPoint(at.X,at.Y,e.Pointer.Type == PointerType.Pen ? Math.Clamp(props.Pressure,.05f,1f) : .5f,props.XTilt,props.YTilt);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var pointer = e.GetCurrentPoint(this);
        if (!pointer.Properties.IsLeftButtonPressed && !pointer.Properties.IsMiddleButtonPressed) return;
        _dragging=true;_changed=false;_historyStarted=false;_moving=false;
        _lastScreen=pointer.Position;_dragOrigin=pointer.Position;
        _panning=Tool==CanvasTool.Hand || pointer.Properties.IsMiddleButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        e.Pointer.Capture(this);
        var at=View.ToWorld(_lastScreen.X,_lastScreen.Y);
        if (_panning) return;
        if (Tool == CanvasTool.Select)
        {
            var hit=Engine.HitTest(at.X,at.Y,7,View.Zoom);
            if (hit != null)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { if (!_selected.Add(hit.Id)) _selected.Remove(hit.Id); }
                else if (!_selected.Contains(hit.Id)) { _selected.Clear(); _selected.Add(hit.Id); }
                _moving = _selected.Contains(hit.Id);
                _moveOrigins.Clear();_movingReferences.Clear();
                if (_moving) foreach (var obj in Engine.Document.Elements.Where(o=>_selected.Contains(o.Id)))
                { _moveOrigins[obj.Id]=BoardCodec.CloneElement(obj);_movingReferences.Add(obj); }
            }
            else
            {
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _selected.Clear();
                _marquee = new WorldRect(at.X,at.Y,0,0);
            }
            SelectionChanged?.Invoke();InvalidateVisual();return;
        }
        if (Tool == CanvasTool.Eraser) { EraseAt(at); return; }
        StartHistory();
        _activeElement=new BoardElement
        {
            Kind=Tool switch { CanvasTool.Rectangle=>ElementKind.Rectangle,CanvasTool.Ellipse=>ElementKind.Ellipse,
                CanvasTool.Line=>ElementKind.Line,CanvasTool.Arrow=>ElementKind.Arrow,
                CanvasTool.Sticky=>ElementKind.Sticky,CanvasTool.Text=>ElementKind.Text,_=>ElementKind.Stroke },
            X=at.X,Y=at.Y,Width=1,Height=1,
            Thickness=Tool==CanvasTool.Highlighter?InkWidth*3:InkWidth,
            Color=Tool==CanvasTool.Highlighter?"#91A4E8":InkColor,
            Fill=Tool==CanvasTool.Sticky?"#FFF0B7":"#FFFFFF",
            Text=Tool==CanvasTool.Sticky?"New sticky note":""
        };
        if (_activeElement.Kind==ElementKind.Stroke) InkPipeline.Append(_activeElement.Points,Sample(e,at),View.Zoom);
        if (_activeElement.Kind is ElementKind.Sticky or ElementKind.Text)
        { _activeElement.Width=220;_activeElement.Height=175; }
        Engine.Add(_activeElement);
        if (_activeElement.Kind is ElementKind.Text or ElementKind.Sticky)
        {
            _selected.Clear(); _selected.Add(_activeElement.Id); SelectionChanged?.Invoke();
        }
        _changed=true;InvalidateVisual();
    }
    private void EraseAt((double X,double Y) at)
    {
        var radius=11 / View.Zoom;
        // Avoid a history entry for an eraser click that misses the ink entirely.
        if (!_historyStarted && Engine.HitTest(at.X,at.Y,12,View.Zoom)?.Kind != ElementKind.Stroke) return;
        StartHistory();
        if (Engine.EraseInk(at.X,at.Y,radius)>0) { _changed=true; InvalidateVisual(); }
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (!_dragging)return;
        var screen=e.GetPosition(this);var at=View.ToWorld(screen.X,screen.Y);
        if (_panning) { View.Pan(screen.X-_lastScreen.X,screen.Y-_lastScreen.Y);_lastScreen=screen;InvalidateVisual();ViewChanged?.Invoke();return; }
        if (Tool==CanvasTool.Eraser) { EraseAt(at);return; }
        if (Tool==CanvasTool.Select)
        {
            if (_marquee != null)
            {
                var start=View.ToWorld(_dragOrigin.X,_dragOrigin.Y);
                _marquee=new WorldRect(Math.Min(start.X,at.X),Math.Min(start.Y,at.Y),Math.Abs(at.X-start.X),Math.Abs(at.Y-start.Y));
            }
            else if (_moving && _moveOrigins.Count>0)
            {
                var sx=(screen.X-_dragOrigin.X)/View.Zoom;var sy=(screen.Y-_dragOrigin.Y)/View.Zoom;
                if (Math.Abs(sx)+Math.Abs(sy)>.7 && !_changed) { StartHistory();_changed=true; }
                if (_changed)
                foreach (var moving in _movingReferences)
                {
                    var orig=_moveOrigins[moving.Id];moving.X=orig.X+sx;moving.Y=orig.Y+sy;
                    if (orig.Kind==ElementKind.Stroke)
                        moving.Points=orig.Points.Select(p=>new InkPoint(p.X+sx,p.Y+sy,p.Pressure,p.TiltX,p.TiltY)).ToList();
                }
            }
            InvalidateVisual();return;
        }
        if (_activeElement is { } obj)
        {
            if (obj.Kind==ElementKind.Stroke) InkPipeline.Append(obj.Points,Sample(e,at),View.Zoom);
            else if (obj.Kind is ElementKind.Rectangle or ElementKind.Ellipse)
            {
                var initial=View.ToWorld(_dragOrigin.X,_dragOrigin.Y);
                obj.X=Math.Min(initial.X,at.X);obj.Y=Math.Min(initial.Y,at.Y);
                obj.Width=Math.Max(1,Math.Abs(at.X-initial.X));obj.Height=Math.Max(1,Math.Abs(at.Y-initial.Y));
            }
            else if (obj.Kind is ElementKind.Line or ElementKind.Arrow)
            {
                var initial=View.ToWorld(_dragOrigin.X,_dragOrigin.Y);
                obj.X=initial.X;obj.Y=initial.Y;obj.Width=at.X-initial.X;obj.Height=at.Y-initial.Y;
            }
            InvalidateVisual();
        }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);if(!_dragging)return;e.Pointer.Capture(null);
        if (_marquee is { } m && m.Width>4/View.Zoom && m.Height>4/View.Zoom)
        {
            foreach (var element in Engine.Document.Elements)
                if (element.Bounds.Intersects(m)) _selected.Add(element.Id);
            SelectionChanged?.Invoke();
        }
        _marquee=null;_moveOrigins.Clear();_movingReferences.Clear();_dragging=_panning=_moving=false;_activeElement=null;
        if (_changed) Engine.CompleteEdit();
        _changed=false;_historyStarted=false;InvalidateVisual();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);var p=e.GetPosition(this);
        View.ZoomAround(Math.Pow(1.12,e.Delta.Y),p.X,p.Y);InvalidateVisual();ViewChanged?.Invoke();e.Handled=true;
    }
}