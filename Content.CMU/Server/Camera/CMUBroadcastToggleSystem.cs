using Content.Server.Camera;
using Content.Server.CMU14.Camera.Television;
using Content.Shared.Camera;
using Content.Shared.CMU14.Camera;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Camera;

/// <summary>
/// Allows players to toggle a broadcasting camera's television feed.
/// </summary>
public sealed partial class CMUBroadcastToggleSystem : EntitySystem
{
    private static readonly ProtoId<CameraNetworkPrototype> TelevisionNetwork = "RMCTelevision";

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CameraNetworkSystem _networks = default!;
    [Dependency] private CMUTelevisionSystem _television = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBroadcastToggleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUBroadcastToggleComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnStartup(Entity<CMUBroadcastToggleComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent, out CameraNetworkMemberComponent? member))
            _appearance.SetData(ent, CMUBroadcastVisuals.Broadcasting, member.Networks.Contains(TelevisionNetwork));
    }

    private void OnGetVerbs(Entity<CMUBroadcastToggleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || !TryComp(ent, out CameraNetworkMemberComponent? member))
        {
            return;
        }

        var broadcasting = member.Networks.Contains(TelevisionNetwork);

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(broadcasting
                ? "cmu-camera-stop-broadcast"
                : "cmu-camera-start-broadcast"),
            Act = () => SetBroadcasting(ent, !broadcasting),
        });
    }

    private void SetBroadcasting(EntityUid camera, bool broadcasting)
    {
        var changed = broadcasting
            ? _networks.AddNetwork(camera, TelevisionNetwork)
            : _networks.RemoveNetwork(camera, TelevisionNetwork);
        if (!changed)
            return;

        _appearance.SetData(camera, CMUBroadcastVisuals.Broadcasting, broadcasting);
        if (broadcasting)
            _television.RefreshTunedViewers(camera);
    }
}
