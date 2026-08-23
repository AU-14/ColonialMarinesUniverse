using System;
using System.Linq;
using Content.Shared._CMU14.Yautja;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaClanAdminEditorState
{
    private long _lastMutationVersion;

    public int? EditingClanId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Color { get; private set; } = "";
    public bool IsEditing => EditingClanId != null;

    public void BeginEdit(YautjaClanAdminClanState clan)
    {
        EditingClanId = clan.Id;
        Name = clan.Name;
        Description = clan.Description;
        Color = clan.Color;
    }

    public void CaptureDraft(string name, string description, string color)
    {
        Name = name;
        Description = description;
        Color = color;
    }

    public void ApplyState(YautjaClanAdminEuiState state)
    {
        var isNewMutation = state.ClanMutationVersion > _lastMutationVersion;
        _lastMutationVersion = Math.Max(_lastMutationVersion, state.ClanMutationVersion);

        if (isNewMutation &&
            !IsEditing &&
            state.LastMutationKind == YautjaClanAdminMutationKind.Created)
        {
            Cancel();
            return;
        }

        if (EditingClanId is not { } editingClanId)
            return;

        if (isNewMutation &&
            state.LastMutatedClanId == editingClanId &&
            state.LastMutationKind == YautjaClanAdminMutationKind.Deleted)
        {
            Cancel();
            return;
        }

        var clan = state.Clans.FirstOrDefault(entry => entry.Id == editingClanId);
        if (clan == null)
        {
            Cancel();
            return;
        }

        if (isNewMutation &&
            state.LastMutatedClanId == editingClanId &&
            state.LastMutationKind == YautjaClanAdminMutationKind.Updated)
        {
            BeginEdit(clan);
        }
    }

    public void Cancel()
    {
        EditingClanId = null;
        Name = "";
        Description = "";
        Color = "";
    }
}
