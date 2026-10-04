using Content.Server.Medical;
using Content.Shared._AU14.WorkingJoe;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._AU14.WorkingJoe;

public sealed partial class WorkingJoeRebootSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DefibrillatorSystem _defib = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier ChargeSound =
        new("/Audio/_RMC14/Medical/reset_key_powerup.ogg", AudioParams.Default.WithVolume(-8));

    private static readonly SoundPathSpecifier ZapSound =
        new("/Audio/_RMC14/Medical/reset_key_release.ogg", AudioParams.Default.WithVolume(-6));

    private static readonly SoundPathSpecifier SuccessSound =
        new("/Audio/_RMC14/Medical/reset_key_boot_on.ogg", AudioParams.Default.WithVolume(-6));

    private static readonly SoundPathSpecifier FailSound =
        new("/Audio/_RMC14/Medical/reset_key_shortbeep.ogg", AudioParams.Default.WithVolume(-6));

    private const float DoAfterSeconds = 5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorkingJoeVoiceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WorkingJoeVoiceComponent, WorkingJoeRebootDoAfterEvent>(OnRebootDoAfter);
    }

    private void OnGetVerbs(Entity<WorkingJoeVoiceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_mobState.IsDead(ent.Owner))
            return;

        if (!TryComp<DamageableComponent>(ent.Owner, out var damageable))
            return;

        if (damageable.TotalDamage > 0)
            return;

        var user = args.User;
        var target = ent.Owner;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("working-joe-reboot-verb"),
            Act = () =>
            {
                var ev = new WorkingJoeRebootDoAfterEvent();
                var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(DoAfterSeconds), ev, target, target)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                };
                if (_doAfter.TryStartDoAfter(doAfter))
                    _audio.PlayPvs(ChargeSound, target);
            },
            Priority = 2,
        });
    }

    private void OnRebootDoAfter(Entity<WorkingJoeVoiceComponent> ent, ref WorkingJoeRebootDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!_mobState.IsDead(ent.Owner))
            return;

        RemComp<UnrevivableComponent>(ent.Owner);

        _audio.PlayPvs(ZapSound, ent.Owner);

        var key = Spawn("RMCSynthResetKeySeegson", Transform(ent.Owner).Coordinates);
        _toggle.TryActivate(key, args.User);

        var revived = false;
        if (TryComp<DefibrillatorComponent>(key, out var defibComp))
        {
            _defib.Zap(key, ent.Owner, args.User, defibComp);
            revived = !_mobState.IsDead(ent.Owner);
        }

        QueueDel(key);

        _audio.PlayPvs(revived ? SuccessSound : FailSound, ent.Owner);
    }
}
