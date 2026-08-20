using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.BlightWave;

public abstract partial class SharedBlightWaveSystem : EntitySystem
{
    [Dependency] protected EntityLookupSystem _lookup = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] protected INetManager _net = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDeafnessSystem _deaf = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] protected SharedTransformSystem _transform = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoBlightWaveComponent, CMUXenoBlightWaveActionEvent>(OnAction);
    }

    protected virtual void OnAction(Entity<CMUXenoBlightWaveComponent> xeno, ref CMUXenoBlightWaveActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        var coords = _transform.GetMapCoordinates(xeno);

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(coords, xeno.Comp.Range, _mobs);

        foreach (var mob in _mobs)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, mob))
                continue;

            if (_mobState.IsDead(mob))
                continue;

            _slow.TrySuperSlowdown(mob, xeno.Comp.SuperSlowDuration);
            _deaf.TryDeafen(mob, xeno.Comp.DazeDuration);

            _popup.PopupEntity(
                Loc.GetString("cmu14-xeno-blight-wave-hit"),
                mob, PopupType.MediumCaution);
        }

        _popup.PopupPredicted(
            Loc.GetString("cmu14-xeno-blight-wave-self"),
            Loc.GetString("cmu14-xeno-blight-wave-others", ("xeno", xeno.Owner)),
            xeno, xeno, PopupType.LargeCaution);
    }
}
