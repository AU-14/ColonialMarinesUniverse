using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Insurgency.Orders;

/// <summary>
///     An order sheet the cell leader can pass down to the whole cell. Passing the word does
///     not spawn anything - the orders land in every member's character brief instead, so
///     there is nothing to drop and nothing for a patrol to find on a body.
/// </summary>
[RegisterComponent]
public sealed partial class CLFStandingOrdersComponent : Component
{
    /// <summary>
    ///     Jobs allowed to pass the word down. Anyone else can read and write on the sheet,
    ///     but only the cell's leadership speaks for the cell.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> AuthorJobs = new() { "AU14JobCLFCellLeader" };

    /// <summary>
    ///     Orders longer than this are trimmed. A few lines a runner can carry in their
    ///     head, not a full operations order.
    /// </summary>
    [DataField]
    public int MaxLength = 700;

    /// <summary>
    ///     How many times the word can go round in a round. Spent budget is tracked per
    ///     round rather than per sheet, so a second pad does not buy a second allowance.
    /// </summary>
    [DataField]
    public int MaxIssues = 3;

    /// <summary>
    ///     How long the cell leader waits between passing the word down. Together with
    ///     <see cref="MaxIssues"/> this is what stops the brief being used as a private
    ///     radio net: it carries intent set before the operation, not running commentary.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);
}
