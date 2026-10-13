// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Chemistry.Components;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server.CMU14.Gunsmith;

/// <summary>
/// What a cartridge made at the ammunition bench does to the bullet it fires: it marks the bullet for the weapon to work the damage out (the energy
/// depends on the barrel), and gives it what the liquid in the core does: it burns, or it injects what it carries into whoever it hits.
/// </summary>
public sealed class CraftedCartridgeSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private RMCReagentSystem _reagent = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private GunsmithCatalogSystem _catalog = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CraftedCartridgeComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<CraftedCartridgeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CraftedCartridgeComponent, StackMergeAttemptEvent>(OnMergeAttempt);
        SubscribeLocalEvent<CraftedCartridgeComponent, StackSplitEvent>(OnSplit);
        SubscribeLocalEvent<CraftedCartridgeComponent, StackOneTakenEvent>(OnOneTaken);
    }

    /// <summary>Cartridges stack with the ones that are made the same, and with no others.</summary>
    private void OnMergeAttempt(Entity<CraftedCartridgeComponent> ent, ref StackMergeAttemptEvent args)
    {
        if (!TryComp<CraftedCartridgeComponent>(args.Donor, out var donor) || donor.Signature != ent.Comp.Signature)
            args.Cancelled = true;
    }

    /// <summary>A cartridge taken from a stack is made the same as the stack: the stack gives the new one what it has.</summary>
    private void OnSplit(Entity<CraftedCartridgeComponent> ent, ref StackSplitEvent args) => GiveWhatItHas(ent, args.NewId);

    private void OnOneTaken(Entity<CraftedCartridgeComponent> ent, ref StackOneTakenEvent args) => GiveWhatItHas(ent, args.NewId);

    private void GiveWhatItHas(Entity<CraftedCartridgeComponent> ent, EntityUid newId)
    {
        var from = ent.Comp;
        var copy = EnsureComp<CraftedCartridgeComponent>(newId);
        copy.CaliberKey = from.CaliberKey;
        copy.Load = from.Load.Clone();
        copy.DamageMultiplier = from.DamageMultiplier;
        copy.ArmorPierce = from.ArmorPierce;
        copy.Seal = from.Seal;
        copy.Jam = from.Jam;
        copy.Extra = new Dictionary<string, double>(from.Extra);
        copy.PayloadReagent = from.PayloadReagent;
        copy.PayloadUnits = from.PayloadUnits;
        copy.InjectUnits = from.InjectUnits;
        copy.Ignite = from.Ignite;
        copy.PayloadDamageType = from.PayloadDamageType;
        copy.PayloadDamagePerUnit = from.PayloadDamagePerUnit;
        copy.Pellets = from.Pellets;
        copy.RatedDamage = from.RatedDamage;
        copy.GameDamage = from.GameDamage;
        copy.GameMaxDamage = from.GameMaxDamage;
        copy.BaselineEnergyJ = from.BaselineEnergyJ;
        copy.NominalPellets = from.NominalPellets;
        copy.Signature = from.Signature;
        copy.Kind = from.Kind;
        Dirty(newId, copy);

        _catalog.TagCartridge(newId, from.CaliberKey);
        var meta = MetaData(ent);
        _meta.SetEntityName(newId, meta.EntityName);
        _meta.SetEntityDescription(newId, meta.EntityDescription);
    }

    private void OnShot(Entity<CraftedCartridgeComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            // The bullets of earlier cartridges of the same shot have their marks already.
            if (HasComp<CraftedProjectileComponent>(projectile))
                continue;

            var marker = AddComp<CraftedProjectileComponent>(projectile);
            marker.From = ent;

            // In a weapon of the game the bullet does what the cartridge does in the barrel the caliber is rated in; a weapon made at the bench
            // works the damage out again for its own barrel (see CraftedFirearmSystem), which comes after this.
            if (TryComp<ProjectileComponent>(projectile, out var component))
            {
                var damage = new DamageSpecifier();
                foreach (var (type, amount) in AmmoMechanics.ProjectileDamage(ent.Comp.RatedDamage, ent.Comp.Extra, ent.Comp.PayloadDamageType,
                             ent.Comp.PayloadDamagePerUnit, ent.Comp.PayloadUnits, ent.Comp.Pellets))
                {
                    damage.DamageDict[type] = FixedPoint2.New((float) amount);
                }

                component.Damage = damage;
            }

            // A person is set alight by the fire of the fuel that the bullet carries: how hot, how long and what color are the fuel's own.
            if (ent.Comp.Ignite)
            {
                var intensity = 30;
                var duration = 20;
                var color = Color.FromHex("#EE6515");
                if (_reagent.TryIndex(ent.Comp.PayloadReagent, out var fuel))
                {
                    if (fuel.Intensity > 0)
                        intensity = Math.Clamp(fuel.Intensity, 5, 80);

                    if (fuel.Duration != 0)
                        duration = Math.Clamp(Math.Abs(fuel.Duration), 5, 60);

                    color = fuel.SubstanceColor;
                }

                _flammable.SetProjectileFire(projectile, intensity, duration, color);
            }

            if (ent.Comp.InjectUnits > 0 && !string.IsNullOrEmpty(ent.Comp.PayloadReagent)
                && _solutions.TryGetSolution(projectile, "ammo", out var solution))
            {
                _solutions.TryAddReagent(solution.Value, ent.Comp.PayloadReagent, FixedPoint2.New((float) ent.Comp.InjectUnits / Math.Max(1, ent.Comp.Pellets)));
                var inject = EnsureComp<SolutionInjectOnProjectileHitComponent>(projectile);
                inject.Solution = "ammo";
                inject.TransferAmount = FixedPoint2.New((float) ent.Comp.InjectUnits / Math.Max(1, ent.Comp.Pellets));
            }
        }
    }

    private void OnExamined(Entity<CraftedCartridgeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("ammo-examine", ("key", ent.Comp.CaliberKey)));
        if (!string.IsNullOrEmpty(ent.Comp.PayloadReagent))
            args.PushMarkup(Loc.GetString("ammo-examine-payload"));
    }
}
