namespace Inklume.Core;

/// <summary>Bounds-only spatial hash. Maintained on completed edits, not on every pen sample.
/// Rendering and hit testing only enumerate candidate cells intersecting the viewport.</summary>
public sealed class SpatialHashIndex
{
    public const double CellSize = 256;
    private readonly Dictionary<(int, int), HashSet<string>> _cells = new();
    private readonly HashSet<string> _huge = new();
    private readonly Dictionary<string, WorldRect> _bounds = new();

    private static int Cell(double value) => (int)Math.Clamp(Math.Floor(value / CellSize), -1000000, 1000000);

    public void Rebuild(IEnumerable<BoardElement> elements)
    {
        _cells.Clear(); _huge.Clear(); _bounds.Clear();
        foreach (var element in elements)
        {
            var b = element.Bounds.Inflate(Math.Max(6, element.Thickness / 2));
            _bounds[element.Id] = b;
            var left = Cell(b.X); var right = Cell(b.Right);
            var top = Cell(b.Y); var bottom = Cell(b.Bottom);
            if ((long)(right - left + 1) * (bottom - top + 1) > 512)
            {
                _huge.Add(element.Id); continue;
            }
            for (var x = left; x <= right; x++)
            for (var y = top; y <= bottom; y++)
            {
                var key = (x, y);
                if (!_cells.TryGetValue(key, out var ids)) _cells[key] = ids = new HashSet<string>();
                ids.Add(element.Id);
            }
        }
    }

    public HashSet<string> Query(WorldRect world)
    {
        var result = new HashSet<string>(_huge);
        var left = Cell(world.X); var right = Cell(world.Right);
        var top = Cell(world.Y); var bottom = Cell(world.Bottom);
        // Extremely zoomed-out queries use the bounds table rather than billions of cells.
        if ((long)(right - left + 1) * (bottom - top + 1) > 4096)
        {
            foreach (var (id, bounds) in _bounds) if (bounds.Intersects(world)) result.Add(id);
            return result;
        }
        for (var x = left; x <= right; x++)
        for (var y = top; y <= bottom; y++)
            if (_cells.TryGetValue((x, y), out var ids)) result.UnionWith(ids);
        result.RemoveWhere(id => !_bounds.TryGetValue(id, out var b) || !b.Intersects(world));
        return result;
    }
}
