using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Atmos;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

public sealed partial class CMUHunterFireRollSystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHunterFireRollComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUHunterFireRollComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUHunterFireRollComponent, ResistFireAlertEvent>(OnResistFire, after: [typeof(FlammableSystem)]);
    }

    private void OnStartup(Entity<CMUHunterFireRollComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out FlammableComponent? flammable))
            return;

        // thralls get this at runtime and can lose it again, so keep what they had
        ent.Comp.OriginalResistTime ??= flammable.ResistTime;
        flammable.ResistTime = ent.Comp.ResistTime;
        Dirty(ent.Owner, flammable);
    }

    private void OnShutdown(Entity<CMUHunterFireRollComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.OriginalResistTime is not { } original ||
            TerminatingOrDeleted(ent) ||
            !TryComp(ent, out FlammableComponent? flammable))
        {
            return;
        }

        flammable.ResistTime = original;
        Dirty(ent.Owner, flammable);
    }

    private void OnResistFire(Entity<CMUHunterFireRollComponent> ent, ref ResistFireAlertEvent args)
    {
        if (!TryComp(ent, out FlammableComponent? flammable) ||
            flammable.ResistCompleteTime is not { } rollEnd ||
            rollEnd <= _timing.CurTime ||
            rollEnd == ent.Comp.LastRollEnd)
        {
            return;
        }

        // FlammableSystem already took OnFire.ResistStacks off for this roll, just top it up
        ent.Comp.LastRollEnd = rollEnd;
        var applied = CompOrNull<OnFireComponent>(ent)?.ResistStacks ?? 0;
        var extra = ent.Comp.ResistStacks - applied;
        if (extra < 0 && flammable.FireStacks > 0)
            _flammable.AdjustFireStacks(ent, Math.Max(extra, -flammable.FireStacks), flammable);
    }
}
