using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Power;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaApcSiphonSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier SiphonSound =
        new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/apc_power_drain.wav");

    private static readonly TimeSpan SiphonDuration = TimeSpan.FromSeconds(9.3);
    private static readonly TimeSpan SiphonCooldown = TimeSpan.FromMinutes(20);

    private static readonly DamageSpecifier SiphonDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 200 },
        },
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCApcComponent, GetVerbsEvent<InteractionVerb>>(OnApcGetVerbs);
        SubscribeLocalEvent<RMCApcComponent, YautjaApcSiphonDoAfterEvent>(OnSiphonDoAfter);
    }

    private void OnApcGetVerbs(EntityUid uid, RMCApcComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_power.TryGetWornBracer(args.User, out _))
            return;

        if (comp.Broken)
            return;

        if (TryComp<YautjaApcSiphonCooldownComponent>(uid, out var cooldown) &&
            _timing.CurTime < cooldown.SiphonAvailableAt)
        {
            var remaining = (int) Math.Ceiling((cooldown.SiphonAvailableAt - _timing.CurTime).TotalMinutes);
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-yautja-apc-siphon-verb"),
                Message = Loc.GetString("cmu-yautja-apc-siphon-cooldown", ("minutes", remaining)),
                Disabled = true,
            });
            return;
        }

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-yautja-apc-siphon-verb"),
            Act = () => TrySiphon(uid, user),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/zap.svg.192dpi.png")),
        });
    }

    private void TrySiphon(EntityUid apc, EntityUid user)
    {
        if (!HasComp<RMCApcComponent>(apc))
            return;

        if (!_power.TryGetWornBracer(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-apc-siphon-no-bracer"), user, user, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, SiphonDuration,
            new YautjaApcSiphonDoAfterEvent(), apc, apc)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPvs(SiphonSound, apc);
        Spawn("EffectSparks", _transform.GetMapCoordinates(apc));
        _popup.PopupEntity(Loc.GetString("cmu-yautja-apc-siphon-start"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} began siphoning power from {ToPrettyString(apc):apc}");
    }

    private void OnSiphonDoAfter(EntityUid uid, RMCApcComponent comp, YautjaApcSiphonDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        if (!_power.TryGetWornBracer(user, out var bracer))
            return;

        var rechargeAmount = bracer.Comp.MaxCharge * FixedPoint2.New(0.15f);
        _power.RegenPower(bracer, rechargeAmount);

        _damage.TryChangeDamage(uid, SiphonDamage);

        Spawn("EffectSparks", _transform.GetMapCoordinates(uid));

        var cooldown = EnsureComp<YautjaApcSiphonCooldownComponent>(uid);
        cooldown.SiphonAvailableAt = _timing.CurTime + SiphonCooldown;
        Dirty(uid, cooldown);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-apc-siphon-complete"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):player} siphoned power from {ToPrettyString(uid):apc}, bracer recharged 15%");
    }
}
