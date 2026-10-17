using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Inklume.Core;

namespace Inklume.App;

/// <summary>Native desktop shell. No embedded browser; tools and board library use Avalonia controls.</summary>
public sealed class MainWindow : Window
{
    private static readonly IBrush Accent = B("#6458DC");
    private static readonly IBrush SoftAccent = B("#ECEAFE");
    private static readonly IBrush Ink = B("#20263A");
    private static readonly IBrush Muted = B("#78839B");
    private static readonly IBrush Outline = B("#E5E9F2");
    private static readonly IBrush Surface = Brushes.White;
    private readonly BoardStore _store;
    private readonly BoardCanvas _canvas;
    private readonly TextBlock _status = new() { Text = "Ready to create", Foreground = Muted, FontSize = 12 };
    private readonly TextBlock _zoom = new() { Text = "100%", FontWeight = FontWeight.SemiBold, FontSize = 12 };
    private readonly TextBox _title = new() { FontSize = 16, FontWeight = FontWeight.SemiBold, Background = Brushes.Transparent,
        BorderThickness = new Thickness(0), Foreground = Ink, MinWidth = 130, MaxWidth = 280 };
    private readonly StackPanel _libraryItems = new() { Spacing = 7 };
    private readonly StackPanel _toolItems = new() { Spacing = 6 };
    private readonly TextBlock _selectionSummary = new() { FontSize = 12, Foreground = Muted };
    private readonly TextBox _textEditor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110,
        MaxHeight = 230, PlaceholderText = "Select a text or sticky-note element" };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private readonly ColumnDefinition _libraryColumn = new() { Width = new GridLength(230) };
    private readonly ColumnDefinition _inspectorColumn = new() { Width = new GridLength(244) };
    private readonly Dictionary<CanvasTool, Button> _toolButtons = new();

    public MainWindow()
    {
        Title = "Inklume — a canvas for every idea";
        Width = 1510; Height = 940; MinWidth = 950; MinHeight = 650;
        Background = B("#F5F7FC");
        _store = new BoardStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Inklume", "Boards"));
        var initial = _store.ListBoards().FirstOrDefault() ?? new BoardDocument { Name = "My first board" };
        _canvas = new BoardCanvas(new BoardEngine(initial));
        HookEngine();
        _title.Text = initial.Name;
        _canvas.ViewChanged += () => _zoom.Text = $"{_canvas.View.Zoom * 100:0}%";
        _canvas.SelectionChanged += SyncSelection;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(false); };
        Closing += (_, _) => SaveNow(false);
        KeyDown += OnShortcut;
        _title.KeyDown += (_, e) => { if (e.Key == Key.Enter) { UpdateTitle(); _canvas.Focus(); e.Handled = true; } };
        _title.LostFocus += (_, _) => UpdateTitle();
        Content = Compose();
        RefreshLibrary(); SyncTools(); SyncSelection();
    }
    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static Border Panel(Control child, string bg = "#FFFFFF", double radius = 15) => new()
    {
        Background = B(bg), CornerRadius = new CornerRadius(radius), Child = child
    };
    private static TextBlock Label(string text, int size = 12, IBrush? color = null, bool strong = false) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Ink,
        FontWeight = strong ? FontWeight.SemiBold : FontWeight.Normal,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    private static Button ActionButton(string text, Action action, bool primary = false)
    {
        var b = new Button
        {
            Content = text, FontSize = 12, FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(13,9),
            Background = primary ? Accent : B("#F3F5FA"),
            Foreground = primary ? Brushes.White : Ink,
            BorderBrush = primary ? Accent : Outline,
            BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Center
        };
        b.Click += (_, _) => action(); return b;
    }
    private static Border Divider() => new() { Background = Outline, Height = 1, Margin = new Thickness(0,12) };
    private Control Compose()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("70,*,44") };
        root.Children.Add(BuildHeader());
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,72,*,Auto"), Background = B("#F5F7FC") };
        body.ColumnDefinitions[0] = _libraryColumn; body.ColumnDefinitions[3] = _inspectorColumn;
        Grid.SetRow(body,1);root.Children.Add(body);
        var library = BuildLibrary(); Grid.SetColumn(library,0); body.Children.Add(library);
        var tools = BuildToolRail(); Grid.SetColumn(tools,1);body.Children.Add(tools);
        var canvasWrap = new Border { Background = Brushes.White, Margin = new Thickness(10,12,10,12),
            CornerRadius = new CornerRadius(13), ClipToBounds = true, BorderBrush = Outline, BorderThickness = new Thickness(1), Child = _canvas };
        Grid.SetColumn(canvasWrap,2); body.Children.Add(canvasWrap);
        var inspector = BuildInspector(); Grid.SetColumn(inspector,3);body.Children.Add(inspector);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Background = Brushes.White };
        _status.VerticalAlignment=VerticalAlignment.Center;_status.Margin=new Thickness(20,0);
        footer.Children.Add(_status);
        var zoomBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,22,0) };
        zoomBar.Children.Add(ActionButton("−",()=>Zoom(1/1.15)));
        _zoom.VerticalAlignment=VerticalAlignment.Center;_zoom.MinWidth=45;_zoom.TextAlignment=TextAlignment.Center;
        zoomBar.Children.Add(_zoom);
        zoomBar.Children.Add(ActionButton("+",()=>Zoom(1.15)));
        zoomBar.Children.Add(ActionButton("Fit",()=>_canvas.FitToContent()));
        Grid.SetColumn(zoomBar,1);footer.Children.Add(zoomBar);
        Grid.SetRow(footer,2);root.Children.Add(footer);
        return root;
    }
    private Control BuildHeader()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Background = Brushes.White };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(21,0,28,0), VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border { Background = Accent, Width = 37, Height = 37, CornerRadius = new CornerRadius(12),
            Child = new TextBlock { Text = "✦", Foreground = Brushes.White, FontSize = 25, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        brand.Children.Add(Label("Inklume",21,Ink,true));header.Children.Add(brand);
        _title.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(_title,1);header.Children.Add(_title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8,0,21,0) };
        actions.Children.Add(ActionButton("Boards",()=>_libraryColumn.Width = _libraryColumn.Width.Value > 0 ? new GridLength(0) : new GridLength(230)));
        actions.Children.Add(ActionButton("↶ Undo",()=>{if(_canvas.Engine.Undo())_canvas.Redraw();}));
        actions.Children.Add(ActionButton("↷ Redo",()=>{if(_canvas.Engine.Redo())_canvas.Redraw();}));
        actions.Children.Add(ActionButton("Import",()=>_ = ImportAsync()));
        actions.Children.Add(ActionButton("Export",()=>_ = ExportAsync()));
        actions.Children.Add(ActionButton("＋ Image",()=>_ = ImportImageAsync()));
        actions.Children.Add(ActionButton("Save",()=>SaveNow(true),true));
        Grid.SetColumn(actions,3);header.Children.Add(actions);
        return header;
    }
    private Control BuildLibrary()
    {
        var outer = new StackPanel { Spacing = 11, Margin = new Thickness(17,20,13,17) };
        outer.Children.Add(Label("WORKSPACE",11,Muted,true));
        outer.Children.Add(Label("Your boards",19,Ink,true));
        outer.Children.Add(Label("Saved locally · no account needed",11,Muted));
        outer.Children.Add(ActionButton("＋  New board", NewBoard, true));
        outer.Children.Add(Divider());
        outer.Children.Add(_libraryItems);
        outer.Children.Add(Divider());
        outer.Children.Add(ActionButton("Collapse sidebar",()=>_libraryColumn.Width = _libraryColumn.Width.Value > 0 ? new GridLength(0) : new GridLength(230)));
        var scroll = new ScrollViewer { Content = outer, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        return new Border { Background = Brushes.White, Child = scroll, BorderBrush = Outline, BorderThickness = new Thickness(0,0,1,0) };
    }
    private Control BuildToolRail()
    {
        var container = new StackPanel { Spacing = 5, Margin = new Thickness(6,16) };
        var tools = new (string Icon,string Name,CanvasTool Tool)[]
        {
            ("↖","Select",CanvasTool.Select),("✎","Pen",CanvasTool.Pen),("▰","Marker",CanvasTool.Highlighter),
            ("⌫","Erase",CanvasTool.Eraser),("✥","Hand",CanvasTool.Hand),("▢","Rect",CanvasTool.Rectangle),
            ("◯","Ellipse",CanvasTool.Ellipse),("╱","Line",CanvasTool.Line),("➜","Arrow",CanvasTool.Arrow),
            ("▤","Note",CanvasTool.Sticky),("T","Text",CanvasTool.Text)
        };
        foreach(var (icon,name,tool) in tools)
        {
            var stack=new StackPanel { Spacing=2,HorizontalAlignment=HorizontalAlignment.Center };
            stack.Children.Add(Label(icon,21,Ink,true));stack.Children.Add(Label(name,10,Muted));
            var button=ActionButton("",()=>SelectTool(tool)); button.Content=stack;
            button.Padding=new Thickness(6,5);button.Width=58;button.MinHeight=51;
            _toolButtons[tool]=button;container.Children.Add(button);
        }
        _toolItems.Children.Add(container);
        return new Border { Background=Brushes.White, Child=new ScrollViewer { Content=_toolItems }, BorderBrush=Outline, BorderThickness=new Thickness(0,0,1,0) };
    }
    private Control BuildInspector()
    {
        var outer = new StackPanel { Spacing = 13, Margin = new Thickness(18,23) };
        outer.Children.Add(Label("PROPERTIES",11,Muted,true));
        outer.Children.Add(Label("Make it yours",19,Ink,true));
        outer.Children.Add(Label("INK COLOR",11,Muted,true));
        var swatches=new StackPanel { Orientation=Orientation.Horizontal, Spacing=5 };
        foreach(var hex in new[]{"#243148","#6458DC","#1682BE","#E75B7B","#148D76","#121212"})
        {
            var h=hex;var b=ActionButton("●",()=>{_canvas.InkColor=h;_status.Text="Ink color changed";});
            b.Foreground=B(h);b.FontSize=22;b.Width=30;b.Height=34;b.Padding=new Thickness(0);
            swatches.Children.Add(b);
        }
        outer.Children.Add(swatches);
        outer.Children.Add(Label("PEN SIZE",11,Muted,true));
        var slider=new Slider { Minimum=1,Maximum=30,Value=3,TickFrequency=1 };
        slider.PropertyChanged+=(_,e)=>{if(e.Property==Slider.ValueProperty)_canvas.InkWidth=slider.Value;};
        outer.Children.Add(slider);
        outer.Children.Add(Divider());
        outer.Children.Add(Label("SELECTION",11,Muted,true));
        outer.Children.Add(_selectionSummary);
        outer.Children.Add(_textEditor);
        outer.Children.Add(ActionButton("Apply text",()=>_canvas.UpdateSelectedText(_textEditor.Text??"")));
        outer.Children.Add(ActionButton("Duplicate  Ctrl+D",()=>_canvas.DuplicateSelection()));
        outer.Children.Add(ActionButton("Delete selection",()=>_canvas.DeleteSelection()));
        outer.Children.Add(Divider());
        outer.Children.Add(Label("CANVAS",11,Muted,true));
        outer.Children.Add(ActionButton("Toggle grid",()=>{_canvas.ShowGrid=!_canvas.ShowGrid;_canvas.Redraw();}));
        outer.Children.Add(ActionButton("Toggle light / dark",()=>{_canvas.IsDark=!_canvas.IsDark;_canvas.Redraw();}));
        outer.Children.Add(ActionButton("Fit all objects",()=>_canvas.FitToContent()));
        outer.Children.Add(Divider());
        outer.Children.Add(Label("TIP",11,Muted,true));
        outer.Children.Add(Label("Use the mouse wheel to zoom toward the pointer. Hold Alt to pan. Shift-click to select multiple objects.",12,Muted));
        var scroll = new ScrollViewer { Content = outer, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        return new Border { Background = Brushes.White, Child = scroll, BorderBrush=Outline, BorderThickness=new Thickness(1,0,0,0) };
    }
    private void SyncTools()
    {
        foreach (var (tool,button) in _toolButtons)
        {
            var active=_canvas.Tool==tool;
            button.Background=active?SoftAccent:Brushes.Transparent;
            button.BorderBrush=active?Accent:Brushes.Transparent;
        }
    }
    private void SelectTool(CanvasTool tool) { _canvas.SetTool(tool);SyncTools();_status.Text=$"{tool} tool";_canvas.Focus(); }
    private void SyncSelection()
    {
        var ids = _canvas.Selection;
        _selectionSummary.Text = ids.Count==0 ? "Click or drag to select objects" : $"{ids.Count} object(s) selected";
        var obj=_canvas.Engine.Document.Elements.FirstOrDefault(o=>ids.Contains(o.Id) && o.Kind is ElementKind.Sticky or ElementKind.Text);
        _textEditor.IsEnabled=obj != null; _textEditor.Text=obj?.Text ?? "";
    }
    private void RefreshLibrary()
    {
        _libraryItems.Children.Clear();
        foreach (var board in _store.ListBoards())
        {
            var current=board.Id==_canvas.Engine.Document.Id;
            var row=new StackPanel { Spacing=3 };
            row.Children.Add(Label(board.Name,13,current?Accent:Ink,true));
            row.Children.Add(Label(board.UpdatedUtc.ToLocalTime().ToString("MMM d, yyyy"),10,Muted));
            var button=ActionButton("",()=>Open(board));button.Content=row;
            button.HorizontalContentAlignment=HorizontalAlignment.Left;
            button.Background=current?SoftAccent:B("#F7F8FB");button.BorderBrush=current?Accent:Outline;
            _libraryItems.Children.Add(button);
        }
    }
    private void HookEngine() => _canvas.Engine.Changed += ScheduleSave;
    private void UpdateTitle()
    {
        var name=(_title.Text??"").Trim();
        if (name.Length==0 || name==_canvas.Engine.Document.Name) return;
        _canvas.Engine.Document.Name=name[..Math.Min(name.Length,90)];ScheduleSave();
    }
    private void Zoom(double factor)
    {
        _canvas.View.ZoomAround(factor,_canvas.Bounds.Width/2,_canvas.Bounds.Height/2);
        _canvas.Redraw();_zoom.Text=$"{_canvas.View.Zoom*100:0}%";
    }
    private void NewBoard() => Open(new BoardDocument { Name="Untitled board" });
    private void Open(BoardDocument board)
    {
        SaveNow(false);_canvas.ReplaceDocument(board);HookEngine();_title.Text=board.Name;
        _status.Text="Board opened";SaveNow(false);_canvas.Focus();
    }
    private void ScheduleSave() { _status.Text="● Unsaved changes";_saveTimer.Stop();_saveTimer.Start(); }
    private void SaveNow(bool showStatus)
    {
        _saveTimer.Stop();
        try { _store.Save(_canvas.Engine.Document);if(showStatus||_status.Text?.Contains("Unsaved")==true)_status.Text="✓ Saved locally";RefreshLibrary(); }
        catch(Exception ex) { _status.Text="Save failed — " + ex.Message; }
    }
    private async Task ImportAsync()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title="Import an Inklume or Bosha board",AllowMultiple=false,
          FileTypeFilter=new[]{new FilePickerFileType("JSON Whiteboard") { Patterns=new[]{"*.json"} }} });
        if(files.Count==0)return;
        try
        {
            await using var stream=await files[0].OpenReadAsync();using var reader=new StreamReader(stream);
            var imported=BoardCodec.Read(await reader.ReadToEndAsync());
            imported.Id=Guid.NewGuid().ToString("N");imported.Name+=" (imported)";
            Open(imported);SaveNow(true);
        }
        catch(Exception ex) { _status.Text="Import failed — " + ex.Message; }
    }
    private async Task ExportAsync()
    {
        var board=_canvas.Engine.Document;
        var name=new string(board.Name.Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_').ToArray()).Trim();
        if (string.IsNullOrEmpty(name)) name="Inklume board";
        try
        {
            var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title="Export an editable Inklume board",
                SuggestedFileName=name+".inklume.json",DefaultExtension="json",
                FileTypeChoices=new[]{new FilePickerFileType("Editable Inklume board") { Patterns=new[]{"*.json"} }}
            });
            if (file==null) return;
            await using var stream=await file.OpenWriteAsync();
            await using var writer=new StreamWriter(stream);
            await writer.WriteAsync(BoardCodec.Write(board));
            await writer.FlushAsync();
            _status.Text="✓ Board exported as editable JSON";
        }
        catch (Exception ex) { _status.Text="Export failed — "+ex.Message; }
    }
    private async Task ImportImageAsync()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title="Insert an image",AllowMultiple=false,
          FileTypeFilter=new[]{new FilePickerFileType("Images") { Patterns=new[]{"*.png","*.jpg","*.jpeg","*.webp"} }} });
        if(files.Count==0)return;
        try
        {
            await using var stream=await files[0].OpenReadAsync();
            using var mem=new MemoryStream();await stream.CopyToAsync(mem);
            if(mem.Length>8_000_000)throw new InvalidDataException("Image exceeds 8 MB");
            var file=files[0].Name.ToLowerInvariant();
            var mime=file.EndsWith(".png")?"image/png":file.EndsWith(".webp")?"image/webp":"image/jpeg";
            var center=_canvas.View.ToWorld(_canvas.Bounds.Width/2,_canvas.Bounds.Height/2);
            var element=new BoardElement { Kind=ElementKind.Image,X=center.X-200,Y=center.Y-130,
                Width=400,Height=260,EmbeddedImage=$"data:{mime};base64,{Convert.ToBase64String(mem.ToArray())}" };
            _canvas.Engine.BeginEdit();_canvas.Engine.Add(element);_canvas.Engine.CompleteEdit();_canvas.Redraw();
            _status.Text="Image inserted";
        }
        catch(Exception ex){_status.Text="Image import failed — " + ex.Message;}
    }
    private void OnShortcut(object? sender,KeyEventArgs e)
    {
        if(e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch(e.Key)
            {
                case Key.Z: if(_canvas.Engine.Undo())_canvas.Redraw();break;
                case Key.Y: if(_canvas.Engine.Redo())_canvas.Redraw();break;
                case Key.D: _canvas.DuplicateSelection();break;
                case Key.A: _canvas.SelectAll();break;
                case Key.S: SaveNow(true);break;
                case Key.O: _=ImportAsync();break;
                case Key.E: _=ExportAsync();break;
                default:return;
            }
            e.Handled=true;return;
        }
        if(e.Key==Key.Delete && _canvas.IsFocused) { _canvas.DeleteSelection();e.Handled=true; }
    }
}