using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._AU14.WorkingJoe;

public sealed class WorkingJoeVoiceFavorites
{
    private static readonly ResPath Path = new("/working_joe_voice_favorites.txt");

    private readonly IResourceManager _resource;
    private readonly HashSet<string> _favorites = new();

    public WorkingJoeVoiceFavorites(IResourceManager resource)
    {
        _resource = resource;
        Load();
    }

    public bool Contains(string emoteId) => _favorites.Contains(emoteId);

    public void Toggle(string emoteId)
    {
        if (!_favorites.Remove(emoteId))
            _favorites.Add(emoteId);

        Save();
    }

    private void Load()
    {
        if (!_resource.UserData.TryReadAllText(Path, out var text))
            return;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                _favorites.Add(trimmed);
        }
    }

    private void Save()
    {
        var content = string.Join("\n", _favorites);
        _resource.UserData.WriteAllText(Path, content);
    }
}
