using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.ZombieSummoner;

[RegisterComponent, NetworkedComponent, ComponentProtoName("ZombieSummoner")]
public sealed partial class ZombieSummonerComponent : Component
{
    [DataField]
    public EntProtoId ActionOpen = "ActionZombieSummonerOpen";

    [DataField]
    public EntityUid? ActionOpenEntity;

    [DataField]
    public EntProtoId ActionOrderHalt = "ActionZombieSummonerOrderHalt";

    [DataField]
    public EntityUid? ActionOrderHaltEntity;

    [DataField]
    public EntProtoId ActionOrderAttack = "ActionZombieSummonerOrderAttack";

    [DataField]
    public EntityUid? ActionOrderAttackEntity;

    [DataField]
    public EntProtoId ActionOrderFollow = "ActionZombieSummonerOrderFollow";

    [DataField]
    public EntityUid? ActionOrderFollowEntity;

    [DataField]
    public EntProtoId ActionOrderCheeseEm = "ActionZombieSummonerOrderCheeseEm";

    [DataField]
    public EntityUid? ActionOrderCheeseEmEntity;

    [DataField]
    public int Points = 200;

    [DataField]
    public int MaxPoints = 200;

    [DataField]
    public int ZombieCost = 10;

    [DataField]
    public int MilitaryZombieCost = 20;

    [DataField]
    public float PointsPerSecond = 0.4f;

    [DataField]
    public int MaxControlledZombies = 20;

    [DataField]
    public EntProtoId ZombiePrototype = "CMUZombieSummonerInfected";

    [DataField]
    public EntProtoId MilitaryZombiePrototype = "CMUZombieSummonerMilitaryInfected";

    [DataField]
    public List<EntProtoId> ZombieWeaponPrototypes = new()
    {
        "CMScrewdriver",
        "CMWrench",
        "CMWirecutter",
        "RMCKitchenKnife",
        "RMCKitchenKnifeChef",
        "RMCKitchenKnifeButcher",
        "RMCToolHatchet",
        "AU14ToolMachete",
        "CMM2132Machete",
        "RMCFireAxe",
        "CMCrowbar",
    };

    [DataField]
    public List<EntProtoId> MilitaryZombieWeaponPrototypes = new()
    {
        "RMCFireAxe",
        "CMM2132Machete",
        "RMCM2100Machete",
        "AU14ToolMachete",
        "RMCM5Bayonet",
        "CMM11Knife",
        "RMCCombatUtilityKnifeA",
        "RMCCombatUtilityKnifeB",
        "RMCIceAxe",
    };

    [DataField]
    public string ZombieFaceMarking = "CMUZombieSummonerBloodyCross";

    [DataField]
    public Color ZombieFaceMarkingColor = Color.FromHex("#8a0303");

    [DataField]
    public bool StartsWithFaceMark;

    [DataField]
    public DamageSpecifier ZombieBonusMeleeDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", 25 },
        },
    };

    [DataField]
    public float ZombieDelimbChance = 0.01f;

    [DataField]
    public float ZombieHitScreamChance = 0.5f;

    [DataField]
    public float SpawnRadius = 1.25f;

    [DataField]
    public float ZombieMovementSpeedModifier = 1f;

    [DataField]
    public float MilitaryZombieMovementSpeedModifier = 1f;

    [DataField]
    public List<Color> SkinColors = new();

    [DataField]
    public List<Color> EyeColors = new()
    {
        Color.Brown,
        Color.Gray,
        Color.Azure,
        Color.SteelBlue,
        Color.Black,
    };

    [DataField]
    public ZombieSummonerOrderType CurrentOrder = ZombieSummonerOrderType.Attack;

    [DataField]
    public EntityUid? OrderedTarget;

    [DataField]
    public string ZombieOrderCompound = "CMUZombieSummonerZombieCompound";

    [DataField]
    public HashSet<EntityUid> Zombies = new();

    [DataField]
    public Dictionary<ZombieSummonerOrderType, string> OrderCallouts = new()
    {
        { ZombieSummonerOrderType.Halt, "ZombieSummonerCommandHalt" },
        { ZombieSummonerOrderType.Attack, "ZombieSummonerCommandAttack" },
        { ZombieSummonerOrderType.Follow, "ZombieSummonerCommandFollow" },
        { ZombieSummonerOrderType.CheeseEm, "ZombieSummonerCommandCheeseEm" },
    };

    public float PointAccumulator;
}

[RegisterComponent, ComponentProtoName("ZombieSummonerZombieLabel")]
public sealed partial class ZombieSummonerZombieLabelComponent : Component
{
    [DataField]
    public LocId Name = "cmu-zombie-summoner-zombie-name";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieSummonerMinionComponent : Component
{
    [DataField]
    public EntityUid? Summoner;

    [DataField]
    public EntityUid? SpawnedWeapon;

    [DataField]
    public DamageSpecifier BonusMeleeDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", 25 },
        },
    };

    [DataField]
    public float DelimbChance = 0.01f;

    [DataField]
    public float HitScreamChance = 0.5f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieSummonerInsanityComponent : Component
{
    [DataField]
    public float Elapsed;

    [DataField]
    public float TransformAfter;

    [DataField]
    public float NextMessageAt;

    [DataField]
    public int MessageStage = 1;

    [DataField]
    public float StageOneMessageInterval = 60f;

    [DataField]
    public float StageTwoMessageInterval = 30f;

    [DataField]
    public float StageThreeMessageInterval = 15f;

    [DataField]
    public EntProtoId TransformPrototype = "CMUZombieSummoner";

    [DataField]
    public int TransformedStartingPoints;

    [DataField]
    public float TransformedPointsPerSecond = 0.4f;

    [DataField]
    public float DelayedFaceMarkTime = 30f;

    [DataField]
    public string FaceMarking = "CMUZombieSummonerBloodyCross";

    [DataField]
    public Color FaceMarkingColor = Color.FromHex("#c00000");
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieSummonerDelayedFaceMarkComponent : Component
{
    [DataField]
    public float TimeRemaining = 30f;

    [DataField]
    public TimeSpan ApplyAt = TimeSpan.Zero;

    [DataField]
    public string FaceMarking = "CMUZombieSummonerBloodyCross";

    [DataField]
    public Color FaceMarkingColor = Color.FromHex("#c00000");
}

public sealed partial class ZombieSummonerOpenActionEvent : InstantActionEvent;

public sealed partial class ZombieSummonerOrderActionEvent : InstantActionEvent
{
    [DataField]
    public ZombieSummonerOrderType Type;
}

[Serializable, NetSerializable]
public sealed class ZombieSummonerSpawnMessage : BoundUserInterfaceMessage
{
    public ZombieSummonerSpawnMessage(int count, ZombieSummonerSpawnType type)
    {
        Count = count;
        Type = type;
    }

    public int Count { get; }
    public ZombieSummonerSpawnType Type { get; }
}

[Serializable, NetSerializable]
public sealed class ZombieSummonerBuiState : BoundUserInterfaceState
{
    public ZombieSummonerBuiState(
        int points,
        int maxPoints,
        int zombieCost,
        int militaryZombieCost,
        int controlledZombies,
        int maxControlledZombies)
    {
        Points = points;
        MaxPoints = maxPoints;
        ZombieCost = zombieCost;
        MilitaryZombieCost = militaryZombieCost;
        ControlledZombies = controlledZombies;
        MaxControlledZombies = maxControlledZombies;
    }

    public int Points { get; }
    public int MaxPoints { get; }
    public int ZombieCost { get; }
    public int MilitaryZombieCost { get; }
    public int ControlledZombies { get; }
    public int MaxControlledZombies { get; }
}

[Serializable, NetSerializable]
public enum ZombieSummonerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum ZombieSummonerSpawnType : byte
{
    Civilian,
    Military
}

[Serializable, NetSerializable]
public enum ZombieSummonerOrderType : byte
{
    Halt,
    Attack,
    Follow,
    CheeseEm
}
