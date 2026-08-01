using Content.Server.Administration;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._RMC14.Areas;
using Content.Shared.Radio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     The audio (early-warning) trap's payload. The wire lifecycle is entirely the tripwire system's;
///     this system only:
///     - asks the sapper to name the trap the moment it is planted (a small dialog box), and
///     - when the wire is crossed, blows the whistle at the wire and reports the trap's name and area
///       over the CLF radio channel, then re-arms after a cooldown.
/// </summary>
public sealed class SapperAudioTrapSystem : EntitySystem
{
    [Dependency] private QuickDialogSystem _dialog = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private IGameTiming _timing = default!;

    // The CLF cell channel the alert goes out on. One place to retarget it.
    private static readonly ProtoId<RadioChannelPrototype> AlertChannel = "radioCLF";

    public override void Initialize()
    {
        base.Initialize();

        // After the shared handler has actually planted it, ask the sapper what to call this trap.
        SubscribeLocalEvent<SapperAudioTrapComponent, SapperTrapDeployDoAfterEvent>(OnDeployed, after: new[] { typeof(SapperTrapSystem) });
        SubscribeLocalEvent<SapperAudioTrapComponent, SapperAudioTrapTrippedEvent>(OnTripped);
    }

    private void OnDeployed(Entity<SapperAudioTrapComponent> ent, ref SapperTrapDeployDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<SapperTrapComponent>(ent, out var trap) || !trap.Deployed)
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var trapUid = ent.Owner;
        var comp = ent.Comp;
        _dialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("insfor-sapper-audio-name-title"),
            Loc.GetString("insfor-sapper-audio-name-prompt"),
            (string name) =>
            {
                if (Deleted(trapUid))
                    return;

                // Untrusted client text: trim and clamp before it ever hits the radio.
                name = name.Trim();
                if (name.Length > comp.MaxNameLength)
                    name = name[..comp.MaxNameLength];
                if (name.Length > 0)
                    comp.TrapName = name;
            });
    }

    private void OnTripped(Entity<SapperAudioTrapComponent> ent, ref SapperAudioTrapTrippedEvent args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextAlert)
            return;

        ent.Comp.NextAlert = now + ent.Comp.AlertCooldown;

        // The whistle blows where the wire was crossed, not at the buried box.
        _audio.PlayPvs(ent.Comp.AlarmSound, args.Where);

        var location = Loc.GetString("insfor-sapper-audio-location-unknown");
        if (_areas.TryGetArea(ent.Owner, out _, out var areaProto))
            location = areaProto.Name;

        var name = ent.Comp.TrapName.Length > 0 ? ent.Comp.TrapName : Loc.GetString("insfor-sapper-audio-default-name");
        var message = Loc.GetString("insfor-sapper-audio-radio-alert", ("name", name), ("location", location));
        _radio.SendRadioMessage(ent, message, AlertChannel, ent);
    }
}
