using Content.Server.Administration.Managers;
using Content.Shared._RMC14.Admin;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Admin;

public sealed partial class RMCAdminSpawnJobVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor) ||
            !_admin.IsAdmin(actor.PlayerSession) ||
            !HasComp<ActorComponent>(args.Target))
        {
            return;
        }

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("rmc-admin-player-actions-spawn-here-as-job"),
            Category = VerbCategory.Admin,
            Act = () => OpenJobDialog(args.User, args.Target),
            ConfirmationPopup = true,
            Impact = LogImpact.High,
        });
    }

    private void OpenJobDialog(EntityUid user, EntityUid target)
    {
        if (!TryComp(user, out ActorComponent? actor) ||
            !_admin.IsAdmin(actor.PlayerSession) ||
            !HasComp<ActorComponent>(target))
        {
            return;
        }

        var jobs = new List<DialogOption>();
        foreach (var job in _prototypes.EnumerateCM<JobPrototype>())
        {
            var ev = new SpawnAsJobDialogEvent(GetNetEntity(user), GetNetEntity(target), job.ID);
            jobs.Add(new DialogOption(job.SpawnMenuRoleName ?? job.LocalizedName, ev));
        }

        jobs.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.Ordinal));
        _dialog.OpenOptions(user, "Choose a job", jobs);
    }
}
