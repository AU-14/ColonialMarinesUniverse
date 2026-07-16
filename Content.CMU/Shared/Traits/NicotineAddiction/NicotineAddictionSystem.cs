using Content.Shared.Alert;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Traits.NicotineAddiction;

public sealed partial class NicotineAddictionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NicotineAddictionComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<NicotineAddictionComponent> ent, ref ComponentStartup args)
    {
        ResetCraving(ent);
    }

    /// <summary>
    ///     Called whenever the entity metabolizes nicotine (i.e. smokes). Resets the craving clock
    ///     and clears any active craving/shaking.
    /// </summary>
    public void Smoked(EntityUid uid)
    {
        if (!TryComp<NicotineAddictionComponent>(uid, out var comp))
            return;

        var wasCraving = comp.Craving;
        ResetCraving((uid, comp));

        if (wasCraving)
            _popup.PopupEntity(Loc.GetString("au14-nicotineaddiction-satisfied"), uid, uid, PopupType.Medium);
    }

    private void ResetCraving(Entity<NicotineAddictionComponent> ent)
    {
        var time = _timing.CurTime;
        ent.Comp.LastSmoked = time;
        ent.Comp.NextShake = time + ent.Comp.ShakeThreshold;
        ent.Comp.Craving = false;
        Dirty(ent);

        _alerts.ClearAlert(ent.Owner, ent.Comp.CravingAlert);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<NicotineAddictionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (time < comp.NextCheck)
                continue;

            comp.NextCheck = time + comp.TimeBetweenChecks;
            Dirty(uid, comp);

            var elapsed = time - comp.LastSmoked;
            var craving = elapsed >= comp.CravingThreshold;

            if (craving != comp.Craving)
            {
                comp.Craving = craving;
                Dirty(uid, comp);

                if (craving)
                {
                    _alerts.ShowAlert(uid, comp.CravingAlert);
                    _popup.PopupEntity(Loc.GetString("au14-nicotineaddiction-onset"), uid, uid, PopupType.Medium);
                }
                else
                {
                    _alerts.ClearAlert(uid, comp.CravingAlert);
                }
            }

            if (craving && time >= comp.NextCravingMessage)
            {
                comp.NextCravingMessage = time + comp.CravingMessageCooldown;
                _popup.PopupEntity(Loc.GetString("au14-nicotineaddiction-craving"), uid, uid, PopupType.Small);
            }

            if (elapsed >= comp.ShakeThreshold && time >= comp.NextShake)
            {
                comp.NextShake = time + TimeSpan.FromSeconds(
                    _random.NextFloat((float)comp.ShakeIntervalMin.TotalSeconds, (float)comp.ShakeIntervalMax.TotalSeconds));
                Dirty(uid, comp);

                _jitter.DoJitter(uid, comp.ShakeDuration, true, 8, 4);
                _popup.PopupEntity(Loc.GetString("au14-nicotineaddiction-shake"), uid, uid, PopupType.SmallCaution);
            }
        }
    }
}
