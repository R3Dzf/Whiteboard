using System.Text.Json;

namespace Inklume.Core;

public sealed class BoardStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; } = directory;

    public void Save(BoardDocument board)
    {
        if (!Guid.TryParse(board.Id, out var guid)) throw new InvalidDataException("Invalid board identifier");
        Directory.CreateDirectory(DirectoryPath);
        var destination = Path.Combine(DirectoryPath, guid.ToString("N") + ".inklume.json");
        var temp = destination + ".tmp";
        var backup = destination + ".bak";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(board, Options);
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(destination)) File.Replace(temp, destination, backup);
        else File.Move(temp, destination);
    }

    public BoardDocument Load(string file)
    {
        var name = Path.GetFileName(file);
        if (name.IndexOfAny(['/', '\\']) >= 0 || !name.EndsWith(".inklume.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unrecognized board file");
        var path = Path.Combine(DirectoryPath, name);
        try { return BoardCodec.Read(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
        {
            var backup = path + ".bak";
            if (File.Exists(backup)) return BoardCodec.Read(File.ReadAllText(backup));
            throw;
        }
    }

    public IReadOnlyList<BoardDocument> ListBoards()
    {
        if (!Directory.Exists(DirectoryPath)) return Array.Empty<BoardDocument>();
        var result = new List<BoardDocument>();
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.inklume.json"))
        {
            try { result.Add(Load(Path.GetFileName(file))); }
            catch (Exception) { /* Corrupt entries never take the whole gallery down. */ }
        }
        return result.OrderByDescending(b => b.UpdatedUtc).ToArray();
    }
}
