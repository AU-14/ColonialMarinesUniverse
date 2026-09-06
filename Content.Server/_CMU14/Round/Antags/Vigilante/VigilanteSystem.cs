using Content.Server.Antag;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared._CMU14.Round.Antags.Vigilante;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Round.Antags.Vigilante;

public sealed partial class VigilanteSystem : EntitySystem
{
    private static readonly HashSet<string> MobJobs = new() { "AU14JobMobBoss", "AU14JobMobGoon" };

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VigilanteComponent, ComponentStartup>(OnVigilanteSpawned);
    }

    private void OnVigilanteSpawned(EntityUid uid, VigilanteComponent comp, ComponentStartup args)
    {
        comp.NextFax = _timing.CurTime + comp.FaxDelay;
    }

    public override void Update(float frameTime)
    {
        var enumerator = EntityManager.AllEntityQueryEnumerator<VigilanteComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.Faxed || _timing.CurTime < comp.NextFax)
                continue;

            comp.Faxed = true;
            _antag.SendBriefing(uid, BuildTargetList(comp), Color.FromHex("#8b0000"), null);
        }
    }

    private string BuildTargetList(VigilanteComponent comp)
    {
        var names = new List<string>();
        var minds = EntityManager.AllEntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (mind.CurrentEntity == null
                || !_jobs.MindTryGetJobId(mindId, out var job)
                || job == null
                || !MobJobs.Contains(job.Value.Id))
                continue;

            names.Add(mind.CharacterName ?? MetaData(mind.CurrentEntity.Value).EntityName);
        }

        comp.TargetCount = names.Count;
        return names.Count == 0
            ? Loc.GetString("cmu-vigilante-empty")
            : Loc.GetString("cmu-vigilante-list") + "\n" + string.Join("\n", names);
    }
}
