namespace Content.Shared._RMC14.Xenonids;

public sealed partial class XenoComponent
{
    /// <summary>Fraction of the puller's usual drag speed when this caste is dead.</summary>
    public float CorpseDragMultiplier => Tier switch
    {
        2 => 0.8f,
        3 => 0.6f,
        _ => 1f,
    };
}
