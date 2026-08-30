using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Util.Admin.Console;

/// <summary>
/// Shared plumbing for the nuke:* cleanup commands in this file: [all] bool (default false,
/// true = serious cleanup, drops the per-command safety filter) + prototype id filter args,
/// batch deletion of collected loose entities, prototype id completion, and the report line.
/// </summary>
public abstract partial class NukeEntitiesCommand : LocalizedEntityCommands
{
    [Dependency] protected SharedContainerSystem _container = default!;
    [Dependency] protected IPrototypeManager _protoMan = default!;

    public override string Description => "Nuke/delete from every loaded grid.";

    protected static (bool All, HashSet<string>? Ids) ParseBoolAndIds(string[] args)
    {
        var all = false;
        if (args.Length > 0 && bool.TryParse(args[0], out var parsed))
        {
            all = parsed;
            args = args[1..];
        }

        return (all, args.Length > 0 ? new HashSet<string>(args) : null);
    }

    protected static bool MatchesIds(MetaDataComponent meta, HashSet<string>? ids) =>
        ids == null || (meta.EntityPrototype is { } proto && ids.Contains(proto.ID));

    protected int DeleteAll(List<EntityUid> toDelete)
    {
        var removed = 0;
        foreach (var uid in toDelete)
        {
            if (!EntityManager.EntityExists(uid))
                continue;

            EntityManager.DeleteEntity(uid);
            removed++;
        }

        return removed;
    }

    protected void ReportRemoved(IConsoleShell shell, int removed, string noun, HashSet<string>? ids, string? modeSuffix)
    {
        var filterMsg = ids != null ? $" matching {ids.Count} ids" : "";
        var modeMsg = modeSuffix != null ? $" {modeSuffix}" : "";
        shell.WriteLine($"Removed {removed} {noun}{filterMsg}{modeMsg}.");
    }

    protected abstract bool IncludeProto(EntityPrototype proto);

    protected CompletionResult BoolOrIdsCompletion(string[] args, string modeHint, string idHint)
    {
        if (args.Length != 1)
            return IdsCompletion(args, idHint);

        var options = new List<string> { "true", "false" };
        options.AddRange(ProtoIds(new HashSet<string>(args)));
        return CompletionResult.FromHintOptions(options, modeHint);
    }

    protected CompletionResult IdsCompletion(string[] args, string idHint) =>
        CompletionResult.FromHintOptions(ProtoIds(new HashSet<string>(args)), idHint);

    private IEnumerable<string> ProtoIds(HashSet<string> alreadyTyped) =>
        _protoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && IncludeProto(p) && !alreadyTyped.Contains(p.ID))
            .Select(p => p.ID);
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeCartridgesCommand : NukeEntitiesCommand
{
    public override string Command => "nuke:cartridges";
    public override string Help =>
        "nuke:cartridges [all (true/false, default: false)] [cartridgeId...] - Deletes loose cartridge/casing entities from the world.\n" +
        " Cartridges that are chambered, held, worn, or otherwise inside a container are left alone.\n" +
        " By default this only removes spent (already-fired) casings, sparing live rounds on the floor.\n" +
        " Pass 'true' as the first argument to remove all loose cartridges, spent or not.\n" +
        " Optionally list cartridge prototype ids to restrict the nuke to those types.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var (all, ids) = ParseBoolAndIds(args);

        var toDelete = new List<EntityUid>();
        var query = EntityManager.EntityQueryEnumerator<CartridgeAmmoComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var cartridge, out var meta))
        {
            if (_container.IsEntityInContainer(uid))
                continue; // chambered/held/worn/stored

            if (!MatchesIds(meta, ids)
                || (!all && !cartridge.Spent))
                continue;

            toDelete.Add(uid);
        }

        ReportRemoved(shell, DeleteAll(toDelete), "loose cartridges", ids,
            all ? "(all cartridges)" : "(spent only)");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        BoolOrIdsCompletion(args, "[all (default: false)] or [cartridgeId]", "[cartridgeId...]");

    protected override bool IncludeProto(EntityPrototype proto) =>
        proto.Components.ContainsKey("CartridgeAmmo");
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeMagazinesCommand : NukeEntitiesCommand
{
    public override string Command => "nuke:magazines";
    public override string Help =>
        "nuke:magazines [all (true/false, default: false)] [magazineId...] - Deletes loose, detachable magazine entities (BallisticAmmoProvider with mayTransfer) from the world.\n" +
        " Fixed/internal ammo wells built into a gun, and magazines currently held, worn, loaded into a weapon, or stored, are left alone.\n" +
        " By default this only removes empty magazines, sparing loaded ones dropped on the floor.\n" +
        " Pass 'true' as the first argument to remove all loose magazines, loaded or not.\n" +
        " Optionally list magazine prototype ids to restrict the nuke to those types.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var (all, ids) = ParseBoolAndIds(args);

        var toDelete = new List<EntityUid>();
        var query = EntityManager.EntityQueryEnumerator<BallisticAmmoProviderComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var provider, out var meta))
        {
            if (!provider.MayTransfer)
                continue; // fixed ammo wells built into guns and turrets

            if (_container.IsEntityInContainer(uid))
                continue; // loaded into a gun, held, worn, or stored

            if (!MatchesIds(meta, ids)
                || (!all && provider.Entities.Count + provider.UnspawnedCount > 0))
                continue;

            toDelete.Add(uid);
        }

        ReportRemoved(shell, DeleteAll(toDelete), "loose magazines", ids,
            all ? "(all magazines)" : "(empty only)");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        BoolOrIdsCompletion(args, "[all (default: false)] or [magazineId]", "[magazineId...]");

    protected override bool IncludeProto(EntityPrototype proto) =>
        proto.Components.TryGetValue("BallisticAmmoProvider", out var reg)
            && reg.Component is BallisticAmmoProviderComponent { MayTransfer: true };
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukePuddlesCommand : NukeEntitiesCommand
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    private static readonly FixedPoint2 SmallVolumeThreshold = FixedPoint2.New(5);

    public override string Command => "nuke:puddles";
    public override string Help =>
        "nuke:puddles [all (true/false, default: false)] [puddleId...] - Deletes puddle entities (blood, spills, chemical messes, etc.) from the world.\n" +
        " By default this only removes small/dried-up puddles (volume 5u or less), sparing large or fresh ones\n" +
        " that might still be relevant (an active spill, a scene someone's investigating).\n" +
        " Pass 'true' as the first argument to remove every puddle regardless of volume.\n" +
        " Optionally list puddle prototype ids to restrict the nuke to those types.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var (all, ids) = ParseBoolAndIds(args);

        var toDelete = new List<EntityUid>();
        var query = EntityManager.EntityQueryEnumerator<PuddleComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var meta))
        {
            if (_container.IsEntityInContainer(uid)
                || !MatchesIds(meta, ids))
                continue;

            if (!all
                && (!_solution.TryGetSolution(uid, puddle.SolutionName, out _, out var solution)
                    || solution.Volume > SmallVolumeThreshold))
                continue;

            toDelete.Add(uid);
        }

        ReportRemoved(shell, DeleteAll(toDelete), "puddles", ids,
            all ? "(all puddles)" : "(small only)");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        BoolOrIdsCompletion(args, "[all (default: false)] or [puddleId]", "[puddleId...]");

    protected override bool IncludeProto(EntityPrototype proto) =>
        proto.Components.ContainsKey("Puddle");
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeTrashCommand : NukeEntitiesCommand
{
    [Dependency] private TagSystem _tag = default!;

    private static readonly string[] AdditionalTrashTags = { };
    private static readonly string[] AdditionalTrashPrototypes = { };

    private static readonly string[] ExcludedTrashComponents = { "Paper" };

    public override string Command => "nuke:trash";
    public override string Help =>
        "nuke:trash [trashId...] - Deletes loose trash entities from the world.\n" +
        " Trash is anything tagged 'Trash', plus the curated AdditionalTrashTags and AdditionalTrashPrototypes lists in this command's source.\n" +
        " Prototypes carrying a component from ExcludedTrashComponents (papers and friends) are always spared.\n" +
        " Trash that is held, worn, or otherwise inside a container is left alone.\n" +
        " Optionally list prototype ids to restrict the nuke to those types.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ids = args.Length > 0 ? new HashSet<string>(args) : null;

        var toDelete = new List<EntityUid>();
        // MetaDataComponent-only query: curated prototypes may carry no TagComponent
        var query = EntityManager.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (_container.IsEntityInContainer(uid) // held/worn/stored
                || !MatchesIds(meta, ids)
                || !IsTrash(uid, meta))
            {
                continue;
            }

            toDelete.Add(uid);
        }

        ReportRemoved(shell, DeleteAll(toDelete), "loose trash items", ids, null);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        IdsCompletion(args, "[trashId...]");

    private bool IsTrash(EntityUid uid, MetaDataComponent meta)
    {
        if (meta.EntityPrototype is { } proto)
        {
            if (ExcludedTrashComponents.Any(c => proto.Components.ContainsKey(c)))
                return false;

            if (AdditionalTrashPrototypes.Contains(proto.ID))
                return true;
        }

        if (!EntityManager.TryGetComponent<TagComponent>(uid, out var tags))
            return false;

        if (_tag.HasTag(tags, "Trash"))
            return true;

        foreach (var tag in AdditionalTrashTags)
        {
            if (_tag.HasTag(tags, tag))
                return true;
        }

        return false;
    }

    protected override bool IncludeProto(EntityPrototype proto) =>
        !ExcludedTrashComponents.Any(c => proto.Components.ContainsKey(c))
        && (AdditionalTrashPrototypes.Contains(proto.ID)
            || (proto.Components.TryGetValue("Tag", out var reg)
                && reg.Component is TagComponent tag
                && (_tag.HasTag(tag, "Trash") || AdditionalTrashTags.Any(t => _tag.HasTag(tag, t))))); // CMU14: analyzer-safe tag access
}
