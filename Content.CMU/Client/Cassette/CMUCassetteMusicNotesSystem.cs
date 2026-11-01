using Content.Shared._RMC14.Cassette;
using Content.Shared.CCVar;
using Content.Shared.Item;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Cassette;

/// <summary>
///     Redraws everyone's cassette players when the setting to hide their music notes changes.
/// </summary>
public sealed class CMUCassetteMusicNotesSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        Subs.CVar(_cfg, CCVars.CMUHideCassetteMusicNotes, _ => Refresh());
    }

    private void Refresh()
    {
        var query = EntityQueryEnumerator<CassettePlayerComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _item.VisualsChanged(uid);
        }
    }
}
