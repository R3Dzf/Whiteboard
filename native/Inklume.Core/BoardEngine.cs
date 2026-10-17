using System.Text.Json;

namespace Inklume.Core;

public sealed class BoardEngine
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly SpatialHashIndex _index = new();
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    public BoardDocument Document { get; private set; }
    public event Action? Changed;

    public BoardEngine(BoardDocument? document = null)
    {
        Document = document ?? new BoardDocument();
        _index.Rebuild(Document.Elements);
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

    public void CompleteEdit()
    {
        _index.Rebuild(Document.Elements);
        Document.UpdatedUtc = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    public void Add(BoardElement element)
    {
        Document.Elements.Add(element);
    }

    public bool Remove(string id) => Document.Elements.RemoveAll(e => e.Id == id) > 0;

    public IEnumerable<BoardElement> Visible(WorldRect world)
    {
        var ids = _index.Query(world);
        // Document order is authoritative for z-index.
        return Document.Elements.Where(e => ids.Contains(e.Id));
    }

    public BoardElement? HitTest(double x, double y, double screenRadius, double zoom)
    {
        var distance = screenRadius / Math.Max(.1, zoom);
        var ids = _index.Query(new WorldRect(x - distance, y - distance, distance * 2, distance * 2));
        for (var i = Document.Elements.Count - 1; i >= 0; i--)
        {
            var element = Document.Elements[i];
            if (!ids.Contains(element.Id)) continue;
            if (element.Kind == ElementKind.Stroke)
            {
                if (StrokeHit(element.Points, x, y, distance + element.Thickness / 2)) return element;
            }
            else if (element.Bounds.Inflate(distance).Contains(x, y)) return element;
        }
        return null;
    }

    public static bool StrokeHit(IReadOnlyList<InkPoint> points, double x, double y, double radius)
    {
        if (points.Count == 0) return false;
        if (points.Count == 1) return Math.Hypot(points[0].X - x, points[0].Y - y) <= radius;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i];
            var vx = b.X - a.X; var vy = b.Y - a.Y;
            var denom = vx * vx + vy * vy;
            var t = denom == 0 ? 0 : Math.Clamp(((x - a.X) * vx + (y - a.Y) * vy) / denom, 0, 1);
            if (Math.Hypot(x - (a.X + vx * t), y - (a.Y + vy * t)) <= radius) return true;
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
