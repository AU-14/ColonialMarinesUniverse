namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public bool AntiVehicle;
    public TimeSpan NextVehicleApproach;
    public readonly HashSet<EntityUid> RememberedWeapons = new();
    public TimeSpan NextSupplyShare;
    public TimeSpan SupplyShareAt;
    public EntityUid? SupplyRecipient;
    public EntityUid? SupplyTransfer;
    public int SuppliesShared;
    public int SuppliesReceived;
    public int CratesOpened;
    public string SupplyDecision = "stocked";
    public TimeSpan NextEquipmentAction;
    public int BipodsDeployed;
    public EntityUid? AimedWeapon;
    public EntityUid? AimedTarget;
    public TimeSpan AimedStarted;
    public TimeSpan AimedUntil;
    public TimeSpan NextAimedShot;
    public string Outfit = "scavenger";
    public readonly Dictionary<EntityUid, TimeSpan> AnnouncedContacts = new();
    public string LastContactPhrase = "";
}
