using Content.Client.CMU14.Threats;
using Content.Client.Lobby.UI;
using Content.Shared.CMU14.Threats;

namespace Content.Client.UserInterface.Systems.Ghost.Controls.Roles;

public sealed partial class GhostRolesWindow
{
    public event Action<uint, bool>? OnForceInterestChanged;
    public event Action<uint>? OnForceCompleted;

    public void AddForceEntry(ForceInterestInfo force)
    {
        var entry = new ForceInterestEntry(force);
        entry.OnInterestChanged += (id, interested) => OnForceInterestChanged?.Invoke(id, interested);
        entry.OnCompleted += id => OnForceCompleted?.Invoke(id);
        CrtLobbyTheme.Apply(entry);
        EntryContainer.AddChild(entry);
        _entries.Add(new EntryState(entry, null!, null!, force.Name, string.Empty));
        if (!_updatingEntries)
            UpdateVisibleEntries();
    }
}
