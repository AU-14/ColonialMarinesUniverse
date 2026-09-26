using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Damage.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;

namespace Content.Server.CMU14.Development;

public sealed class WaterTestMapSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private RMCStandingSystem _rest = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<WaterTestSubjectComponent, MapInitEvent>(OnSubjectMapInit);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Handled || _ticker.LobbyEnabled)
            return;

        var maps = EntityQueryEnumerator<WaterTestMapComponent>();
        while (maps.MoveNext(out var uid, out _))
        {
            if (_station.GetOwningStation(uid) != args.Station)
                continue;

            // Local testing must not depend on the player's saved combat-job preferences.
            args.Handled = true;
            _ticker.DoSpawn(args.Player, args.Profile, args.Station, "Passenger", true,
                out var player, out _, out _);
            EnsureComp<GodmodeComponent>(player);
            Log.Info($"Water test arena spawned playable tester {ToPrettyString(player)}.");
            return;
        }
    }

    private void OnSubjectMapInit(Entity<WaterTestSubjectComponent> ent, ref MapInitEvent args)
    {
        // Samples stay available for possession and do not attack the tester autonomously.
        EnsureComp<GodmodeComponent>(ent);
        RemComp<ParasiteAIComponent>(ent);

        if (ent.Comp.Dead)
        {
            _mobState.ChangeMobState(ent, MobState.Dead);
            return;
        }

        if (!ent.Comp.Resting)
            return;

        if (HasComp<XenoComponent>(ent))
        {
            EnsureComp<XenoRestingComponent>(ent);
            _appearance.SetData(ent, XenoVisualLayers.Base, XenoRestState.Resting);
            var restEvent = new XenoRestEvent(true);
            RaiseLocalEvent(ent, ref restEvent);
        }
        else
        {
            _rest.SetRest((ent, null), true);
            _standing.Down(ent, playSound: false);
        }
    }
}
