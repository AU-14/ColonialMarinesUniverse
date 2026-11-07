using Content.Shared.Examine;
using Content.Shared.GameTicking;

namespace Content.Shared.CMU14.Clock;

public sealed class CMUMilitaryClockSystem : EntitySystem
{
    [Dependency] private SharedGameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMilitaryClockComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CMUMilitaryClockComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("cmu-military-clock-examine", ("time", GetTime(ent.Comp).ToString(@"hh\:mm"))));
    }

    public TimeSpan GetTime(CMUMilitaryClockComponent component)
    {
        var elapsed = component.StartTime + _ticker.RoundDuration();
        var ticks = elapsed.Ticks % TimeSpan.TicksPerDay;
        if (ticks < 0)
            ticks += TimeSpan.TicksPerDay;

        return TimeSpan.FromTicks(ticks);
    }
}
