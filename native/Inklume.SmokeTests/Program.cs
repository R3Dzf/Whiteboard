using Inklume.Core;

var count = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAILED: " + label);
    Console.WriteLine("PASS " + label); count++;
}

var board = new BoardDocument { Name = "Test" };
var engine = new BoardEngine(board);
engine.BeginEdit();
var stroke = new BoardElement
{
    Kind = ElementKind.Stroke, Color = "#112233", Thickness = 3,
    Points = new() { new(10, 20, .2f), new(50, 20, .9f), new(90, 22, .5f) }
};
engine.Add(stroke); engine.CompleteEdit();
Check(engine.Visible(new WorldRect(0, 0, 100, 100)).Count() == 1, "spatial culling detects stroke");
Check(!engine.Visible(new WorldRect(5000, 0, 100, 100)).Any(), "spatial culling excludes offscreen element");
Check(engine.HitTest(50, 21, 4, 1)?.Id == stroke.Id, "stroke precise hit");
Check(engine.HitTest(50, 90, 4, 1) is null, "stroke bounding-box false positives avoided");
Check(engine.Undo() && engine.Document.Elements.Count == 0, "undo");
Check(engine.Redo() && engine.Document.Elements.Count == 1, "redo");
Check(engine.Document.Elements[0].Points[1].Pressure == .9f, "pressure persists through undo");
var v = new BoardViewport();
v.ZoomAround(2, 120, 70);
var pt = v.ToWorld(120, 70);
Check(Math.Abs(pt.X - 120) < .001 && Math.Abs(pt.Y - 70) < .001, "anchor-preserving zoom");
Check(Math.Abs(v.Zoom - 2) < .001, "zoom scaling");
var json = BoardCodec.Write(engine.Document);
Check(BoardCodec.Read(json).Elements[0].Kind == ElementKind.Stroke, "roundtrip native schema");
var legacy = """{"name":"Old notes","objects":[{"type":"stroke","x":10,"y":20,"w":90,"h":20,"color":"#000000","points":[[10,20,0.2],[100,30,0.8]]},{"type":"sticky","x":8,"y":10,"w":200,"h":150,"text":"Keep me"}]}""";
var migrated = BoardCodec.Read(legacy);
Check(migrated.Elements.Count == 2, "legacy Bosha JSON migration");
Check(migrated.Elements[0].Points[1].Pressure == .8f, "legacy pressure migration");
Check(migrated.Elements[1].Text == "Keep me", "legacy sticky migration");
var folder = Path.Combine(Path.GetTempPath(), "inklume-tests-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new BoardStore(folder);
    store.Save(migrated);
    Check(store.ListBoards().Count == 1, "atomic board save and load");
    migrated.Name = "Updated"; store.Save(migrated);
    File.WriteAllText(Directory.GetFiles(folder, "*.inklume.json").Single(), "broken");
    Check(store.ListBoards().Single().Name == "Old notes", "backup recovery from torn snapshot");
}
finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
// Spatial hash should examine only a few candidates in dense documents.
var dense = new BoardDocument();
for (var i = 0; i < 10000; i++) dense.Elements.Add(new BoardElement { Kind = ElementKind.Rectangle, X = i * 300, Y = 0, Width = 100, Height = 100 });
var many = new BoardEngine(dense);
Check(many.Visible(new WorldRect(149950, 0, 120, 120)).Count() < 3, "10k-object spatial query bounded");
// Native interaction and ink pipeline regression cases.
var sampled = new List<InkPoint>();
Check(InkPipeline.Append(sampled, new InkPoint(0, 0, .2f, 12f, -8f), 1), "initial stylus sample accepted");
Check(InkPipeline.Append(sampled, new InkPoint(120, 0, .9f, 20f, -5f), 1), "fast movement resampled");
Check(sampled.Count > 10 && sampled.Count < 75, "intermediate samples bounded");
Check(sampled[^1].X == 120 && sampled[^1].Pressure == .9f, "ink endpoint and pressure preserved");
Check(sampled[^1].TiltX == 20f, "stylus tilt preserved");
Check(!InkPipeline.Append(sampled, new InkPoint(120.1, 0), 1), "tiny pointer jitter filtered");
var eraserStroke = new BoardElement { Kind = ElementKind.Stroke, Thickness = 2,
    Points = Enumerable.Range(0, 21).Select(i => new InkPoint(i * 10, 0, .35f + i * .01f)).ToList() };
var pieces = InkPipeline.PartialErase(eraserStroke, 100, 0, 15);
Check(pieces != null && pieces.Count == 2, "eraser creates two independent fragments");
Check(pieces![0].Points.All(p => p.X < 85) && pieces[1].Points.All(p => p.X > 115), "erased gap never reconnects");
Check(pieces[0].Points[0].Pressure == eraserStroke.Points[0].Pressure, "eraser preserves surviving pressure");
Check(InkPipeline.PartialErase(eraserStroke, 100, 500, 10) is null, "eraser never changes distant strokes");
var shapes = new BoardDocument();
shapes.Elements.Add(new BoardElement { Kind = ElementKind.Ellipse, X = 0, Y = 0, Width = 100, Height = 100 });
shapes.Elements.Add(new BoardElement { Kind = ElementKind.Line, X = 200, Y = 0, Width = 100, Height = 100 });
var shapesEngine = new BoardEngine(shapes);
Check(shapesEngine.HitTest(5, 5, 2, 1) is null, "ellipse corner is not a selection hit");
Check(shapesEngine.HitTest(50, 50, 2, 1)?.Kind == ElementKind.Ellipse, "ellipse precise center hit");
Check(shapesEngine.HitTest(205, 95, 2, 1) is null, "line hit excludes bounding box false positive");
Check(shapesEngine.HitTest(250, 50, 2, 1)?.Kind == ElementKind.Line, "line hits on path");
var clone = BoardCodec.CloneElement(eraserStroke);
Check(clone.Points.Count == eraserStroke.Points.Count && !ReferenceEquals(clone.Points, eraserStroke.Points), "deep clone for independent object editing");
shapesEngine.BeginEdit();
Check(shapesEngine.RemoveMany(new[] { shapes.Elements[0].Id }) == 1, "multi-selection deletion");
shapesEngine.CompleteEdit();
Check(shapesEngine.Undo() && shapesEngine.Document.Elements.Count == 2, "deletion undo restores objects");
var fitted = new BoardViewport();
fitted.SetFit(new WorldRect(1000, 1000, 500, 400), 900, 650);
var fittedCenter = fitted.ToScreen(1250, 1200);
Check(Math.Abs(fittedCenter.X - 450) < .01 && Math.Abs(fittedCenter.Y - 325) < .01, "fit-to-content centers art");
Console.WriteLine($"SUCCESS: {count} tests");