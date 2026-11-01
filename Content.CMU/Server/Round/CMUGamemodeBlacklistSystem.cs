using System.Linq;
using Content.Server.GameTicking;

namespace Content.Server.CMU14.Round;

public sealed partial class CMUGamemodeBlacklistSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly AuRoundSystem _auRound = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGamemodeBlacklistComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUGamemodeBlacklistComponent> ent, ref MapInitEvent args)
    {
        // An empty whitelist would delete the entity in every mode; treat that as a mistake and keep it.
        if (ent.Comp.Inverted && ent.Comp.BlacklistedGamemodes.Count == 0)
        {
            Log.Error($"{ToPrettyString(ent)} has an inverted gamemode blacklist with no gamemodes, keeping it");
            return;
        }

        // Fail open: with no known preset the entity stays. A wrong deletion would empty normal rounds.
        // The ticker preset fields stay null through AU round start; SelectedPreset is the only live source there.
        var preset = _ticker.CurrentPreset?.ID ?? _ticker.Preset?.ID ?? _auRound.SelectedPreset?.ID;
        if (preset == null)
            return;

        // Inverted turns the list into a whitelist: delete in every mode except the listed ones.
        if (ent.Comp.BlacklistedGamemodes.Contains(preset, StringComparer.OrdinalIgnoreCase) != ent.Comp.Inverted)
            QueueDel(ent);
    }
}
