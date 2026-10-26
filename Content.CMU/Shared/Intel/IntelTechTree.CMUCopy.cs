// CMU14: display copies of a team's tech tree.
namespace Content.Shared._RMC14.Intel;

public sealed partial class IntelTechTree
{
    /// <summary>
    /// A copy whose <see cref="Points"/> can be changed for display without touching the real balance.
    /// The tier options are shared with the original.
    /// </summary>
    public IntelTechTree DisplayCopy() => (IntelTechTree) MemberwiseClone();
}
