using Content.Shared.CMU14.Round.Objectives;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// CMU14: lets a faction's Analyzer Machine process RMC intel items for that faction.
namespace Content.Shared._RMC14.Intel;

public enum IntelAnalyzeResult : byte
{
    /// <summary>Not an intel item.</summary>
    NotIntel,

    /// <summary>Still locked; another piece of intel has to be processed first.</summary>
    Locked,

    /// <summary>Already processed by someone.</summary>
    AlreadyDone,

    /// <summary>Processed and credited to the team.</summary>
    Credited,
}

public sealed partial class IntelSystem
{
    private static readonly EntProtoId DataDiskProto = "CMUIntelDataDisk";

    /// <summary>Data disks per round.</summary>
    private const int DataDiskCount = 12;

    /// <summary>Called from RunSpawners in place of RMC's disabled disk spawning.</summary>
    private void SpawnCMUDataDisks()
    {
        SpawnIntel(DataDiskProto, DataDiskCount, _diskChances);
    }

    /// <summary>Credits an uploaded data disk to <paramref name="team"/>.</summary>
    public void CreditDataDisk(string team, FixedPoint2 value)
    {
        if (_net.IsClient)
            return;

        var tree = EnsureTechTree(team);
        tree.Comp.Tree.UploadData.Current++;
        AddPoints(tree, value, team);
    }

    /// <summary>A team's intel points including fractions, for display.</summary>
    public float GetIntelPointsExact(string team)
    {
        if (string.IsNullOrEmpty(team) || team == Team.None || !TryGetTechTree(team, out var tree))
            return 0;

        return tree.Value.Comp.Tree.Points.Float();
    }

    /// <summary>
    /// A team's spendable points: objective (win) points plus intel points. This is the number the tech
    /// console shows. Server only; on the client this is just the intel points.
    /// </summary>
    public FixedPoint2 GetCombinedPoints(string team)
    {
        var intel = TryGetTechTree(team, out var tree) ? tree.Value.Comp.Tree.Points : FixedPoint2.Zero;
        return GetAuWinPoints(team, intel);
    }

    /// <summary>
    /// Spends <paramref name="cost"/> from the combined pool, taking intel points first so objective points,
    /// which count toward winning, are only used for whatever intel can't cover.
    /// </summary>
    public bool TrySpendCombinedPoints(string team, FixedPoint2 cost)
    {
        if (_net.IsClient || string.IsNullOrEmpty(team) || team == Team.None)
            return false;

        if (GetCombinedPoints(team) < cost)
            return false;

        var tree = EnsureTechTree(team);
        var fromIntel = FixedPoint2.Min(tree.Comp.Tree.Points, cost);
        tree.Comp.Tree.Points -= fromIntel;
        Dirty(tree);

        var fromObjectives = cost - fromIntel;
        if (fromObjectives > FixedPoint2.Zero)
            RaiseLocalEvent(new SpendWinPointsEvent { Team = team, Amount = fromObjectives });

        UpdateTree(tree);
        return true;
    }

    /// <summary>
    /// Processes an intel item as if it had been read and recovered: credits its value to <paramref name="team"/>'s
    /// tech tree, then unlocks whatever intel it points to. Clues for the newly unlocked intel are added to
    /// <paramref name="clues"/>. Items that are still locked, or were already processed, are left alone.
    /// </summary>
    public IntelAnalyzeResult AnalyzeIntel(EntityUid intel, string team, float multiplier, List<string> clues, out FixedPoint2 points)
    {
        points = FixedPoint2.Zero;

        TryComp(intel, out IntelReadObjectiveComponent? read);
        TryComp(intel, out IntelRetrieveItemObjectiveComponent? retrieve);
        if (read == null && retrieve == null)
            return IntelAnalyzeResult.NotIntel;

        // Documents unlock by reading; recovered devices unlock through their own state.
        var locked = read != null
            ? read.State == IntelObjectiveState.Inactive
            : retrieve!.State == IntelObjectiveState.Inactive;
        if (locked)
            return IntelAnalyzeResult.Locked;

        var readDone = read == null || read.State == IntelObjectiveState.Complete;
        var retrieveDone = retrieve == null || retrieve.State == IntelObjectiveState.Complete;
        if (readDone && retrieveDone)
            return IntelAnalyzeResult.AlreadyDone;

        if (_net.IsClient)
            return IntelAnalyzeResult.Credited;

        var tree = EnsureTechTree(team);

        if (read != null && read.State != IntelObjectiveState.Complete)
        {
            read.State = IntelObjectiveState.Complete;
            Dirty(intel, read);
            tree.Comp.Tree.Documents.Current++;
            points += read.Value;
        }

        if (retrieve != null && retrieve.State != IntelObjectiveState.Complete)
        {
            retrieve.State = IntelObjectiveState.Complete;
            Dirty(intel, retrieve);
            tree.Comp.Tree.RetrieveItems.Current++;
            points += retrieve.Value;
            RemComp<ActiveIntelPositionComponent>(intel);

            if (TryComp(intel, out IntelCluesComponent? ownClues) && ownClues.Category is { } ownCategory &&
                tree.Comp.Tree.Clues.TryGetValue(ownCategory, out var ownCategoryClues))
            {
                ownCategoryClues.Remove(GetNetEntity(intel));
            }
        }

        if (TryComp(intel, out IntelUnlocksComponent? unlocks))
        {
            foreach (var unlock in unlocks.Unlocks)
            {
                if (TerminatingOrDeleted(unlock))
                    continue;

                if (TryComp(unlock, out IntelCluesComponent? cluesComp))
                {
                    var msg = Loc.GetString(cluesComp.Clue, ("intel", unlock), ("area", cluesComp.InitialArea));
                    clues.Add(msg);

                    if (cluesComp.Category is { } category &&
                        TryComp(unlock, out IntelRetrieveItemObjectiveComponent? unlockRetrieve) &&
                        unlockRetrieve.State != IntelObjectiveState.Complete)
                    {
                        tree.Comp.Tree.Clues.GetOrNew(category)[GetNetEntity(unlock)] = msg;
                    }
                }

                ActivateIntel(intel, unlock);
            }

            unlocks.Unlocks.Clear();
            Dirty(intel, unlocks);
        }

        points *= multiplier;
        AddPoints(tree, points, team);
        return IntelAnalyzeResult.Credited;
    }
}
