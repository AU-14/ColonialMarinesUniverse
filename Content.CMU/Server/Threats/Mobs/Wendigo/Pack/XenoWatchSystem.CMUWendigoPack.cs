using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;

namespace Content.Server._RMC14.Xenonids.Watch;

// CMU14: Wendigo pack members have no hive or queen but reuse the xeno watch UI to watch each other.
public sealed partial class XenoWatchSystem
{
    private void InitializeCMUWendigoPack()
    {
        SubscribeLocalEvent<CMUWendigoPackComponent, XenoWatchActionEvent>(OnCMUWendigoPackWatchAction);

        Subs.BuiEvents<CMUWendigoPackComponent>(XenoWatchUIKey.Key,
            subs =>
            {
                subs.Event<XenoWatchBuiMsg>(OnCMUWendigoPackWatchBui);
            });
    }

    /// <summary>
    /// True when a non-xeno pack member watches another pack member; hive and queen checks are skipped.
    /// </summary>
    private bool IsCMUWendigoPackWatch(EntityUid watcher, EntityUid toWatch)
    {
        return !HasComp<XenoComponent>(watcher) &&
               HasComp<CMUWendigoPackComponent>(watcher) &&
               HasComp<CMUWendigoPackComponent>(toWatch);
    }

    private void OnCMUWendigoPackWatchAction(Entity<CMUWendigoPackComponent> ent, ref XenoWatchActionEvent args)
    {
        // A pack member that is also a xeno uses the hive list instead.
        if (args.Handled || HasComp<XenoComponent>(ent))
            return;

        args.Handled = true;

        _ui.OpenUi(ent.Owner, XenoWatchUIKey.Key, ent.Owner);

        var members = new List<Xeno>();
        var query = EntityQueryEnumerator<CMUWendigoPackComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var metaData))
        {
            if (uid == ent.Owner || _mobState.IsDead(uid))
                continue;

            members.Add(new Xeno(GetNetEntity(uid), Name(uid, metaData), metaData.EntityPrototype?.ID));
        }

        members.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        _ui.SetUiState(ent.Owner, XenoWatchUIKey.Key, new XenoWatchBuiState(members, 0));
    }

    private void OnCMUWendigoPackWatchBui(Entity<CMUWendigoPackComponent> ent, ref XenoWatchBuiMsg args)
    {
        if (HasComp<XenoComponent>(ent))
            return;

        if (!TryGetEntity(args.Target, out var target) ||
            !IsCMUWendigoPackWatch(ent.Owner, target.Value) ||
            _mobState.IsDead(target.Value))
        {
            return;
        }

        Watch(ent.Owner, target.Value);
    }
}
