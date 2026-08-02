using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed partial class YautjaHarvestTrophyDoAfterEvent : SimpleDoAfterEvent
{
    public readonly YautjaTrophyKind Kind;

    public YautjaHarvestTrophyDoAfterEvent(YautjaTrophyKind kind)
    {
        Kind = kind;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaHarvestTrophyDoAfterEvent(Kind);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaHarvestTrophyDoAfterEvent trophy && trophy.Kind == Kind;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaButcherDoAfterEvent : SimpleDoAfterEvent
{
    public readonly YautjaButcherProcedure Procedure;
    public readonly int Stage;

    public YautjaButcherDoAfterEvent(YautjaButcherProcedure procedure, int stage)
    {
        Procedure = procedure;
        Stage = stage;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaButcherDoAfterEvent(Procedure, Stage);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaButcherDoAfterEvent butcher &&
               butcher.Procedure == Procedure &&
               butcher.Stage == Stage;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaPolishTrophyDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaCauldronBoilDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaCleanserDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaOverloadBracerDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaApcSiphonDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaHealthShardUseDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaHivebreakerDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaHunterSpearFishingDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaChainedWeaponUntangleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaCeremonialDaggerPrepareFlayDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaCeremonialDaggerFlayDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaCeremonialDaggerLimbFlayDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaRelayBeaconDoAfterEvent : SimpleDoAfterEvent
{
    public readonly YautjaRelayDestinationKind Destination;
    public readonly int CustomIndex;
    public readonly string? DestinationId;

    public YautjaRelayBeaconDoAfterEvent(
        YautjaRelayDestinationKind destination,
        int customIndex = -1,
        string? destinationId = null)
    {
        Destination = destination;
        CustomIndex = customIndex;
        DestinationId = destinationId;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaRelayBeaconDoAfterEvent(Destination, CustomIndex, DestinationId);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaRelayBeaconDoAfterEvent relay &&
               relay.Destination == Destination &&
               relay.CustomIndex == CustomIndex &&
               relay.DestinationId == DestinationId;
    }
}

public enum YautjaBracerMisuseAction : byte
{
    None,
    OpenTranslator,
    ToggleLock,
    ToggleIdChip,
    ChangeExplosionType,
    ToggleNotificationSound,
    AddTrackedItem,
    RemoveTrackedItem,
    CreateStabilisingCrystal,
    CreateHumanStabilisingCrystal,
    CreateHealingCapsule,
    CreateHuntingTrap,
    SelfDestruct,
}

[Serializable, NetSerializable]
public sealed partial class YautjaBracerMisuseDoAfterEvent : SimpleDoAfterEvent
{
    public readonly YautjaBracerMisuseAction Action;
    public readonly bool RequireWorn;
    public readonly bool AlwaysDelimb;

    public YautjaBracerMisuseDoAfterEvent(
        YautjaBracerMisuseAction action,
        bool requireWorn = true,
        bool alwaysDelimb = false)
    {
        Action = action;
        RequireWorn = requireWorn;
        AlwaysDelimb = alwaysDelimb;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaBracerMisuseDoAfterEvent(Action, RequireWorn, AlwaysDelimb);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaBracerMisuseDoAfterEvent;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaHuntEscapeScanDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaPreserveEscapeDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaLeapDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates Coordinates;

    [DataField]
    public NetEntity? Warning;

    public YautjaLeapDoAfterEvent(NetCoordinates coordinates, NetEntity? warning = null)
    {
        Coordinates = coordinates;
        Warning = warning;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaChainGauntletExecuteDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaChainGauntletForceDoorDoAfterEvent : SimpleDoAfterEvent
{
    public readonly bool Close;
    public readonly bool DamageAirlock;

    public YautjaChainGauntletForceDoorDoAfterEvent(bool close, bool damageAirlock)
    {
        Close = close;
        DamageAirlock = damageAirlock;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaChainGauntletForceDoorDoAfterEvent(Close, DamageAirlock);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaChainGauntletForceDoorDoAfterEvent door &&
               door.Close == Close &&
               door.DamageAirlock == DamageAirlock;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaBracerAttachmentForceDoorDoAfterEvent : SimpleDoAfterEvent
{
    public readonly bool Close;

    public YautjaBracerAttachmentForceDoorDoAfterEvent(bool close)
    {
        Close = close;
    }

    public override DoAfterEvent Clone()
    {
        return new YautjaBracerAttachmentForceDoorDoAfterEvent(Close);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is YautjaBracerAttachmentForceDoorDoAfterEvent door &&
               door.Close == Close;
    }
}

[Serializable, NetSerializable]
public sealed partial class YautjaTrapArmDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class YautjaTrapBreakFreeAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public sealed partial class YautjaTrapBreakFreeDoAfterEvent : SimpleDoAfterEvent;
