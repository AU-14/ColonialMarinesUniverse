using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.LowAmmoSound;

/// <summary>
///     Ported from cmss13's fire_rattle: once the loaded ammo drops to <see cref="Fraction"/> or below, the gun fires
///     with <see cref="Sound"/> instead of its normal gunshot, like a magazine rattling near empty.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMULowAmmoFireSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public float Fraction = 0.25f;

    [DataField, AutoNetworkedField]
    public bool Low;
}

public sealed class CMULowAmmoFireSoundSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULowAmmoFireSoundComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<CMULowAmmoFireSoundComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<CMULowAmmoFireSoundComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CMULowAmmoFireSoundComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    private void OnRefreshModifiers(Entity<CMULowAmmoFireSoundComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.Low)
            args.SoundGunshot = ent.Comp.Sound;
    }

    private void OnShot(Entity<CMULowAmmoFireSoundComponent> ent, ref GunShotEvent args)
    {
        Update(ent);
    }

    private void OnContainerChanged<T>(Entity<CMULowAmmoFireSoundComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        Update(ent);
    }

    private void Update(Entity<CMULowAmmoFireSoundComponent> ent)
    {
        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(ent, ref ev);

        var low = ev.Capacity > 0 && ev.Count <= ev.Capacity * ent.Comp.Fraction;
        if (low == ent.Comp.Low)
            return;

        ent.Comp.Low = low;
        Dirty(ent);
        _gun.RefreshModifiers(ent.Owner);
    }
}
