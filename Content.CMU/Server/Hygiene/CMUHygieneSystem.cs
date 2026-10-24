using Content.Server.Power.Components;
using Content.Server._RMC14.Explosion;
using Content.Shared.Audio;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Systems;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Projectiles;
using Content.Shared.Storage;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._RMC14.Xenonids.Spray;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;

    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [Dependency] private SharedAudioSystem _audio = default!;

    [Dependency] private InventorySystem _inventory = default!;

    [Dependency] private EntityLookupSystem _lookup = default!;

    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private CMUResidueSystem _residue = default!;

    [Dependency] private CMUItemStainSystem _stains = default!;

    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color AlienColor = Color.FromHex("#b3c942");
    private static readonly SoundSpecifier WaterSound = new SoundPathSpecifier("/Audio/Ambience/Objects/drain.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUShowerComponent, MapInitEvent>(OnShowerInit);
        SubscribeLocalEvent<CMUShowerComponent, ActivateInWorldEvent>(OnShowerActivate);
        SubscribeLocalEvent<CMUShowerComponent, CMUShowerToggleRequestEvent>(OnShowerToggleRequested);
        SubscribeLocalEvent<CMUShowerComponent, ExaminedEvent>(OnShowerExamine);
        SubscribeLocalEvent<CMULaundryHeatedGrenadeComponent, ActiveTimerTriggerEvent>(OnLaundryVocalize,
            before: new[] { typeof(RMCExplosionSystem) });
        SubscribeLocalEvent<CMULaundryHeatedGrenadeComponent, CMUExplosionPreparingEvent>(OnLaundryExplosion);
        SubscribeLocalEvent<CMULaundryHeatedGrenadeComponent, CMClusterSpawnedEvent>(OnLaundryFragments);
        SubscribeLocalEvent<CMULaundryPopcornComponent, ComponentShutdown>(OnPopcornShutdown);
        SubscribeLocalEvent<CMULaundryPopcornComponent, MapInitEvent>(OnPopcornMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<CMULaundryComponent, MapInitEvent>(OnLaundryInit);
        SubscribeLocalEvent<CMULaundryComponent, PowerChangedEvent>(OnLaundryPower);
        SubscribeLocalEvent<CMULaundryComponent, SignalReceivedEvent>(OnLaundrySignal);
        SubscribeLocalEvent<CMULaundryComponent, DamageChangedEvent>(OnLaundryDamage);
        SubscribeLocalEvent<CMULaundryComponent, GetVerbsEvent<AlternativeVerb>>(OnLaundryVerbs);
        SubscribeLocalEvent<CMULaundryComponent, ExaminedEvent>(OnLaundryExamine);
        SubscribeLocalEvent<CMULaundryComponent, ContainerIsInsertingAttemptEvent>(OnInsert);
        SubscribeLocalEvent<CMULaundryComponent, ContainerIsRemovingAttemptEvent>(OnRemove);
        SubscribeLocalEvent<XenoAcidProjectileComponent, ProjectileHitEvent>(OnAcidHit);
        SubscribeLocalEvent<SprayAcidedComponent, ComponentStartup>(OnAcidSpray);
    }

    private void OnAcidHit(Entity<XenoAcidProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!args.Handled && args.Damage.GetTotal() > 0)
            ApplyAlienResidue(args.Target);
    }

    private void OnAcidSpray(Entity<SprayAcidedComponent> ent, ref ComponentStartup args)
    {
        ApplyAlienResidue(ent);
    }

    public void ApplyAlienResidue(EntityUid target)
    {
        _stains.StainExposedEquipment(target, CMUItemStainKind.Blood, AlienColor);
        _residue.Stain(target, AlienColor, alien: true);
    }

    public override void Update(float frameTime)
    {
        UpdateLaundryPopcorn();
        UpdateShowers(frameTime);
        UpdateLaundry();
        UpdateLaundryCookoff();
    }
}
