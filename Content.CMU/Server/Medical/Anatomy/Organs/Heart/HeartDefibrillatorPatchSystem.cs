using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._RMC14.Medical.Defibrillator;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.CMU14.Medical.Anatomy.Organs.Heart;

public sealed partial class HeartDefibrillatorPatchSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedHeartSystem _heart = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, RMCDefibrillatorAttemptEvent>(OnDefibAttempt);
    }

    private void OnDefibAttempt(Entity<CMUHumanMedicalComponent> ent, ref RMCDefibrillatorAttemptEvent args)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) || !_cfg.GetCVar(CMUMedicalCCVars.OrganEnabled))
            return;

        if (args.Cancelled || args.Target != ent.Owner)
            return;
        if (!_heart.TryPrepareDefibrillation(ent, args.AllowBeatingHeart, out var token, out var reason))
        {
            args.Cancel(reason);
            return;
        }
        args.Heart = token;
    }
}
