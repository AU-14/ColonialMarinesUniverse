using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.SSDTimer;

/// <summary>
///     Ported from Goob-Station PR 6441. On a mob whose player has disconnected; examining them shows how long ago.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUSSDTimerComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Since;
}

public sealed class CMUSSDTimerExamineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSSDTimerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CMUSSDTimerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var minutes = (int) (_timing.CurTime - ent.Comp.Since).TotalMinutes;
        args.PushMarkup(Loc.GetString("cmu-ssd-timer-examined", ("ent", ent.Owner), ("time", minutes)));
    }
}
