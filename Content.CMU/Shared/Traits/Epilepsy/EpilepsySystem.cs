using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Traits.Epilepsy;

public sealed partial class EpilepsySystem : EntitySystem
{
    /// <summary>
    ///     Chance a flash (a handheld flash or a camera flash) sets off a seizure.
    /// </summary>
    public const float FlashSeizureChance = 0.25f;

    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EpilepsyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EpilepsyComponent, AfterFlashedEvent>(OnFlashed);
        SubscribeLocalEvent<EpilepsyComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<EpilepsyComponent> ent, ref ExaminedEvent args)
    {
        if (_timing.CurTime >= ent.Comp.SeizingUntil)
            return;

        args.PushMarkup(Loc.GetString("au14-epilepsy-examine-seizing", ("user", Identity.Entity(ent, EntityManager))));
    }

    private void OnStartup(Entity<EpilepsyComponent> ent, ref ComponentStartup args)
    {
        ScheduleNext(ent);
    }

    private void OnFlashed(Entity<EpilepsyComponent> ent, ref AfterFlashedEvent args)
    {
        // Raised on the user and the flash too; only the person flashed can seize.
        if (args.Target != ent.Owner || !TryComp(args.Used, out CMUEpilepsyTriggerComponent? trigger))
            return;

        TryFlashSeizure(ent.AsNullable(), trigger.Chance);
    }

    /// <summary>
    ///     Rolls for a flash setting off a seizure in someone with epilepsy.
    /// </summary>
    public bool TryFlashSeizure(Entity<EpilepsyComponent?> ent, float chance = FlashSeizureChance)
    {
        if (_net.IsClient ||
            !Resolve(ent, ref ent.Comp, false) ||
            _timing.CurTime < ent.Comp.SeizingUntil ||
            _timing.CurTime < ent.Comp.SuppressedUntil)
        {
            return false;
        }

        if (!_random.Prob(chance))
            return false;

        Seize((ent, ent.Comp));
        return true;
    }

    private void ScheduleNext(Entity<EpilepsyComponent> ent)
    {
        var interval = _random.NextFloat((float)ent.Comp.IntervalMin.TotalSeconds, (float)ent.Comp.IntervalMax.TotalSeconds);
        ent.Comp.NextSeizure = _timing.CurTime + TimeSpan.FromSeconds(interval);
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<EpilepsyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (time < comp.NextSeizure)
                continue;

            // Medicated: the seizure that was due is skipped and the clock starts over.
            if (time < comp.SuppressedUntil)
            {
                ScheduleNext((uid, comp));
                continue;
            }

            Seize((uid, comp));
        }
    }

    /// <summary>
    ///     Cuts a seizure short: the convulsions, the collapse and the darkness all end now.
    /// </summary>
    public bool TryEndSeizure(Entity<EpilepsyComponent> ent)
    {
        var time = _timing.CurTime;
        if (time >= ent.Comp.SeizingUntil)
            return false;

        ent.Comp.SeizingUntil = time;
        Dirty(ent);

        _stun.TryClearStunAndKnockdown(ent);
        _statusEffects.TryRemoveStatusEffect(ent, "Jitter");
        RemCompDeferred<ScreechBlindComponent>(ent);
        _popup.PopupEntity(Loc.GetString("au14-epilepsy-seizure-ends"), ent, ent, PopupType.Medium);
        return true;
    }

    private void Seize(Entity<EpilepsyComponent> ent)
    {
        var uid = ent.Owner;
        var comp = ent.Comp;
        var time = _timing.CurTime;

        var duration = TimeSpan.FromSeconds(_random.NextFloat(comp.DurationMin, comp.DurationMax));
        comp.SeizingUntil = time + duration;

        // A seizure resets the clock to the next one, whatever set it off. This also sends SeizingUntil.
        ScheduleNext(ent);

        _popup.PopupEntity(Loc.GetString("au14-epilepsy-seizure"), uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("au14-epilepsy-seizure-others", ("user", Identity.Entity(uid, EntityManager))),
            uid,
            Filter.PvsExcept(uid),
            true,
            PopupType.MediumCaution);
        _jitter.DoJitter(uid, duration, true, 10, 6);
        _stun.TryParalyze(uid, duration, true);

        var blind = EnsureComp<ScreechBlindComponent>(uid);
        blind.Radius = comp.VisionDimRadius;
        blind.EndsAt = time + duration;
        Dirty(uid, blind);
    }
}
