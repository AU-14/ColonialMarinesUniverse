using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Medical.Asphyxiation;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Medical.Asphyxiation;

public sealed partial class GaspOnAsphyxiationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;

    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GaspOnAsphyxiationComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, GaspOnAsphyxiationComponent comp, DamageChangedEvent args)
    {
        if (_mobState.IsDead(uid))
            return;

        if (_timing.CurTime < comp.NextGasp)
            return;

        if (!args.Damageable.Damage.DamageDict.TryGetValue(AsphyxiationType, out var asphyxiation) ||
            asphyxiation < comp.Threshold)
        {
            return;
        }

        comp.NextGasp = _timing.CurTime + comp.Cooldown;
        _emote.TryEmoteWithChat(uid, comp.Emote, hideLog: true, ignoreActionBlocker: true, forceEmote: true);
    }
}
