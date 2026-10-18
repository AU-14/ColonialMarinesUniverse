// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the farming of crystall-punk-14 (MIT / CC-BY-SA) and adapted to this project.
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Farming;

// The backbone of any plant: the energy and the resource it spends to grow, and how far it has grown.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState(true), Access(typeof(SharedFarmingSystem))]
public sealed partial class FarmPlantComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Energy = 30f;

    [DataField, AutoNetworkedField]
    public float EnergyMax = 100f;

    // What it consumes to grow (the water it drinks gives it).
    [DataField, AutoNetworkedField]
    public float Resource = 30f;

    [DataField, AutoNetworkedField]
    public float ResourceMax = 100f;

    // From 0 to 1.
    [DataField, AutoNetworkedField]
    public float GrowthLevel = 0f;

    [DataField]
    public float UpdateFrequency = 60f;

    [DataField, AutoPausedField]
    public TimeSpan NextUpdateTime = TimeSpan.Zero;

    // The solution of the plant that it drinks from.
    [DataField]
    public string? Solution;
}

// Raised on the plant every now and then, at random: whoever listens adds what the plant gets or loses.
public sealed class FarmPlantUpdateEvent(Entity<FarmPlantComponent> comp) : EntityEventArgs
{
    public readonly Entity<FarmPlantComponent> Plant = comp;
    public float EnergyDelta = 0f;
    public float ResourceDelta = 0f;
}

// Raised after that one, when the changes are made: the growth is paid for here.
public sealed class FarmAfterPlantUpdateEvent(Entity<FarmPlantComponent> comp) : EntityEventArgs
{
    public readonly Entity<FarmPlantComponent> Plant = comp;
}

// A seed: planted on a tile of the right soil (the seedbed that a shovel makes), or in a planter that holds soil, it gives a plant.
[RegisterComponent, Access(typeof(SharedFarmingSystem))]
public sealed partial class FarmSeedComponent : Component
{
    [DataField]
    public TimeSpan PlantingTime = TimeSpan.FromSeconds(1f);

    [DataField(required: true)]
    public EntProtoId PlantProto;

    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> SoilTile = new();
}

// The plant grows: every renewal it spends the energy and the resource and grows a bit.
[RegisterComponent, Access(typeof(SharedFarmingSystem))]
public sealed partial class FarmGrowingComponent : Component
{
    [DataField]
    public float EnergyCost = 0f;

    [DataField]
    public float ResourceCost = 0f;

    // Per renewal, which is not every frame: it depends on the rate of the plant.
    [DataField]
    public float GrowthPerUpdate = 0f;
}

// The plant gets energy of the light. This build has no day and night of its own, so a plant that lives on the light gets it all the time.
[RegisterComponent, Access(typeof(SharedFarmingSystem))]
public sealed partial class FarmLightEnergyComponent : Component
{
    [DataField]
    public float Energy = 0f;
}

// The plant drinks the liquid of its solution, and what it drinks gives it resource, energy or growth (see the metabolizer prototype).
[RegisterComponent, Access(typeof(SharedFarmingSystem))]
public sealed partial class FarmMetabolizerComponent : Component
{
    [DataField]
    public FixedPoint2 SolutionPerUpdate = 5f;

    [DataField(required: true)]
    public ProtoId<FarmMetabolizerPrototype> MetabolizerId;
}

// What a plant does with the reagents it drinks.
[Prototype("farmMetabolizer")]
public sealed partial class FarmMetabolizerPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, HashSet<FarmMetabolizerEffect>> Metabolization = new();
}

// Base of what a reagent does to a plant when the plant takes it up.
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class FarmMetabolizerEffect
{
    public abstract void Effect(Entity<FarmPlantComponent> plant, FixedPoint2 amount, EntityManager entityManager);
}

// A reagent effect that changes the energy, resource and growth of a plant.
public sealed partial class AffectFarmPlantValues : FarmMetabolizerEffect
{
    [DataField]
    public float Energy = 0f;

    [DataField]
    public float Resource = 0f;

    [DataField]
    public float Growth = 0f;

    public override void Effect(Entity<FarmPlantComponent> plant, FixedPoint2 amount, EntityManager entityManager)
    {
        var farming = entityManager.System<SharedFarmingSystem>();

        farming.AffectEnergy(plant, Energy * (float) amount);
        farming.AffectResource(plant, Resource * (float) amount);
        farming.AffectGrowth(plant, Growth * (float) amount);
    }
}

// A plant that can be gathered with a tool.
[RegisterComponent]
public sealed partial class FarmGatherableComponent : Component
{
    // Which tools can gather it.
    [DataField(required: true)]
    public EntityWhitelist ToolWhitelist = new();

    // The loot by the tag of the tool (the key "All" is for every tool): the table of what it gives.
    [DataField]
    public Dictionary<string, EntityTableSelector>? Loot = new();

    // The random shift of the things that appear.
    [DataField]
    public float GatherOffset = 0.3f;

    [DataField]
    public TimeSpan GatherDelay = TimeSpan.FromSeconds(1f);

    // After the harvest the plant is gone altogether.
    [DataField]
    public bool DeleteAfterHarvest;

    // Otherwise the growth falls by this much.
    [DataField]
    public float GrowthCostHarvest = 0.4f;

    // How far it must have grown to be gathered.
    [DataField]
    public float GrowthLevelToHarvest = 0.9f;

    [DataField]
    public SoundSpecifier GatherSound = new SoundPathSpecifier("/Audio/Effects/Footsteps/grass1.ogg");
}

// A planter (a tub of boards) that can hold soil. Filled with a shovel, it takes seeds like a seedbed does.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FarmPlanterSoilComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Filled;

    // What the planter turns into when a shovel is put to it: the empty one gets soil, the filled one is dug out (if nothing grows in it).
    [DataField(required: true)]
    public EntProtoId Swap;
}

// How a plant looks as it grows: one sprite state for each step of the growth.
[RegisterComponent]
public sealed partial class FarmPlantVisualsComponent : Component
{
    [DataField]
    public int GrowthSteps = 3;

    [DataField]
    public string? GrowState;
}

// The sprite layers of a plant.
public enum FarmPlantVisualLayers : byte
{
    Base,
}
