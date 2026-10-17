using System.Text.Json;

namespace Inklume.Core;

public sealed class BoardEngine
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly SpatialHashIndex _index = new();
    private readonly Dictionary<string, (int Order, BoardElement Element)> _lookup = new();
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    public BoardDocument Document { get; private set; }
    public event Action? Changed;

    public BoardEngine(BoardDocument? document = null)
    {
        Document = document ?? new BoardDocument();
        Reindex();
    }

    public void BeginEdit()
    {
        _undo.Push(JsonSerializer.Serialize(Document, Json));
        if (_undo.Count > 40)
        {
            var limited = _undo.Take(40).Reverse().ToArray();
            _undo.Clear(); foreach (var item in limited) _undo.Push(item);
        }
        _redo.Clear();
    }

    private void Reindex()
    {
        _index.Rebuild(Document.Elements);
        _lookup.Clear();
        for (var i = 0; i < Document.Elements.Count; i++)
            _lookup[Document.Elements[i].Id] = (i, Document.Elements[i]);
    }

    public void CompleteEdit()
    {
        Reindex();
        Document.UpdatedUtc = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    public void Add(BoardElement element)
    {
        Document.Elements.Add(element);
    }

    public bool Remove(string id) => Document.Elements.RemoveAll(e => e.Id == id) > 0;

    public int RemoveMany(IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        return Document.Elements.RemoveAll(e => selected.Contains(e.Id));
    }

    public int EraseInk(double x, double y, double radius)
    {
        // The index is stable for the lifetime of an edit; erasing repeatedly must
        // inspect live objects as well as indexed cells since split fragments are new.
        var changed = 0;
        var revised = new List<BoardElement>(Document.Elements.Count);
        foreach (var element in Document.Elements)
        {
            var result = InkPipeline.PartialErase(element, x, y, radius);
            if (result is null) revised.Add(element);
            else { changed++; revised.AddRange(result); }
        }
        if (changed > 0) { Document.Elements.Clear(); Document.Elements.AddRange(revised); }
        return changed;
    }

    public IEnumerable<BoardElement> Visible(WorldRect world)
    {
        var ids = _index.Query(world);
        // Sort only spatial candidates; do not scan the whole document during every frame.
        return ids.Where(id => _lookup.ContainsKey(id))
            .OrderBy(id => _lookup[id].Order).Select(id => _lookup[id].Element);
    }

    public BoardElement? HitTest(double x, double y, double screenRadius, double zoom)
    {
        var distance = screenRadius / Math.Max(.1, zoom);
        var ids = _index.Query(new WorldRect(x - distance, y - distance, distance * 2, distance * 2));
        foreach (var id in ids.Where(id => _lookup.ContainsKey(id))
                     .OrderByDescending(id => _lookup[id].Order))
        {
            var element = _lookup[id].Element;
            if (element.Kind == ElementKind.Stroke)
            {
                if (StrokeHit(element.Points, x, y, distance + element.Thickness / 2)) return element;
            }
            else if (PreciseHit(element, x, y, distance)) return element;
        }
        return null;
    }

    private static bool PreciseHit(BoardElement element, double x, double y, double padding)
    {
        var r = element.Bounds;
        if (!r.Inflate(padding).Contains(x, y)) return false;
        if (element.Kind == ElementKind.Ellipse)
        {
            var rx = Math.Max(1, r.Width / 2 + padding), ry = Math.Max(1, r.Height / 2 + padding);
            var dx = (x - (r.X + r.Width / 2)) / rx;
            var dy = (y - (r.Y + r.Height / 2)) / ry;
            return dx * dx + dy * dy <= 1;
        }
        if (element.Kind is ElementKind.Line or ElementKind.Arrow)
            return StrokeHit(new[] { new InkPoint(element.X, element.Y),
                new InkPoint(element.X + element.Width, element.Y + element.Height) },
                x, y, padding + element.Thickness / 2);
        return true;
    }

    public static bool StrokeHit(IReadOnlyList<InkPoint> points, double x, double y, double radius)
    {
        if (points.Count == 0) return false;
        if (points.Count == 1) return double.Hypot(points[0].X - x, points[0].Y - y) <= radius;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i];
            var vx = b.X - a.X; var vy = b.Y - a.Y;
            var denom = vx * vx + vy * vy;
            var t = denom == 0 ? 0 : Math.Clamp(((x - a.X) * vx + (y - a.Y) * vy) / denom, 0, 1);
            if (double.Hypot(x - (a.X + vx * t), y - (a.Y + vy * t)) <= radius) return true;
        }
        return false;
    }

    public bool Undo() => Restore(_undo, _redo);
    public bool Redo() => Restore(_redo, _undo);

    private bool Restore(Stack<string> source, Stack<string> destination)
    {
        if (source.Count == 0) return false;
        destination.Push(JsonSerializer.Serialize(Document, Json));
        Document = JsonSerializer.Deserialize<BoardDocument>(source.Pop(), Json)
            ?? throw new InvalidDataException("Invalid history snapshot");
        CompleteEdit();
        return true;
    }
}