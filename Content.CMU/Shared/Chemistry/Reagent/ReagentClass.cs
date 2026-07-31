namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
    [DataField]
    public ReagentClass Class = ReagentClass.None;

    [DataField]
    public ReagentFlags Flags;

    [DataField]
    public int GenTier;

    [DataField]
    public bool Generated;

    [DataField]
    public int Reward = 2;

    [DataField]
    public bool Lockdown;
}

public enum ReagentClass
{
    None = 0,
    Basic = 1,
    Common = 2,
    Uncommon = 3,
    Rare = 4,
    Special = 5,
    Ultra = 6,
    Hydro = 7,
}

[Flags]
public enum ReagentFlags
{
    Medical = 1 << 0,
    Scannable = 1 << 1,
    NotIngestible = 1 << 2,
    CannotOverdose = 1 << 3,
    Stimulant = 1 << 4,
    NoGeneration = 1 << 5,
    Specialist = 1 << 6,
}
