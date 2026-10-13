using System.Linq;
using Content.Shared.CMU14.Crayon;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Crayon;

/// <summary>
/// The player's saved crayon symbols, one per line in user data: name|size|colours|pixels.
/// </summary>
public sealed class CMUCrayonSymbolStorage
{
    [Dependency] private IResourceManager _resources = default!;

    private static readonly ResPath File = new("/cmu_crayon_symbols.txt");

    public readonly List<(string Name, CMUCrayonSymbol Symbol)> Symbols = new();

    public CMUCrayonSymbolStorage()
    {
        IoCManager.InjectDependencies(this);
        Load();
    }

    public void Save(string name, CMUCrayonSymbol symbol)
    {
        Symbols.RemoveAll(s => s.Name == name);
        Symbols.Add((name, symbol));
        Write();
    }

    public void Delete(string name)
    {
        Symbols.RemoveAll(s => s.Name == name);
        Write();
    }

    private void Load()
    {
        Symbols.Clear();
        if (!_resources.UserData.Exists(File))
            return;

        using var reader = _resources.UserData.OpenText(File);
        while (reader.ReadLine() is { } line)
        {
            if (Parse(line) is { } entry)
                Symbols.Add(entry);
        }
    }

    private void Write()
    {
        using var writer = _resources.UserData.OpenWriteText(File);
        foreach (var (name, symbol) in Symbols)
        {
            var colors = string.Join(',', symbol.Palette.Select(c => c.ToHex()));
            writer.WriteLine($"{name.Replace('|', ' ')}|{symbol.Size}|{colors}|{Convert.ToBase64String(symbol.Pixels)}");
        }
    }

    private static (string, CMUCrayonSymbol)? Parse(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var size))
            return null;

        try
        {
            var symbol = new CMUCrayonSymbol
            {
                Size = size,
                Palette = parts[2].Split(',').Select(h => Color.FromHex(h)).ToList(),
                Pixels = Convert.FromBase64String(parts[3]),
            };
            return symbol.IsValid() ? (parts[0], symbol) : null;
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return null;
        }
    }
}
