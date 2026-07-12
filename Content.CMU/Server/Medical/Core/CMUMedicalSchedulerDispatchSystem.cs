using Content.Server._RMC14.Damage;
using Content.Server.Body.Systems;
using Content.Shared._CMU14.Medical.Core;

namespace Content.Server._CMU14.Medical.Core;

/// <summary>
///     Owns the server update phase for sparse medical deadline dispatch.
/// </summary>
public sealed partial class CMUMedicalSchedulerDispatchSystem : EntitySystem
{
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Pain feedback historically ran after both systems so respiration
        // cannot erase newly applied asphyxiation in the same tick.
        UpdatesAfter.Add(typeof(RMCDamageableSystem));
        UpdatesAfter.Add(typeof(RespiratorSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _scheduler.DispatchDue();
    }
}
