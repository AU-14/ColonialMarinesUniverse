using Content.Shared._RMC14.Flag;
using Content.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flag;

public sealed partial class CMUFlagAuraSystem
{
    [Dependency] private FixtureSystem _fixture = default!;

    /// <summary>
    ///     The hitbox a planted carriable flag is shot through. Only aimed shots hit it (RequireProjectileTarget).
    /// </summary>
    private const string CarriableFlagHitbox = "deploy";

    /// <summary>
    ///     JIMA extended flag stats, used while a carriable flag is held.
    /// </summary>
    private static readonly CMUFlagAuraStats CarriedFlagAura = new()
    {
        Range = 8.466f,
    };

    /// <summary>
    ///     JIMA warbanner stats, used while a carriable flag is planted.
    /// </summary>
    private static readonly CMUFlagAuraStats PlantedFlagAura = new()
    {
        ApplyMove = true,
        Duration = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    ///     The upstream UN flags, which only rally the UN's reinforcements.
    /// </summary>
    private static readonly HashSet<EntProtoId> UnitedNationsFlags = ["RMCFlagCarriableUN", "RMCFlagCarriableUNMC"];

    private static readonly ProtoId<CMUFlagReinforcementsPrototype> UnitedNationsReinforcements =
        "CMUFlagReinforcementsUN";

    private void InitializeCarriedFlag()
    {
        SubscribeLocalEvent<PlantableFlagComponent, MapInitEvent>(OnPlantableFlagMapInit);
    }

    /// <summary>
    ///     Every upstream carriable flag rallies its bearer's side, except decorative ones; prototypes may still set
    ///     their own stats.
    /// </summary>
    private void OnPlantableFlagMapInit(Entity<PlantableFlagComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        if (TryComp(ent, out PhysicsComponent? body))
        {
            _fixture.TryCreateFixture(ent, new PhysShapeCircle(0.45f), CarriableFlagHitbox, density: 20, hard: false,
                collisionLayer: (int) CollisionGroup.BulletImpassable, body: body);
        }

        if (HasComp<CMUDecorativeFlagComponent>(ent))
            return;

        var aura = EnsureComp<CMUFlagAuraComponent>(ent);
        aura.Carried ??= CarriedFlagAura;
        aura.Planted ??= PlantedFlagAura;

        if (MetaData(ent).EntityPrototype?.ID is { } id && UnitedNationsFlags.Contains(id))
            EnsureComp<CMUFlagAllegianceComponent>(ent).Reinforcements ??= UnitedNationsReinforcements;
    }
}
