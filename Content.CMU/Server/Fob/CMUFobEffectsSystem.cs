using Content.Shared.CMU14.Fob;
using Content.Shared.Destructible;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>Cosmetic feedback is emitted only after the gameplay system accepts an action.</summary>
public sealed partial class CMUFobEffectsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobPresentationComponent, StorageAfterOpenEvent>(OnCrateOpened);
        SubscribeLocalEvent<CMUFobPresentationComponent, StorageAfterCloseEvent>(OnCrateClosed);
        SubscribeLocalEvent<CMUFobEquipmentComponent, DestructionEventArgs>(OnEquipmentDestroyed);
        SubscribeLocalEvent<CMUFobSupportGunComponent, GunMuzzleFlashAttemptEvent>(OnMuzzleFlash);
    }

    private void OnCrateOpened(Entity<CMUFobPresentationComponent> ent, ref StorageAfterOpenEvent args)
    {
        if (!_reserve.Enabled) return;
        _appearance.SetData(ent, CMUFobPresentationVisuals.Open, true);
        SetStatus(ent, CMUFobPresentationStatus.Ready);
        Play(ent, CMUFobPresentationCue.Deploy);
    }

    private void OnCrateClosed(Entity<CMUFobPresentationComponent> ent, ref StorageAfterCloseEvent args)
    {
        if (!_reserve.Enabled) return;
        _appearance.SetData(ent, CMUFobPresentationVisuals.Open, false);
        SetStatus(ent, CMUFobPresentationStatus.Off);
        Play(ent, CMUFobPresentationCue.Pack);
    }

    private void OnMuzzleFlash(Entity<CMUFobSupportGunComponent> ent, ref GunMuzzleFlashAttemptEvent args)
    {
        // Each accepted shot supplies its own barrel-aligned flash or launcher exhaust.
        if (_reserve.Enabled)
            args.Cancelled = true;
    }

    private void OnEquipmentDestroyed(Entity<CMUFobEquipmentComponent> ent, ref DestructionEventArgs args)
        => Play(ent, CMUFobPresentationCue.Break);

    public void SetStatus(EntityUid uid, CMUFobPresentationStatus status)
    {
        if (!_reserve.Enabled || TerminatingOrDeleted(uid) || !TryComp<CMUFobPresentationComponent>(uid, out var presentation))
            return;
        var hadStatus = _appearance.TryGetData<CMUFobPresentationStatus>(uid, CMUFobPresentationVisuals.Status, out var previous);
        if (hadStatus && previous == status)
            return;
        _appearance.SetData(uid, CMUFobPresentationVisuals.Status, status);
        _appearance.SetData(uid, CMUFobPresentationVisuals.StatusSince, _timing.CurTime);
        var sound = status == CMUFobPresentationStatus.Active ? presentation.PowerOnSound
            : previous == CMUFobPresentationStatus.Active ? presentation.PowerOffSound : null;
        if (sound != null)
            _audio.PlayPvs(sound, uid);
    }

    public void Play(EntityUid uid, CMUFobPresentationCue cue)
    {
        if (!_reserve.Enabled || TerminatingOrDeleted(uid) || !TryComp<CMUFobPresentationComponent>(uid, out var presentation))
            return;
        if (cue == CMUFobPresentationCue.Shot)
            presentation.ShotSequence++;
        RaiseNetworkEvent(new CMUFobPresentationEvent(GetNetEntity(uid), cue, presentation.ShotSequence), Filter.Pvs(uid));
        // Gunshots and crate lids already have their own audio. These cues must not double it.
        var sound = cue switch
        {
            CMUFobPresentationCue.Deploy or CMUFobPresentationCue.Pack when !HasComp<EntityStorageComponent>(uid)
                => "/Audio/_RMC14/Machines/hydraulics_1.ogg",
            CMUFobPresentationCue.Repair or CMUFobPresentationCue.Charge => "/Audio/Machines/twobeep.ogg",
            CMUFobPresentationCue.Reload => "/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg",
            CMUFobPresentationCue.Shock => "/Audio/Effects/sparks4.ogg",
            _ => null,
        };
        if (sound != null)
            _audio.PlayPvs(new SoundPathSpecifier(sound, AudioParams.Default.WithVolume(-5)), uid);
        if (cue is CMUFobPresentationCue.Shock or CMUFobPresentationCue.Break)
            SpawnAtPosition("CMUFobElectricalEffect", Transform(uid).Coordinates);
        if (cue == CMUFobPresentationCue.Repair)
            SpawnAtPosition("CMUFobRepairEffect", Transform(uid).Coordinates);
    }
}
