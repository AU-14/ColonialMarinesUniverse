using Content.Shared.Access.Systems;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Access.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
[Access(typeof(SharedIdCardSystem), typeof(SharedPdaSystem), typeof(SharedAgentIdCardSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class IdCardComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    // FIXME Friends
    public string? FullName;

    [DataField]
    [AutoNetworkedField]
    [Access(typeof(SharedIdCardSystem), typeof(SharedPdaSystem), typeof(SharedAgentIdCardSystem), Other = AccessPermissions.ReadWrite)]
    public LocId? JobTitle;

    [DataField]
    [AutoNetworkedField]
    public string? _jobTitle;

    [Access(typeof(SharedIdCardSystem), typeof(SharedPdaSystem), typeof(SharedAgentIdCardSystem), Other = AccessPermissions.ReadWriteExecute)]
    public string? LocalizedJobTitle { set => _jobTitle = value; get => _jobTitle ?? (JobTitle != null ? Loc.GetString(JobTitle) : string.Empty); }

    /// <summary>
    /// The state of the job icon rsi.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<JobIconPrototype> JobIcon = "JobIconUnknown";

    /// <summary>
    /// Holds the job prototype when the ID card has no associated station record
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<JobPrototype>? JobPrototype;

    /// <summary>
    /// The proto IDs of the departments associated with the job
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<ProtoId<DepartmentPrototype>> JobDepartments = new();

    /// <summary>
    /// Determines if accesses from this card should be logged by <see cref="AccessReaderComponent"/>
    /// </summary>
    [DataField]
    public bool BypassLogging;

    [DataField]
    public LocId NameLocId = "access-id-card-component-owner-name-job-title-text";

    [DataField]
    public LocId FullNameLocId = "access-id-card-component-owner-full-name-job-title-text";

    [DataField]
    public bool CanMicrowave = true;

    //RMC14
    [DataField, AutoNetworkedField]
    public EntityUid? OriginalOwner;
    //RMC14

    //AU14
    /// <summary>
    ///     The personal bank account balance stored on this ID card.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int AccountBalance;
    //AU14

    // CMU14 ATM PIN Begin: per-card bank credentials. Server-only (neither networked nor
    // serialized) so clients never receive another card's PIN and copied cards never share one.
    /// <summary>
    ///     5-digit bank account number (10000-99999), unique per card. Assigned when the card is created.
    /// </summary>
    [ViewVariables]
    public int AccountNumber;

    /// <summary>
    ///     4-digit ATM PIN (1000-9999), unique per card. Assigned when the card is created.
    /// </summary>
    [ViewVariables]
    public int AtmPin;

    /// <summary>
    ///     How many consecutive failed PIN attempts have been made.
    /// </summary>
    [ViewVariables]
    public int PinAttempts;

    /// <summary>
    ///     Game-time timestamp after which the PIN lockout expires.
    ///     Null means the card is not locked.
    /// </summary>
    [ViewVariables]
    public TimeSpan? PinLockedUntil;
    // CMU14 End
}
