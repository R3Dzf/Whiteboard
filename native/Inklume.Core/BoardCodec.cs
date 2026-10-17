using System.Text.Json;

namespace Inklume.Core;

public static class BoardCodec
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private const int MaxElements = 15000;
    private const int MaxSamples = 200000;
    public static string Write(BoardDocument board) => JsonSerializer.Serialize(board, Options);

    public static BoardDocument Read(string input)
    {
        if (input.Length > 80_000_000) throw new InvalidDataException("Board too large");
        using var parsed = JsonDocument.Parse(input);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Not a board");
        if (root.TryGetProperty("objects", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
            return ImportLegacy(root, legacy);
        if (!root.TryGetProperty("format", out var format) || format.GetString() != "inklume.board.v1")
            throw new InvalidDataException("Unsupported board format");
        var board = JsonSerializer.Deserialize<BoardDocument>(input, Options) ?? throw new InvalidDataException("Invalid board");
        Validate(board);
        return board;
    }

    public static void Validate(BoardDocument board)
    {
        if (board.Elements.Count > MaxElements) throw new InvalidDataException("Too many elements");
        if (!Guid.TryParse(board.Id, out _)) throw new InvalidDataException("Invalid board identifier");
        foreach (var element in board.Elements)
        {
            if (!Finite(element.X, element.Y, element.Width, element.Height, element.Thickness))
                throw new InvalidDataException("Invalid object coordinates");
            if (element.Points.Count > MaxSamples || element.Points.Any(p => !Finite(p.X, p.Y)))
                throw new InvalidDataException("Invalid ink data");
            if (element.EmbeddedImage?.Length > 12_000_000) throw new InvalidDataException("Image too large");
        }
    }

    private static bool Finite(params double[] values) => values.All(v => double.IsFinite(v) && Math.Abs(v) <= 10_000_000);
    private static double Num(JsonElement node, string property, double fallback = 0)
        => node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : fallback;
    private static string Str(JsonElement node, string property, string fallback = "")
        => node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    public static BoardDocument ImportLegacy(JsonElement root, JsonElement items)
    {
        if (items.GetArrayLength() > MaxElements) throw new InvalidDataException("Too many elements");
        var board = new BoardDocument { Name = Str(root, "name", "Migrated board"), Background = Str(root, "background", "dots") };
        foreach (var item in items.EnumerateArray())
        {
            var kind = Str(item, "type") switch
            {
                "stroke" => ElementKind.Stroke, "rect" => ElementKind.Rectangle,
                "ellipse" => ElementKind.Ellipse, "line" => ElementKind.Line,
                "arrow" or "doubleArrow" => ElementKind.Arrow,
                "sticky" => ElementKind.Sticky, "text" => ElementKind.Text,
                "image" => ElementKind.Image, _ => (ElementKind?)null
            };
            if (kind is null) continue;
            var e = new BoardElement
            {
                Kind = kind.Value, X = Num(item, "x"), Y = Num(item, "y"),
                Width = Num(item, "w", 150), Height = Num(item, "h", 100),
                Color = Str(item, "color", "#243148"), Fill = Str(item, "fill", "#FFEFAD"),
                Thickness = Num(item, "thickness", 3), Text = Str(item, "text"),
                EmbeddedImage = kind == ElementKind.Image ? Str(item, "src") : null
            };
            if (kind == ElementKind.Stroke && item.TryGetProperty("points", out var samples) && samples.ValueKind == JsonValueKind.Array)
            {
                if (samples.GetArrayLength() > MaxSamples) throw new InvalidDataException("Too many samples");
                foreach (var pt in samples.EnumerateArray())
                    if (pt.ValueKind == JsonValueKind.Array && pt.GetArrayLength() >= 2)
                    {
                        var vals = pt.EnumerateArray().ToArray();
                        if (vals[0].TryGetDouble(out var x) && vals[1].TryGetDouble(out var y))
                            e.Points.Add(new InkPoint(x, y, vals.Length > 2 && vals[2].TryGetSingle(out var p) ? Math.Clamp(p, 0, 1) : .5f));
                    }
            }
            board.Elements.Add(e);
        }
        Validate(board);
        return board;
    }
}
