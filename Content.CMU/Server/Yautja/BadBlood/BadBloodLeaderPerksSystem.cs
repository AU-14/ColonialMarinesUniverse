using Content.Shared.Actions;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 Playtest 5: what follows leadership besides <see cref="BadBloodLeaderComponent"/> itself.
/// FLOW-701 (D3): the Hunt Leader's command sight (<see cref="BadBloodCommandSightComponent"/>); the honourable visor
/// is the Engineer's alone now. ECS-705: the War Cry action (<see cref="BadBloodWarCryComponent"/>): clanmates within
/// Radius move faster and stagger for less for Duration. Both are moved on the broadcast
/// <see cref="BadBloodLeaderChangedEvent"/>, so the rule system is untouched.
/// </summary>
public sealed class BadBloodLeaderPerksSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<YautjaBadBloodComponent>> _near = new();
    private readonly List<EntityUid> _ended = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodLeaderChangedEvent>(OnLeaderChanged);
        SubscribeLocalEvent<BadBloodWarCryComponent, BadBloodWarCryActionEvent>(OnWarCry);
        SubscribeLocalEvent<BadBloodWarCriedComponent, GetChemicalStunTimeMultiplierEvent>(OnStunTime);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _ended.Clear();
        var query = EntityQueryEnumerator<BadBloodWarCriedComponent>();
        while (query.MoveNext(out var uid, out var cried))
        {
            if (now >= cried.EndsAt)
                _ended.Add(uid);
        }

        foreach (var uid in _ended)
        {
            RemComp<BadBloodWarCriedComponent>(uid);
        }
    }

    private void OnLeaderChanged(ref BadBloodLeaderChangedEvent ev)
    {
        if (ev.Old is { } old && !TerminatingOrDeleted(old))
        {
            RemComp<BadBloodCommandSightComponent>(old);
            if (TryComp(old, out BadBloodWarCryComponent? cry))
                _actions.RemoveAction(old, cry.ActionEntity);

            RemComp<BadBloodWarCryComponent>(old);
        }

        if (ev.New is not { } leader || TerminatingOrDeleted(leader))
            return;

        EnsureComp<BadBloodCommandSightComponent>(leader);
        var warCry = EnsureComp<BadBloodWarCryComponent>(leader);
        _actions.AddAction(leader, ref warCry.ActionEntity, warCry.Action);
        Dirty(leader, warCry);
    }

    /// <summary>
    /// The Hunt Leader's War Cry: every living Bad Blood within Radius, the Leader too, is cried on for Duration.
    /// Returns how many heard it.
    /// </summary>
    public int WarCry(Entity<BadBloodWarCryComponent> leader)
    {
        var until = _timing.CurTime + leader.Comp.Duration;
        _near.Clear();
        _lookup.GetEntitiesInRange(Transform(leader).Coordinates, leader.Comp.Radius, _near);

        var heard = 0;
        foreach (var clanmate in _near)
        {
            if (!_mobState.IsAlive(clanmate))
                continue;

            var cried = EnsureComp<BadBloodWarCriedComponent>(clanmate);
            cried.EndsAt = until;
            cried.SpeedMultiplier = leader.Comp.SpeedMultiplier;
            cried.StaggerMultiplier = leader.Comp.StaggerMultiplier;
            Dirty(clanmate, cried);
            _movement.RefreshMovementSpeedModifiers(clanmate.Owner);
            heard++;
        }

        _audio.PlayPvs(leader.Comp.Sound, leader);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-war-cry", ("name", Content.Shared.IdentityManagement.Identity.Name(leader, EntityManager))),
            leader, PopupType.MediumCaution);
        return heard;
    }

    private void OnWarCry(Entity<BadBloodWarCryComponent> ent, ref BadBloodWarCryActionEvent args)
    {
        if (args.Handled || !_mobState.IsAlive(ent))
            return;

        args.Handled = true;
        WarCry(ent);
    }

    private void OnStunTime(Entity<BadBloodWarCriedComponent> ent, ref GetChemicalStunTimeMultiplierEvent args)
    {
        args.Multiplier *= ent.Comp.StaggerMultiplier;
    }
}
