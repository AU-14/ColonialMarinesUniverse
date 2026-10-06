using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.util;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ForceOnForce;

public sealed partial class ForceOnForceUniformPolicySystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IComponentFactory _factory = default!;

    public override void Initialize() => SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn);

    private void OnSpawn(PlayerSpawnCompleteEvent args)
    {
        if (_ticker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) != true ||
            !TryComp<MarineComponent>(args.Mob, out var marine)) return;
        var platoon = marine.Faction?.ToLowerInvariant() switch
        {
            "govfor" => _platoons.SelectedGovforPlatoon,
            "opfor" => _platoons.SelectedOpforPlatoon,
            _ => null,
        };
        if (platoon == null) return;
        var policy = EnsureComp<ForceOnForceUniformComponent>(args.Mob);
        policy.Uniforms.Clear();
        if (_prototypes.TryIndex<ForceOnForceUniformPrototype>(platoon.ID, out var whitelist))
            policy.Uniforms.UnionWith(whitelist.Uniforms);
        // Use the same resolved vendors as the ship, including specialist and shipside stock.
        // Vendor updates must not require maintaining a second list of accepted uniforms.
        var vendors = new HashSet<EntProtoId>();
        foreach (var marker in Enum.GetValues<PlatoonMarkerClass>())
        {
            if (!_platoons.TryResolvePlatoonVendor(platoon, marker, out var vendorId) ||
                !vendors.Add(vendorId) ||
                !_prototypes.TryIndex(vendorId, out var vendorPrototype) ||
                !vendorPrototype.TryComp<CMAutomatedVendorComponent>(out var vendor, _factory))
            {
                continue;
            }

            foreach (var section in vendor.Sections)
            foreach (var entry in section.Entries)
            {
                AddUniform(entry.Id, policy);
                foreach (var linked in entry.LinkedEntries)
                    AddUniform(linked, policy);
            }
        }

        // Issued specialist uniforms are legitimate even when shared between platoons.
        if (_inventory.TryGetSlotEntity(args.Mob, "jumpsuit", out var uniform) &&
            MetaData(uniform.Value).EntityPrototype is { } prototype)
            policy.Uniforms.Add(prototype.ID);
        Dirty(args.Mob, policy);
    }

    private void AddUniform(EntProtoId id, ForceOnForceUniformComponent policy)
    {
        if (_prototypes.TryIndex(id, out var prototype) &&
            prototype.TryComp<ClothingComponent>(out var clothing, _factory) &&
            (clothing.Slots & SlotFlags.INNERCLOTHING) != 0)
        {
            policy.Uniforms.Add(id);
        }
    }
}
