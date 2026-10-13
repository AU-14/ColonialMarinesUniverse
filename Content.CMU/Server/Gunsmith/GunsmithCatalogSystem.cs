// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Gunsmith;

/// <summary>
/// The link between the calibers of the benches and the weapons, the magazines and the cartridges of the game. A weapon, a magazine or a cartridge of the
/// game takes a caliber from the preset whose tags it has (the tags the weapons of the game ask for); a cartridge or a magazine made for a caliber gets
/// the tags of its preset, and so fits the magazines and the weapons of the game that take that caliber.
/// </summary>
public sealed class GunsmithCatalogSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    private PrototypeGunsmithData _data = default!;

    public override void Initialize()
    {
        base.Initialize();

        _data = new PrototypeGunsmithData(_prototypes);
    }

    /// <summary>The preset with the signature of the caliber, if the bench has one.</summary>
    public GunsmithCaliberPrototype? PresetByKey(string key) => _data.CaliberByKey(key);

    public static CaliberSpec SpecOf(GunsmithCaliberPrototype preset) => new()
    {
        BoreMm = preset.BoreMm,
        CaseDiameterMm = preset.CaseDiameterMm,
        CaseLengthMm = preset.CaseLengthMm,
        SeatDepthMm = preset.SeatDepthMm,
        BulletMassG = preset.BulletMassG,
        Powder = preset.Powder,
        ChargeG = preset.ChargeG,
    };

    /// <summary>Whether the thing is one the benches can read a caliber from, or at least try: a weapon, a magazine, a cartridge.</summary>
    public bool IsSource(EntityUid uid) =>
        HasComp<CraftedFirearmComponent>(uid) || HasComp<CraftedMagazineComponent>(uid) || HasComp<CraftedCartridgeComponent>(uid)
        || HasComp<GunComponent>(uid) || HasComp<BallisticAmmoProviderComponent>(uid) || HasComp<CartridgeAmmoComponent>(uid);

    /// <summary>The caliber of a weapon, a magazine or a cartridge: of ours, by what it was made for; of the game, by the tags it has or asks for.</summary>
    public bool TryGetCaliber(EntityUid item, out CaliberSpec caliber)
    {
        caliber = default!;

        if (TryComp<CraftedFirearmComponent>(item, out var gun))
        {
            caliber = gun.Design.Caliber.Clone();
            return true;
        }

        if (TryComp<CraftedMagazineComponent>(item, out var magazine))
        {
            caliber = magazine.Caliber.Clone();
            return true;
        }

        if (TryComp<CraftedCartridgeComponent>(item, out var cartridge))
        {
            caliber = cartridge.Load.Clone();
            return true;
        }

        var preset = PresetOfGameItem(item);
        if (preset == null)
            return false;

        caliber = SpecOf(preset);
        return true;
    }

    private GunsmithCaliberPrototype? PresetOfGameItem(EntityUid item)
    {
        var cartridgeTags = new HashSet<string>();
        var magazineTags = new HashSet<string>();

        // A weapon asks for the tags of its magazines, or of its cartridges (a shotgun, a revolver).
        if (HasComp<ItemSlotsComponent>(item) && _slots.TryGetSlot(item, "gun_magazine", out var slot) && slot.Whitelist?.Tags is { } slotTags)
            magazineTags.UnionWith(slotTags.Select(t => t.Id));

        if (TryComp<BallisticAmmoProviderComponent>(item, out var ballistic) && ballistic.Whitelist?.Tags is { } ballisticTags)
            cartridgeTags.UnionWith(ballisticTags.Select(t => t.Id));

        if (TryComp<RevolverAmmoProviderComponent>(item, out var revolver) && revolver.Whitelist?.Tags is { } revolverTags)
            cartridgeTags.UnionWith(revolverTags.Select(t => t.Id));

        GunsmithCaliberPrototype? best = null;
        var bestScore = 0;
        foreach (var preset in _prototypes.EnumeratePrototypes<GunsmithCaliberPrototype>())
        {
            var score = 0;
            foreach (var tag in preset.CartridgeTags)
            {
                if (cartridgeTags.Contains(tag) || HasTag(item, tag))
                    score++;
            }

            foreach (var tag in preset.MagazineTags)
            {
                if (magazineTags.Contains(tag) || HasTag(item, tag))
                    score++;
            }

            if (score > bestScore)
            {
                best = preset;
                bestScore = score;
            }
        }

        return best;
    }

    private bool HasTag(EntityUid item, string tag) =>
        _prototypes.HasIndex<TagPrototype>(tag) && _tags.HasTag(item, new ProtoId<TagPrototype>(tag));

    /// <summary>The tags the cartridges of the game of the caliber have (empty if the caliber is not one of the presets).</summary>
    public List<ProtoId<TagPrototype>> CartridgeTagsOf(string caliberKey)
    {
        var tags = new List<ProtoId<TagPrototype>>();
        if (PresetByKey(caliberKey) is { } preset)
            tags.AddRange(preset.CartridgeTags.Where(t => _prototypes.HasIndex<TagPrototype>(t)).Select(t => new ProtoId<TagPrototype>(t)));

        return tags;
    }

    /// <summary>
    /// Whether a cartridge fits a weapon or a magazine made for the caliber: a cartridge made at the bench by its signature, a cartridge of the game
    /// by the caliber of the preset whose tags it has.
    /// </summary>
    public bool FitsCaliber(EntityUid cartridge, string caliberKey)
    {
        if (TryComp<CraftedCartridgeComponent>(cartridge, out var crafted))
            return crafted.CaliberKey == caliberKey;

        if (!HasComp<CartridgeAmmoComponent>(cartridge))
            return false;

        var preset = PresetOfGameItem(cartridge);
        if (preset == null)
            return false;

        return SpecOf(preset).Key() == caliberKey;
    }

    /// <summary>Gives a cartridge made for the caliber the tags of the cartridges of the game of that caliber.</summary>
    public void TagCartridge(EntityUid cartridge, string caliberKey)
    {
        if (PresetByKey(caliberKey) is { } preset)
            AddTags(cartridge, preset.CartridgeTags);
    }

    /// <summary>Gives a magazine made for the caliber the tags of the magazines of the game that the weapons of that caliber take.</summary>
    public void TagMagazine(EntityUid magazine, string caliberKey)
    {
        if (PresetByKey(caliberKey) is { } preset)
            AddTags(magazine, preset.MagazineTags);
    }

    private void AddTags(EntityUid uid, IEnumerable<string> tags)
    {
        var known = tags.Where(t => _prototypes.HasIndex<TagPrototype>(t)).Select(t => new ProtoId<TagPrototype>(t)).ToList();
        if (known.Count > 0)
            _tags.TryAddTags(uid, known);
    }
}
