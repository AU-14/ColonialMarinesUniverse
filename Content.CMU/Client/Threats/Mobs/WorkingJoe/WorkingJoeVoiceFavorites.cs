using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Threats.Mobs.WorkingJoe;

public sealed class WorkingJoeVoiceFavorites
{
    private static readonly ResPath Path = new("/working_joe_voice_favorites.txt");
    private readonly HashSet<string> _favorites = new();

    private readonly IResourceManager _resource;

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
        if (!_resource.UserData.TryReadAllText(Path, out string? text))
            return;

        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
                _favorites.Add(trimmed);
        }
    }

    private void Save()
    {
        string content = string.Join("\n", _favorites);
        _resource.UserData.WriteAllText(Path, content);
    }
}
