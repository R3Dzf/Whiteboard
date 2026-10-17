using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Inklume.Core;

namespace Inklume.App;

public sealed class MainWindow : Window
{
    private readonly BoardStore _store;
    private readonly BoardCanvas _canvas;
    private readonly TextBlock _status = new() { Text = "Ready", Foreground = Brushes.DimGray, FontSize = 12 };
    private readonly TextBlock _zoom = new() { Text = "100%", FontSize = 12 };
    private readonly TextBlock _name = new() { Text = "Untitled board", FontSize = 16, FontWeight = FontWeight.SemiBold };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };

    public MainWindow()
    {
        Title = "Inklume — Native Preview";
        Width = 1440; Height = 920; MinWidth = 860; MinHeight = 560;
        Background = new SolidColorBrush(Color.Parse("#F6F7FC"));
        _store = new BoardStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Inklume", "Boards"));
        var boards = _store.ListBoards();
        var board = boards.FirstOrDefault() ?? new BoardDocument { Name = "My first board" };
        _canvas = new BoardCanvas(new BoardEngine(board));
        _canvas.Engine.Changed += ScheduleSave;
        _canvas.ViewChanged += () => _zoom.Text = $"{_canvas.View.Zoom * 100:0}%";
        _name.Text = board.Name;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };
        Closing += (_, _) => SaveNow();
        KeyDown += OnShortcut;
        Content = Compose();
    }

    private Control Compose()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("66,*,42") };
        var header = new DockPanel { Background = Brushes.White, LastChildFill = false, Margin = new Thickness(0, 0, 0, 1) };
        header.Children.Add(new TextBlock
        {
            Text = "✦  INKLUME", FontSize = 19, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#5548C9")),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(23, 0, 25, 0)
        });
        header.Children.Add(_name);
        _name.VerticalAlignment = VerticalAlignment.Center;
        _name.Margin = new Thickness(0, 0, 12, 0);
        var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0) };
        quick.Children.Add(ActionButton("New", () => Open(new BoardDocument { Name = "Untitled board" })));
        quick.Children.Add(ActionButton("Import", async () => await ImportAsync()));
        quick.Children.Add(ActionButton("Undo ↶", () => { if (_canvas.Engine.Undo()) _canvas.Redraw(); }));
        quick.Children.Add(ActionButton("Redo ↷", () => { if (_canvas.Engine.Redo()) _canvas.Redraw(); }));
        quick.Children.Add(ActionButton("Save", SaveNow));
        header.Children.Add(quick);
        root.Children.Add(header);

        var workspace = new Grid { ColumnDefinitions = new ColumnDefinitions("84,*,210"), Background = Brushes.White };
        Grid.SetRow(workspace, 1);
        var tools = new StackPanel { Spacing = 7, Margin = new Thickness(9, 18) };
        foreach (var (label, tool) in new[] {
            ("Select", CanvasTool.Select), ("Pen", CanvasTool.Pen), ("Marker", CanvasTool.Highlighter),
            ("Hand", CanvasTool.Pan), ("Eraser", CanvasTool.Eraser), ("Rect", CanvasTool.Rectangle),
            ("Ellipse", CanvasTool.Ellipse), ("Sticky", CanvasTool.Sticky), ("Text", CanvasTool.Text)
        }) tools.Children.Add(ActionButton(label, () => { _canvas.Tool = tool; _status.Text = $"Tool: {label}"; }));
        workspace.Children.Add(tools);
        Grid.SetColumn(_canvas, 1); workspace.Children.Add(_canvas);
        var inspector = new StackPanel { Spacing = 13, Margin = new Thickness(15, 22), Background = Brushes.White };
        inspector.Children.Add(new TextBlock { Text = "DRAWING", FontWeight = FontWeight.Bold, FontSize = 12, Foreground = Brushes.SlateGray });
        inspector.Children.Add(new TextBlock { Text = "Ink color", FontSize = 13 });
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var hex in new[] { "#243148", "#6153D8", "#098BCE", "#ED647D", "#138B74" })
        {
            var color = hex;
            var b = new Button { Content = "●", Foreground = new SolidColorBrush(Color.Parse(color)), FontSize = 24, Width = 32, Height = 38, Padding = new Thickness(1) };
            b.Click += (_, _) => _canvas.InkColor = color;
            colors.Children.Add(b);
        }
        inspector.Children.Add(colors);
        inspector.Children.Add(new TextBlock { Text = "Stroke width", FontSize = 13 });
        var slider = new Slider { Minimum = 1, Maximum = 20, Value = 3 };
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) _canvas.InkWidth = slider.Value; };
        inspector.Children.Add(slider);
        inspector.Children.Add(new TextBlock { Text = "Mouse wheel · zoom\nHand · pan\nCtrl+Z / Ctrl+Y · history\n\nNative renderer prototype", Foreground = Brushes.SlateGray, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(inspector, 2); workspace.Children.Add(inspector);
        root.Children.Add(workspace);

        var footer = new DockPanel { Background = Brushes.White, Margin = new Thickness(0), LastChildFill = true };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0) };
        right.Children.Add(ActionButton("−", () => ChangeZoom(1 / 1.15)));
        right.Children.Add(_zoom);
        right.Children.Add(ActionButton("+", () => ChangeZoom(1.15)));
        DockPanel.SetDock(right, Dock.Right); footer.Children.Add(right);
        _status.Margin = new Thickness(20, 0);
        _status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_status);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        return root;
    }

    private static Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = label, FontSize = 12, Padding = new Thickness(10, 8), HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => action();
        return button;
    }
    private void ChangeZoom(double factor)
    {
        _canvas.View.ZoomAround(factor, _canvas.Bounds.Width / 2, _canvas.Bounds.Height / 2);
        _canvas.Redraw(); _zoom.Text = $"{_canvas.View.Zoom * 100:0}%";
    }
    private void Open(BoardDocument document)
    {
        SaveNow();
        _canvas.ReplaceDocument(document);
        _canvas.Engine.Changed += ScheduleSave;
        _name.Text = document.Name;
        _status.Text = "Board opened";
    }
    private void ScheduleSave()
    {
        _status.Text = "Unsaved changes";
        _saveTimer.Stop(); _saveTimer.Start();
    }
    private void SaveNow()
    {
        _saveTimer.Stop();
        try { _store.Save(_canvas.Engine.Document); _status.Text = "Saved locally ✓"; }
        catch (Exception ex) { _status.Text = "Save failed: " + ex.Message; }
    }
    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Inklume or Bosha Whiteboard board", AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Whiteboard JSON") { Patterns = new[] { "*.json" } } }
        });
        if (files.Count == 0) return;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var imported = BoardCodec.Read(await reader.ReadToEndAsync());
            // An import must never overwrite the original board, even if GUIDs coincide.
            imported.Id = Guid.NewGuid().ToString("N");
            imported.Name += " (imported)";
            Open(imported);
            SaveNow();
        }
        catch (Exception ex) { _status.Text = "Import failed: " + ex.Message; }
    }
    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        switch (e.Key)
        {
            case Key.Z: if (_canvas.Engine.Undo()) _canvas.Redraw(); break;
            case Key.Y: if (_canvas.Engine.Redo()) _canvas.Redraw(); break;
            case Key.S: SaveNow(); break;
            case Key.O: _ = ImportAsync(); break;
            default: return;
        }
        e.Handled = true;
    }
}
