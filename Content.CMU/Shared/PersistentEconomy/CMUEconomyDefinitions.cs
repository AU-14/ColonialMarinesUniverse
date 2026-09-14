using Content.Shared.Eui;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.PersistentEconomy;

[CVarDefs]
public sealed partial class CMUEconomyCVars : CVars
{
    public static readonly CVarDef<bool> Enabled = CVarDef.Create("cmu.economy_enabled", true, CVar.SERVERONLY);
    public static readonly CVarDef<int> StakePercent = CVarDef.Create("cmu.economy_stake_percent", 10, CVar.SERVERONLY);
    // Zero disables the cap. Percentages are integers, including the multiplier (200 = x2).
    public static readonly CVarDef<int> StakeCap = CVarDef.Create("cmu.economy_stake_cap", 2000, CVar.SERVERONLY);
    public static readonly CVarDef<int> MultiplierPercent = CVarDef.Create("cmu.economy_settlement_percent", 200, CVar.SERVERONLY);
}

[Prototype("cmuLoadoutItem")]
public sealed partial class CMULoadoutItemPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public EntProtoId Entity;
    [DataField] public long Price;
    [DataField] public CMUPurchaseMode PurchaseMode;
    [DataField(required: true)] public HashSet<string> Jobs = new();
    [DataField(required: true)] public string Category = "Utility";
    [DataField] public int CategoryLimit = 1;
}

[Serializable, NetSerializable]
public enum CMUPurchaseMode : byte
{
    Deployment,
    Permanent,
}

[Serializable, NetSerializable]
public enum CMUEconomyAction : byte
{
    Refresh,
    Withdraw,
    Deposit,
    WithdrawEscrow,
    Transfer,
    Stake,
}

[Serializable, NetSerializable]
public sealed class CMUEconomyMessage : EuiMessageBase
{
    public Guid Token;
    public int ProfileId;
    public CMUEconomyAction Action;
    public long Amount;
    public string Target = "";
}

[Serializable, NetSerializable]
public sealed class CMUEconomyState : EuiStateBase
{
    public Guid Token;
    public int ProfileId;
    public string Character = "";
    public string PlayerId = "";
    public long Balance;
    public long Stake;
    public long Cap;
    public long Escrow;
    public long Credited;
    public bool StakeEnabled;
    public bool Atm;
    public string Status = "";
    public string History = "";
}
