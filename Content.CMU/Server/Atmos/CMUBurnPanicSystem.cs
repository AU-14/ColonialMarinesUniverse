using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Synth;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

public sealed class CMUBurnPanicSystem : EntitySystem
{
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    private TimeSpan _nextUpdate;
    private bool _forcingRoll;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBurnPanicComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<CMUBurnPanicComponent, ExtinguishedEvent>(OnExtinguished);
        SubscribeLocalEvent<CMUBurnPanicComponent, ResistFireAlertEvent>(OnResistFire);
    }

    private void OnIgnited(Entity<CMUBurnPanicComponent> ent, ref IgnitedEvent args)
    {
        if (HasComp<SynthComponent>(ent) || !_mobState.IsAlive(ent))
            return;

        var now = _timing.CurTime;
        if (now >= ent.Comp.NextEmoteAt)
        {
            ent.Comp.NextEmoteAt = now + ent.Comp.EmoteCooldown;
            _emote.TryEmoteWithChat(ent, ent.Comp.Emote, forceEmote: true, cooldown: TimeSpan.Zero);
        }

        ent.Comp.Forcing = false;
        ent.Comp.ForceRollAt = now + ent.Comp.ForceRollDelay;
    }

    private void OnExtinguished(Entity<CMUBurnPanicComponent> ent, ref ExtinguishedEvent args)
    {
        Stop(ent.Comp);
    }

    private void OnResistFire(Entity<CMUBurnPanicComponent> ent, ref ResistFireAlertEvent args)
    {
        // The player started rolling on their own, so they are never forced to for this fire.
        if (!_forcingRoll)
            ent.Comp.ForceRollAt = null;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var query = EntityQueryEnumerator<CMUBurnPanicComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var panic, out var flammable))
        {
            if (panic.ForceRollAt == null && !panic.Forcing)
                continue;

            if (!flammable.OnFire || flammable.FireStacks <= 0 || !_mobState.IsAlive(uid))
            {
                Stop(panic);
                continue;
            }

            if (!panic.Forcing)
            {
                if (now < panic.ForceRollAt)
                    continue;

                panic.Forcing = true;
                panic.ForceRollAt = null;
            }

            if (flammable.Resisting)
                continue;

            var roll = new ResistFireAlertEvent { User = uid, AlertId = flammable.FireAlert };
            _forcingRoll = true;
            RaiseLocalEvent(uid, roll);
            _forcingRoll = false;
        }
    }

    private static void Stop(CMUBurnPanicComponent panic)
    {
        panic.Forcing = false;
        panic.ForceRollAt = null;
    }
}
