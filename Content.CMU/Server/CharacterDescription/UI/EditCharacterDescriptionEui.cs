using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Shared.CCVar;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Database;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.CharacterDescription.UI;

/// <summary>
/// Lets a player rewrite their character's short examine text and full description for the current round.
/// Only the spawned character changes; the saved profile is untouched.
/// </summary>
[UsedImplicitly]
public sealed partial class EditCharacterDescriptionEui : BaseEui
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entManager = default!;

    private readonly EntityUid _target;

    public EditCharacterDescriptionEui(EntityUid target)
    {
        _target = target;
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var state = new EditCharacterDescriptionEuiState
        {
            MaxShortExamine = _config.GetCVar(CCVars.MaxShortExamineLength),
            MaxFullDescription = _config.GetCVar(CCVars.MaxCharacterDescriptionFieldLength),
        };

        if (_entManager.TryGetComponent(_target, out CharacterDescriptionComponent? desc))
        {
            state.ShortExamine = desc.ShortExamine;
            state.FullDescription = desc.FullDescription;
        }

        return state;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not EditCharacterDescriptionSaveMessage save)
            return;

        // Only the player still controlling the character may rewrite it.
        if (Player.AttachedEntity != _target ||
            !_entManager.TryGetComponent(_target, out CharacterDescriptionComponent? desc))
        {
            Close();
            return;
        }

        desc.ShortExamine = Clean(save.ShortExamine, _config.GetCVar(CCVars.MaxShortExamineLength));
        desc.FullDescription = Clean(save.FullDescription, _config.GetCVar(CCVars.MaxCharacterDescriptionFieldLength));
        _entManager.Dirty(_target, desc);

        _adminLog.Add(LogType.RMCSetPose,
            $"{_entManager.ToPrettyString(_target):player} set their short examine to \"{desc.ShortExamine}\" and full description to \"{desc.FullDescription}\"");

        Close();
    }

    private static string Clean(string text, int maxLength)
    {
        var stripped = FormattedMessage.RemoveMarkupPermissive(text).Trim();
        return stripped.Length > maxLength ? stripped[..maxLength] : stripped;
    }
}
