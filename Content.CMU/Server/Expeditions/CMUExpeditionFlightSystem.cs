using Content.Server.CMU14.Fighter;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Expeditions;

/// <summary>Only the seated pilot can choose a theater; native takeoff clearance still applies.</summary>
public sealed partial class CMUExpeditionFlightSystem : EntitySystem
{
    [Dependency] private FighterSystem _fighters = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterSeatComponent, GetVerbsEvent<Verb>>(OnVerbs);
    }

    private void OnVerbs(Entity<FighterSeatComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Pilot || ent.Comp.Occupant != args.User ||
            ent.Comp.Aircraft is not { } aircraft || !TryComp<FighterAircraftComponent>(aircraft, out var flight) ||
            flight.GroundState != FighterGroundState.Grounded)
            return;
        var user = args.User;
        var maps = EntityQueryEnumerator<CMUExpeditionMapComponent>();
        while (maps.MoveNext(out var map, out var expedition))
        {
            if (!expedition.Ready || expedition.LandingBeacon == null) continue;
            var destination = map;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-expedition-fly", ("sector", Name(map))),
                Act = () => _fighters.TryLaunchExpedition(user, destination),
            });
        }
    }
}
