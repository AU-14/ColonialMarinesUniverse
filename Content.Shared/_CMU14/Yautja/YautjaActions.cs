using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaToggleVisorActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleMaskZoomActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleCloakActionEvent : InstantActionEvent;

public sealed partial class YautjaOpenMarkPanelActionEvent : InstantActionEvent;

public sealed partial class YautjaMarkForHuntActionEvent : EntityTargetActionEvent;

public sealed partial class YautjaLeapActionEvent : WorldTargetActionEvent;

public sealed partial class YautjaOpenBracerMenuActionEvent : InstantActionEvent;

public sealed partial class YautjaRecallActionEvent : InstantActionEvent;

public sealed partial class YautjaCallDiscActionEvent : InstantActionEvent;

public sealed partial class YautjaCallCombiActionEvent : InstantActionEvent;

public sealed partial class YautjaButcherActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed record YautjaButcherTargetSelectedEvent(NetEntity User, NetEntity Target);

[Serializable, NetSerializable]
public sealed record YautjaButcherProcedureSelectedEvent(NetEntity User, NetEntity Target, YautjaButcherProcedure Procedure);

public sealed partial class YautjaFalconControlActionEvent : InstantActionEvent;

public sealed partial class YautjaFalconRecallActionEvent : InstantActionEvent;

public sealed partial class YautjaSelfDestructActionEvent : InstantActionEvent;

[ByRefEvent]
public readonly record struct YautjaSelfDestructArmedEvent(EntityUid Bracer, EntityUid Hunter, EntityUid Victim, bool Remote);

[Serializable, NetSerializable]
public sealed record YautjaSelfDestructConfirmArmEvent(NetEntity User);

[Serializable, NetSerializable]
public sealed record YautjaSelfDestructConfirmCancelEvent(NetEntity User);

[Serializable, NetSerializable]
public sealed record YautjaSelfDestructConfirmRemoteDeadVictimEvent(NetEntity User, NetEntity Victim, NetEntity VictimBracer);

public sealed partial class YautjaChangeExplosionTypeActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerLockActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed record YautjaBracerConfirmDeadHunterLockEvent(NetEntity User, NetEntity Victim, NetEntity VictimBracer);

public sealed partial class YautjaTranslatorActionEvent : InstantActionEvent;

public sealed partial class YautjaAudioPanelActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerIdChipActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerNotificationSoundActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleBracerNameActionEvent : InstantActionEvent;

public sealed partial class YautjaTrackGearActionEvent : InstantActionEvent;

public sealed partial class YautjaAddTrackedItemActionEvent : InstantActionEvent;

public sealed partial class YautjaRemoveTrackedItemActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateStabilisingCrystalActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHumanStabilisingCrystalActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHealingCapsuleActionEvent : InstantActionEvent;

public sealed partial class YautjaCreateHuntingTrapActionEvent : InstantActionEvent;

public sealed partial class YautjaLinkThrallBracerActionEvent : InstantActionEvent;

public sealed partial class YautjaTransmitThrallMessageActionEvent : InstantActionEvent;

public sealed partial class YautjaStunThrallActionEvent : InstantActionEvent;

public sealed partial class YautjaSelfDestructThrallActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed record YautjaThrallSelfDestructConfirmEvent(NetEntity Master, NetEntity ThrallBracer);

public sealed partial class YautjaToggleThrallBracerLockActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleCasterActionEvent : InstantActionEvent;

public sealed partial class YautjaUsePlasmaCannonsActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleWristBladesActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleScimitarActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleShieldActionEvent : InstantActionEvent;

public sealed partial class YautjaToggleChainGauntletActionEvent : InstantActionEvent;

public sealed partial class YautjaRemoveBracerAttachmentsActionEvent : InstantActionEvent;

public sealed partial class YautjaGuardChainGauntletActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed record YautjaBracerAttachmentSlotSelectedEvent(NetEntity User, NetEntity Gear, YautjaGearKind Kind, bool SecondarySlot);

[Serializable, NetSerializable]
public sealed record YautjaHivebreakerConsentAcceptedEvent(NetEntity User, NetEntity Target, NetEntity Hivebreaker);

[Serializable, NetSerializable]
public sealed record YautjaHivebreakerConsentRejectedEvent(NetEntity User);

public sealed partial class YautjaVoiceClickActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceRoarActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceLaughActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceGrowlActionEvent : InstantActionEvent;

public sealed partial class YautjaVoicePainActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceDistractActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceDeathCryActionEvent : InstantActionEvent;

public sealed partial class YautjaVoiceDeathLaughActionEvent : InstantActionEvent;

public sealed partial class YautjaHellhoundSenseOwnerActionEvent : InstantActionEvent;

public sealed partial class YautjaAddTeleporterLocationActionEvent : InstantActionEvent;

public sealed partial class YautjaFoldCombistickActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationRushActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationRoarActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationToggleFrenzyModeActionEvent : InstantActionEvent;

public sealed partial class YautjaAbominationSmashActionEvent : EntityTargetActionEvent;

public sealed partial class YautjaAbominationFrenzyActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed record YautjaSleepingHellhoundConfirmEvent(NetEntity User);

[ByRefEvent]
public readonly record struct YautjaBracerUnequippedEvent(EntityUid User, SlotFlags SlotFlags);

[Serializable, NetSerializable]
public enum YautjaMarkUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaThrallMessageUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaTranslatorUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaAudioPanelUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaRelayBeaconUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaBracerUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum YautjaGearRackVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum YautjaGearRackVisualState : byte
{
    Left,
    LeftCentre,
    Centre,
    RightCentre,
    Right,
}

[Serializable, NetSerializable]
public enum YautjaBracerPanelCommand : byte
{
    OpenMarks,
    LinkThrallBracer,
    OpenThrallTransmission,
    StunThrall,
    ToggleThrallSelfDestruct,
    ToggleThrallBracerLock,
    RemoteExecuteYoungblood,
    OpenTranslator,
    ToggleBracerLock,
    ToggleBracerIdChip,
    CreateStabilisingCrystal,
    CreateHumanStabilisingCrystal,
    CreateHuntingTrap,
    ToggleSelfDestruct,
    RefreshTracker,
    ChangeExplosionType,
    RemoveBracerAttachments,
    CreateHealingCapsule,
    AddTrackedItem,
    RemoveTrackedItem,
    ToggleBracerName,
    ToggleBracerNotificationSound,
}

[Serializable, NetSerializable]
public sealed class YautjaBracerPanelState(
    int charge,
    int maxCharge,
    bool locked,
    bool idChipDeployed,
    bool selfDestructArmed,
    string? thrallName,
    bool thrallLinked,
    bool thrallSelfDestructArmed,
    bool thrallBracerLocked,
    YautjaTrackerReadout trackerReadout,
    List<YautjaGearTrackerEntry> trackedGear) : BoundUserInterfaceState
{
    public readonly int Charge = charge;
    public readonly int MaxCharge = maxCharge;
    public readonly bool Locked = locked;
    public readonly bool IdChipDeployed = idChipDeployed;
    public readonly bool SelfDestructArmed = selfDestructArmed;
    public readonly string? ThrallName = thrallName;
    public readonly bool ThrallLinked = thrallLinked;
    public readonly bool ThrallSelfDestructArmed = thrallSelfDestructArmed;
    public readonly bool ThrallBracerLocked = thrallBracerLocked;
    public readonly YautjaTrackerReadout TrackerReadout = trackerReadout;
    public readonly List<YautjaGearTrackerEntry> TrackedGear = trackedGear;
}

[Serializable, NetSerializable]
public sealed class YautjaTrackerReadout(
    int deadHuntingGrounds,
    int deadOrbit,
    int deadLowOrbit,
    int gearHuntingGrounds,
    int gearOrbit,
    int gearLowOrbit,
    bool closestPresent,
    string? closestName,
    int closestDistance,
    byte closestDirection,
    int closestBearing,
    string? closestArea)
{
    public readonly int DeadHuntingGrounds = deadHuntingGrounds;
    public readonly int DeadOrbit = deadOrbit;
    public readonly int DeadLowOrbit = deadLowOrbit;
    public readonly int GearHuntingGrounds = gearHuntingGrounds;
    public readonly int GearOrbit = gearOrbit;
    public readonly int GearLowOrbit = gearLowOrbit;
    public readonly bool ClosestPresent = closestPresent;
    public readonly string? ClosestName = closestName;
    public readonly int ClosestDistance = closestDistance;
    public readonly byte ClosestDirection = closestDirection;
    public readonly int ClosestBearing = closestBearing;
    public readonly string? ClosestArea = closestArea;

    public List<string> GetCmss13ReadoutLines()
    {
        var lines = new List<string>();

        if (DeadHuntingGrounds > 0 || DeadOrbit > 0 || DeadLowOrbit > 0)
        {
            lines.Add(Loc.GetString(
                "cmu-yautja-tracker-readout-dead",
                ("locations", GetCmss13BucketReadout(DeadHuntingGrounds, DeadOrbit, DeadLowOrbit))));
        }

        if (GearHuntingGrounds > 0 || GearOrbit > 0 || GearLowOrbit > 0)
        {
            lines.Add(Loc.GetString(
                "cmu-yautja-tracker-readout-gear",
                ("locations", GetCmss13BucketReadout(GearHuntingGrounds, GearOrbit, GearLowOrbit))));
        }

        if (ClosestPresent)
        {
            if (ClosestDistance == 0)
            {
                var closestItem = string.IsNullOrWhiteSpace(ClosestName)
                    ? string.Empty
                    : Loc.GetString("cmu-yautja-tracker-closest-owner", ("name", ClosestName));
                lines.Add(Loc.GetString("cmu-yautja-tracker-closest-on-top", ("signature", closestItem)));
            }
            else
            {
                var closestItem = string.IsNullOrWhiteSpace(ClosestName)
                    ? string.Empty
                    : Loc.GetString("cmu-yautja-tracker-closest-item", ("name", ClosestName));
                var distance = ClosestDistance > 10
                    ? Loc.GetString("cmu-yautja-tracker-approximate-distance", ("distance", RoundCmss13TrackerDistance(ClosestDistance)))
                    : $"<b>{ClosestDistance}</b>";
                lines.Add(Loc.GetString(
                    "cmu-yautja-tracker-closest-away",
                    ("signature", closestItem),
                    ("distance", distance),
                    ("direction", Loc.GetString(GetCmss13DirectionText(ClosestDirection))),
                    ("area", string.IsNullOrWhiteSpace(ClosestArea)
                        ? Loc.GetString("cmu-yautja-tracker-unknown-area")
                        : ClosestArea)));
            }
        }

        if (lines.Count == 0)
            lines.Add(Loc.GetString("cmu-yautja-tracker-no-signatures"));

        return lines;
    }

    private static string GetCmss13BucketReadout(int huntingGrounds, int orbit, int lowOrbit)
    {
        var entries = new List<string>(3);

        if (huntingGrounds > 0)
            entries.Add(Loc.GetString("cmu-yautja-tracker-location-hunting-grounds", ("count", huntingGrounds)));

        if (orbit > 0)
            entries.Add(Loc.GetString("cmu-yautja-tracker-location-orbit", ("count", orbit)));

        if (lowOrbit > 0)
            entries.Add(Loc.GetString("cmu-yautja-tracker-location-low-orbit", ("count", lowOrbit)));

        return string.Concat(entries);
    }

    private static int RoundCmss13TrackerDistance(int distance)
    {
        return distance / 10 * 10;
    }

    private static string GetCmss13DirectionText(byte direction)
    {
        return direction switch
        {
            0 => "cmu-yautja-tracker-direction-north",
            1 => "cmu-yautja-tracker-direction-northeast",
            2 => "cmu-yautja-tracker-direction-southeast",
            3 => "cmu-yautja-tracker-direction-south",
            4 => "cmu-yautja-tracker-direction-southwest",
            5 => "cmu-yautja-tracker-direction-northwest",
            _ => "cmu-yautja-tracker-direction-unknown",
        };
    }
}

[Serializable, NetSerializable]
public sealed class YautjaGearTrackerEntry(string name, byte direction, int distance, int bearing, int count = 1)
{
    public readonly string Name = name;
    public readonly byte Direction = direction;
    public readonly int Distance = distance;
    public readonly int Bearing = bearing;
    public readonly int Count = count;
}

[Serializable, NetSerializable]
public sealed class YautjaBracerPanelRefreshMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class YautjaBracerPanelCommandMsg(YautjaBracerPanelCommand command) : BoundUserInterfaceMessage
{
    public readonly YautjaBracerPanelCommand Command = command;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelState(List<YautjaMarkPanelEntry> entries) : BoundUserInterfaceState
{
    public readonly List<YautjaMarkPanelEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelEntry(NetEntity entity, string name, bool isXeno, List<YautjaMarkKind> marks)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly bool IsXeno = isXeno;
    public readonly List<YautjaMarkKind> Marks = marks;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelRefreshMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelMarkMsg(NetEntity target, YautjaMarkKind kind, string? reason) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly YautjaMarkKind Kind = kind;
    public readonly string? Reason = reason;
}

[Serializable, NetSerializable]
public sealed class YautjaMarkPanelUnmarkMsg(NetEntity target, YautjaMarkKind kind) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly YautjaMarkKind Kind = kind;
}

[Serializable, NetSerializable]
public sealed record YautjaBloodedThrallNameEvent(NetEntity Hunter, NetEntity Target, string Message = "") : DialogInputEvent(Message);

[Serializable, NetSerializable]
public sealed class YautjaThrallSendMessageMsg(string message) : BoundUserInterfaceMessage
{
    public readonly string Message = message;
}

[Serializable, NetSerializable]
public sealed class YautjaTranslatorBuiState(int charge, int maxCharge, int cost, int maxLength) : BoundUserInterfaceState
{
    public readonly int Charge = charge;
    public readonly int MaxCharge = maxCharge;
    public readonly int Cost = cost;
    public readonly int MaxLength = maxLength;
}

[Serializable, NetSerializable]
public sealed class YautjaTranslatorSendMessageMsg(string message) : BoundUserInterfaceMessage
{
    public readonly string Message = message;
}

[Serializable, NetSerializable]
public sealed class YautjaAudioPanelState(List<YautjaAudioPanelEntry> entries, TimeSpan cooldownRemaining) : BoundUserInterfaceState
{
    public readonly List<YautjaAudioPanelEntry> Entries = entries;
    public readonly TimeSpan CooldownRemaining = cooldownRemaining;
}

[Serializable, NetSerializable]
public sealed class YautjaAudioPanelEntry(string emoteId, string name, string category)
{
    public readonly string EmoteId = emoteId;
    public readonly string Name = name;
    public readonly string Category = category;
}

[Serializable, NetSerializable]
public sealed class YautjaAudioPanelEmoteMsg(string emoteId) : BoundUserInterfaceMessage
{
    public readonly string EmoteId = emoteId;
}

[Serializable, NetSerializable]
public sealed class YautjaRelayBeaconState(List<YautjaRelayBeaconDestinationEntry> destinations) : BoundUserInterfaceState
{
    public readonly List<YautjaRelayBeaconDestinationEntry> Destinations = destinations;
}

[Serializable, NetSerializable]
public sealed class YautjaRelayBeaconDestinationEntry(
    YautjaRelayDestinationKind kind,
    string name,
    bool available,
    int customIndex = -1,
    string? destinationId = null)
{
    public readonly YautjaRelayDestinationKind Kind = kind;
    public readonly string Name = name;
    public readonly bool Available = available;
    public readonly int CustomIndex = customIndex;
    public readonly string? DestinationId = destinationId;
}

[Serializable, NetSerializable]
public sealed class YautjaRelayBeaconDestinationMsg(
    YautjaRelayDestinationKind destination,
    int customIndex = -1,
    string? destinationId = null) : BoundUserInterfaceMessage
{
    public readonly YautjaRelayDestinationKind Destination = destination;
    public readonly int CustomIndex = customIndex;
    public readonly string? DestinationId = destinationId;
}

[ByRefEvent]
public record struct YautjaMarkAttemptEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, string? Reason, bool Cancelled = false);

[ByRefEvent]
public record struct YautjaMarkAppliedEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, string? Reason);

[ByRefEvent]
public record struct YautjaMarkRemoveAttemptEvent(EntityUid Hunter, EntityUid Target, YautjaMarkKind Kind, bool Cancelled = false);

[ByRefEvent]
public record struct YautjaMarkRemovedEvent(
    EntityUid Hunter,
    EntityUid Target,
    YautjaMarkKind Kind,
    bool CleanupOnly = false,
    bool TargetDestroyed = false);
