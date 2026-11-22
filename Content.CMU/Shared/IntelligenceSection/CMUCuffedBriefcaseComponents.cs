using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.IntelligenceSection;

/// <summary>
///     A briefcase that can be cuffed to its holder's wrist. While cuffed it cannot be dropped, swapped or taken.
///     Only its own key uncuffs it, and only someone holding that key can open it. Wire cutters can cut the cuff,
///     which frees the case for good but leaves it locked.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUCuffedBriefcaseSystem))]
public sealed partial class CMUCuffedBriefcaseComponent : Component
{
    /// <summary>The key spawned alongside the case when it is created.</summary>
    [DataField]
    public EntProtoId KeyPrototype = "CMUIntelBriefcaseKey";

    /// <summary>The only key that fits this case.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Key;

    /// <summary>Who the case is cuffed to, if anyone.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CuffedTo;

    /// <summary>
    ///     The first person to pick the case up is cuffed to it and gets its key put in their pocket.
    ///     This is how a Case Officer spawns with it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CuffOnFirstPickup = true;

    /// <summary>Once the cuff is cut it is gone and the case can never be cuffed again.</summary>
    [DataField, AutoNetworkedField]
    public bool CuffCut;

    [DataField]
    public TimeSpan CuffDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan CutDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public ProtoId<ToolQualityPrototype> CutQuality = "Cutting";

    [DataField]
    public SoundSpecifier CuffSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_end.ogg");

    [DataField]
    public SoundSpecifier UncuffSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_takeoff_end.ogg");
}

/// <summary>The key to one <see cref="CMUCuffedBriefcaseComponent"/>. Small enough to swallow.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUCuffedBriefcaseSystem))]
public sealed partial class CMUBriefcaseKeyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Briefcase;

    [DataField]
    public TimeSpan SwallowDelay = TimeSpan.FromSeconds(3);
}

/// <summary>Put on whoever a briefcase is cuffed to, so others can work on the cuff through them.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUCuffedBriefcaseSystem))]
public sealed partial class CMUCuffedBriefcaseWearerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Briefcase;
}

/// <summary>Holds things a mob has swallowed. They come back up when the mob vomits, or fall out if the body is destroyed.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUCuffedBriefcaseSystem))]
public sealed partial class CMUSwallowedItemsComponent : Component
{
    public const string ContainerId = "cmu-swallowed-items";
}

[Serializable, NetSerializable]
public sealed partial class CMUBriefcaseCuffDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUBriefcaseCutDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUBriefcaseKeySwallowDoAfterEvent : SimpleDoAfterEvent;
