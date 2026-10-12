using Content.Shared._RMC14.Dialog;
using Content.Shared.Body.Part;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Riding;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Riding;

/// <summary>
///     Server half of shoulder riding: the climb on/off verbs, the consent dialog shown to the mount, and dismounting
///     on leg loss and disconnects. The riding rules themselves live in <see cref="CMUMountSystem"/>.
/// </summary>
public sealed partial class CMUMountConsentSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private CMUMountSystem _mount = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SpriteSpecifier ClimbOnIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/buckle.svg.192dpi.png"));

    private static readonly SpriteSpecifier ClimbOffIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/unbuckle.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUMountComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CMUMountComponent, CMUMountConsentEvent>(OnConsent);
        SubscribeLocalEvent<CMUMountComponent, PlayerDetachedEvent>(OnMountPlayerDetached);
        SubscribeLocalEvent<CMURiderComponent, PlayerDetachedEvent>(OnRiderPlayerDetached);

        // Raised on the severed part with broadcast: true by BodyPartSeveranceSystem.
        SubscribeLocalEvent<BodyPartSeveredEvent>(OnBodyPartSevered);
    }

    private void OnGetAlternativeVerbs(EntityUid uid, CMUMountComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == uid)
            return;

        var user = args.User;
        if (_mount.IsRiding(user, uid))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-mount-verb-climb-off"),
                Icon = ClimbOffIcon,
                Act = () => _buckle.TryUnbuckle(user, user),
            });
            return;
        }

        if (!_mount.CanMount((uid, comp), user))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-mount-verb-climb-on"),
            Icon = ClimbOnIcon,
            Act = () => RequestMount(uid, user),
        });
    }

    /// <summary>Asks the mount for consent. The rider climbs on only if the mount accepts in time.</summary>
    public bool RequestMount(EntityUid mount, EntityUid rider)
    {
        if (!TryComp(mount, out CMUMountComponent? comp) || !_mount.CanMount((mount, comp), rider, popup: true))
            return false;

        if (!_mount.TryBeginRequest((mount, comp), rider))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mount-request-busy", ("mount", mount)), mount, rider);
            return false;
        }

        var netMount = GetNetEntity(mount);
        var netRider = GetNetEntity(rider);
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-mount-consent-accept"), new CMUMountConsentEvent(netMount, netRider, true)),
            new(Loc.GetString("cmu-mount-consent-decline"), new CMUMountConsentEvent(netMount, netRider, false)),
        };

        _dialog.OpenOptions(mount,
            mount,
            Loc.GetString("cmu-mount-consent-title"),
            options,
            Loc.GetString("cmu-mount-consent-message", ("rider", Identity.Entity(rider, EntityManager))),
            new CMUMountConsentEvent(netMount, netRider, false),
            comp.ConsentTimeout);

        _popup.PopupEntity(Loc.GetString("cmu-mount-request-sent", ("mount", mount)), mount, rider);
        return true;
    }

    private void OnConsent(EntityUid uid, CMUMountComponent comp, CMUMountConsentEvent args)
    {
        // Option events arrive directed and broadcast, the timeout/close cancel directed only: this sees each once.
        if (!TryGetEntity(args.Mount, out var mount) || mount != uid ||
            !TryGetEntity(args.Rider, out var riderUid) || riderUid is not { } rider)
        {
            return;
        }

        if (!_mount.TryEndRequest((uid, comp), rider))
            return;

        if (!args.Accept)
        {
            _popup.PopupEntity(Loc.GetString("cmu-mount-declined", ("mount", uid)), uid, rider, PopupType.SmallCaution);
            return;
        }

        // Re-checks eligibility: things may have changed while the mount was deciding.
        _mount.TryMount(uid, rider);
    }

    private void OnBodyPartSevered(ref BodyPartSeveredEvent args)
    {
        if (args.Type != BodyPartType.Leg || !HasComp<CMURiderComponent>(args.Body))
            return;

        _mount.Dismount(args.Body);
    }

    private void OnMountPlayerDetached(EntityUid uid, CMUMountComponent comp, PlayerDetachedEvent args)
    {
        _mount.DismountAll((uid, comp), knockdown: false);
    }

    private void OnRiderPlayerDetached(EntityUid uid, CMURiderComponent comp, PlayerDetachedEvent args)
    {
        _mount.Dismount(uid, knockdown: false);
    }
}
