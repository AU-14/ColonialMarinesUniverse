using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaHuntTeleporterSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaTeleportSystem _teleport = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");
    private static readonly TimeSpan StepDuplicateWindow = TimeSpan.FromMilliseconds(500);
    private readonly Dictionary<(EntityUid User, YautjaHuntTeleporterKind Kind), TimeSpan> _nextStepAt = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHuntTeleporterComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<YautjaHuntTeleporterComponent, StepTriggeredOnEvent>(OnStepTriggeredOn);
        SubscribeLocalEvent<YautjaHuntTeleporterComponent, YautjaYoungbloodDeployConfirmedEvent>(OnYoungbloodDeployConfirmed);
    }

    private void OnStepTriggerAttempt(Entity<YautjaHuntTeleporterComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnStepTriggeredOn(Entity<YautjaHuntTeleporterComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (!TryConsumeStep(args.Tripper, ent.Comp.Kind))
            return;

        if (!CanUseTeleporter(args.Tripper, ent.Comp, true))
            return;

        if (!TryGetDestination(ent.Comp, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-teleporter-no-destination"), args.Tripper, args.Tripper, PopupType.SmallCaution);
            return;
        }

        OpenConfirmation(ent, args.Tripper);
    }

    private void OnYoungbloodDeployConfirmed(Entity<YautjaHuntTeleporterComponent> ent, ref YautjaYoungbloodDeployConfirmedEvent args)
    {
        var user = GetEntity(args.User);
        if (Deleted(user) || !CanUseTeleporter(user, ent.Comp, true))
            return;

        Teleport(ent, user);
    }

    private void OpenConfirmation(Entity<YautjaHuntTeleporterComponent> ent, EntityUid user)
    {
        var title = ent.Comp.Kind == YautjaHuntTeleporterKind.Young
            ? "cmu-yautja-hunt-teleporter-young-confirm-title"
            : "cmu-yautja-hunt-teleporter-ship-confirm-title";
        var message = ent.Comp.Kind == YautjaHuntTeleporterKind.Young
            ? "cmu-yautja-hunt-teleporter-young-confirm-message"
            : "cmu-yautja-hunt-teleporter-ship-confirm-message";

        _dialog.OpenConfirmation(
            ent.Owner,
            user,
            Loc.GetString(title),
            Loc.GetString(message),
            new YautjaYoungbloodDeployConfirmedEvent(GetNetEntity(user)));
    }

    private bool TryConsumeStep(EntityUid user, YautjaHuntTeleporterKind kind)
    {
        var now = _timing.CurTime;
        var key = (user, kind);
        if (_nextStepAt.TryGetValue(key, out var next) && now < next)
            return false;

        _nextStepAt[key] = now + StepDuplicateWindow;
        return true;
    }

    private void Teleport(Entity<YautjaHuntTeleporterComponent> ent, EntityUid user)
    {
        if (!TryGetDestination(ent.Comp, out var destination))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-teleporter-no-destination"), user, user, PopupType.SmallCaution);
            return;
        }

        var coordinates = _transform.GetMapCoordinates(destination);
        _teleport.TeleportTrain(user, coordinates);
        _audio.PlayPvs(TeleportSound, user);
    }

    public static bool CanUse(
        YautjaHuntTeleporterKind kind,
        bool yautja,
        bool youngblood,
        bool techAuthorized)
    {
        return kind switch
        {
            YautjaHuntTeleporterKind.Ship => (yautja || techAuthorized) && !youngblood,
            YautjaHuntTeleporterKind.Young => yautja || techAuthorized,
            _ => false,
        };
    }

    private bool CanUseTeleporter(EntityUid user, YautjaHuntTeleporterComponent teleporter, bool popup)
    {
        var yautja = HasComp<YautjaComponent>(user);
        var youngblood = HasComp<YautjaYoungbloodComponent>(user);
        var techAuthorized = HasComp<YautjaTechAuthorizedComponent>(user);

        if (CanUse(teleporter.Kind, yautja, youngblood, techAuthorized))
            return true;

        if (popup)
        {
            var message = teleporter.Kind == YautjaHuntTeleporterKind.Ship && yautja && youngblood
                ? "cmu-yautja-hunt-teleporter-young-denied"
                : "cmu-yautja-hunt-teleporter-denied";

            _popup.PopupEntity(Loc.GetString(message), user, user, PopupType.SmallCaution);
        }

        return false;
    }

    private bool TryGetDestination(YautjaHuntTeleporterComponent teleporter, out EntityUid destination)
    {
        destination = default;
        var query = EntityQueryEnumerator<YautjaHuntTeleportDestinationComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (Deleted(uid) || component.Kind != teleporter.Kind)
                continue;

            if (teleporter.DestinationId != null && !string.Equals(component.Id, teleporter.DestinationId, StringComparison.OrdinalIgnoreCase))
                continue;

            var coordinates = _transform.GetMapCoordinates(uid);
            if (coordinates.MapId == MapId.Nullspace)
                continue;

            destination = uid;
            return true;
        }

        return false;
    }
}
