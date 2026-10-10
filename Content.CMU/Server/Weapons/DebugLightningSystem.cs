using System.Numerics;
using Content.Server.Lightning;
using Content.Shared._RMC14.CameraShake;
using Content.Shared.CMU14.Weapons;
using Content.Shared.Interaction;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Weapons;

public sealed class DebugLightningSystem : EntitySystem
{
    // Invisible point entity the beam system already uses. TimedDespawn self-cleans it.
    private static readonly EntProtoId AnchorProto = "VirtualBeamEntityController";

    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
        => SubscribeLocalEvent<DebugLightningComponent, BeforeRangedInteractEvent>(OnBeforeInteract);

    private void OnBeforeInteract(Entity<DebugLightningComponent> ent, ref BeforeRangedInteractEvent args)
    {
        var click = _transform.ToMapCoordinates(args.ClickLocation);
        var sky = Spawn(AnchorProto, click.Offset(new Vector2(0, ent.Comp.SkyOffset)));
        var ground = Spawn(AnchorProto, click);
        _lightning.ShootLightning(sky, ground, ent.Comp.LightningPrototype);
        _audio.PlayGlobal(_audio.ResolveSound(ent.Comp.ThunderSound), Filter.BroadcastMap(click.MapId), true);

        foreach (var receiver in _lookup.GetEntitiesInRange<ActorComponent>(click, ent.Comp.ScreenShakeRange))
            _cameraShake.ShakeCamera(receiver, ent.Comp.ScreenShakeShakes, ent.Comp.ScreenShakeStrength);

        args.Handled = true;
    }
}
