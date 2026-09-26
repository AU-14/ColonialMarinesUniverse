using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.ForceOnForce;
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
        // Issued specialist uniforms are legitimate even when shared between platoons.
        if (_inventory.TryGetSlotEntity(args.Mob, "jumpsuit", out var uniform) &&
            MetaData(uniform.Value).EntityPrototype is { } prototype)
            policy.Uniforms.Add(prototype.ID);
        Dirty(args.Mob, policy);
    }
}
