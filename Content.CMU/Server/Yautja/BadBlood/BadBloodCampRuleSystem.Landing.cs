using Content.Shared._RMC14.Dropship;

namespace Content.Server.CMU14.Yautja.BadBlood;

// The marine landing latch: the first dropship to reach the planet while the rule runs. Marine-facing Totem and
// Lodge intel waits for it. RMC's MarinesLandedChangedEvent is not used: its CMU fallback latches once per server
// lifetime, which misfires on pooled test servers.
public sealed partial class BadBloodCampRuleSystem
{
    partial void InitializeLanding()
    {
        // DropshipSystem raises this only for planet maps
        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLandedOnPlanet);
    }

    private void OnDropshipLandedOnPlanet(ref DropshipLandedOnPlanetEvent ev)
    {
        MarkMarinesLanded();
    }

    /// <summary>
    /// Latches <see cref="BadBloodCampRuleComponent.MarinesLanded"/> on the running rule and raises
    /// <see cref="BadBloodMarinesLandedEvent"/> the first time. False when there is no rule or it already latched.
    /// </summary>
    public bool MarkMarinesLanded()
    {
        if (!TryGetRule(out var rule) || rule.Comp.MarinesLanded)
            return false;

        rule.Comp.MarinesLanded = true;
        rule.Comp.MarinesLandedAt = Timing.CurTime;

        var ev = new BadBloodMarinesLandedEvent(rule);
        RaiseLocalEvent(ref ev);
        return true;
    }

    /// <summary>
    /// True once marines have landed on the planet during the running rule. False with no rule.
    /// </summary>
    public bool HaveMarinesLanded()
    {
        return TryGetRule(out var rule) && rule.Comp.MarinesLanded;
    }

    /// <summary>
    /// When marines landed during the running rule, or null before that.
    /// </summary>
    public TimeSpan? MarinesLandedAt()
    {
        return TryGetRule(out var rule) ? rule.Comp.MarinesLandedAt : null;
    }
}
