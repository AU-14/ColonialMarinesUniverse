// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// Whoever lands from a fall on a place where stakes stand is run through: the damage of the stakes (for a fall of one level) grows with the height of the fall, and it is dealt to
// the whole body, every part of it wounded, as a blast is (see CMUExplosionMedicalTraumaSystem).
public sealed class FallSpikesSystem : EntitySystem
{
    [Dependency] private readonly CMUMedicalBodyIndexSystem _bodyIndex = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodyPartHealthSystem _bodyPartHealth = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier StabSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    private static readonly DamageImpact StakeImpact = new(DamageImpactDelivery.Environment, DamageImpactContact.Stab, DamageImpactPenetration.High, DamageImpactEnergy.High);

    private readonly HashSet<Entity<FallSpikesComponent>> _near = new();

    public override void Initialize()
    {
        base.Initialize();

        // On the living: the fall damage of the levels listens to the same event by the damageable component, and one component takes one handler of a system only.
        SubscribeLocalEvent<MobStateComponent, CMUZLevelHitEvent>(OnHit);
    }

    // What the stakes do to one who lands on them with this impact: the damage of the base fall, by how much harder the fall was (the damage of a fall grows as the square of the
    // impact, which is the height of it).
    public DamageSpecifier DamageFor(FallSpikesComponent spikes, float impact)
    {
        var multiplier = Math.Clamp(impact * impact / (spikes.BaseImpact * spikes.BaseImpact), 0f, spikes.MaxMultiplier);
        return spikes.Damage * multiplier;
    }

    private void OnHit(Entity<MobStateComponent> victim, ref CMUZLevelHitEvent args)
    {
        var coords = _transform.GetMapCoordinates(victim);
        _near.Clear();
        _lookup.GetEntitiesInRange(coords, 0.6f, _near);

        foreach (var spikes in _near)
        {
            var distance = (_transform.GetMapCoordinates(spikes).Position - coords.Position).Length();
            if (distance > spikes.Comp.Range)
                continue;

            Impale(spikes, victim.Owner, args.ImpactPower);
            return;
        }
    }

    // The stakes run the one who fell through: the damage of the fall onto them, dealt to the body and to every part of it.
    public void Impale(Entity<FallSpikesComponent> spikes, EntityUid victim, float impact)
    {
        var damage = DamageFor(spikes.Comp, impact);
        if (damage.GetTotal() <= 0)
            return;

        _damage.TryChangeDamage(victim, damage, ignoreResistances: true, origin: spikes.Owner);

        // Every part of the body takes its share, and the wounds come from it. A thing that has no body of parts takes the damage as it is.
        var parts = new List<Entity<BodyPartComponent>>();
        foreach (var part in _bodyIndex.GetBodyParts(victim))
        {
            if (HasComp<BodyPartHealthComponent>(part))
                parts.Add(part);
        }

        if (parts.Count > 0)
        {
            var share = damage * (1f / parts.Count);
            foreach (var part in parts)
            {
                _bodyPartHealth.TrackBodyDamage(victim, part, share);
                _bodyPartHealth.TryApplyPartDamage(victim, part, share, origin: spikes.Owner, impact: StakeImpact,
                    mechanism: CMUTraumaMechanism.Pierce, ignoreResistance: true);
            }
        }

        _audio.PlayPvs(StabSound, victim);
        _popup.PopupEntity(Loc.GetString("au14-fall-spikes-impaled"), victim, victim, PopupType.LargeCaution);
    }
}
