using Content.Shared._RMC14.Emplacements;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUDoorGunVisualSystem
{
    [Dependency] private SharedPointLightSystem _lights = default!;

    private static readonly EntProtoId BarrelLightPrototype = "CMUDoorGunBarrelLight";

    private void InitializeBarrelLight()
    {
        SubscribeLocalEvent<CMUDoorGunVisualComponent, ComponentShutdown>(OnGunVisualShutdown);
        SubscribeLocalEvent<CMUDoorGunLightComponent, ComponentShutdown>(OnGunLightShutdown);
    }

    private void OnGunVisualShutdown(Entity<CMUDoorGunVisualComponent> ent, ref ComponentShutdown args)
        => RemComp<CMUDoorGunLightComponent>(ent);

    private void OnGunLightShutdown(Entity<CMUDoorGunLightComponent> ent, ref ComponentShutdown args)
        => ClearBarrelLight(ent.Comp);

    private void ClearBarrelLight(CMUDoorGunLightComponent visuals)
    {
        if (visuals.Light is { } light && !TerminatingOrDeleted(light))
        {
            _lights.SetEnabled(light, false);
            QueueDel(light);
        }
        visuals.Light = null;
    }

    private void UpdateBarrelLight(EntityUid gun, CMUDoorGunVisualComponent visual,
        WeaponMountComponent mount, Angle aim)
    {
        var lamp = EnsureComp<CMUDoorGunLightComponent>(gun);
        if (mount.MountedEntity == null || (MetaData(gun).Flags & MetaDataFlags.Detached) != 0)
        {
            ClearBarrelLight(lamp);
            return;
        }

        // This child rotates with the aimed barrel; the mount itself stays fixed
        // so its firing arc and the tripod's direction remain stable.
        var coordinates = new EntityCoordinates(gun, aim.ToWorldVec() * visual.BarrelLightDistance);
        if (lamp.Light is not { } light || TerminatingOrDeleted(light))
        {
            light = SpawnAttachedTo(BarrelLightPrototype, coordinates);
            lamp.Light = light;
        }
        else
            _transform.SetCoordinates(light, coordinates);

        _transform.SetLocalRotation(light, aim);
    }
}
