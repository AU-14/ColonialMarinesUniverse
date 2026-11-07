using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Access;

/// <summary>
/// A Joint Base Schaefer access terminal. An MP ID remains in the reader while other IDs are scanned.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBaseAccessScannerComponent : Component
{
    public const string OperatorCardSlotId = "cmu_base_access_operator_card";

    [DataField]
    public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_card_in.wav");

    [DataField]
    public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_card_out.wav");

    [DataField]
    public int MaxEntries = 50;

    public EntityUid? CurrentUser;
    public EntityUid? CardInsertedBy;
    public TimeSpan? CardInsertedAt;
    public CMUBaseAccessDirection Direction = CMUBaseAccessDirection.Entering;
    public string StatusMessage = string.Empty;
    public List<CMUBaseAccessLogEntry> Entries = new();
}
