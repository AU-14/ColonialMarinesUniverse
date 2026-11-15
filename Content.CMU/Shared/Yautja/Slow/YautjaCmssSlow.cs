namespace Content.Shared.CMU14.Yautja.Slow;

// one cmss13 xeno slow as it lands on a pred. cmss13 never slows for a flat time, it bumps a counter the
// 2s life tick eats, so how long it really lasts depends on where in the tick the hit lands
[DataDefinition]
public sealed partial class YautjaCmssSlow
{
    // SUPERSLOW instead of SLOW
    [DataField]
    public bool Super;

    // apply_effect amount, one gets eaten per life tick
    [DataField]
    public float Ticks;

    // xeno_slow ttl, the effect adds 1.1 every second while it's alive so it outlasts this by a fair bit
    [DataField]
    public TimeSpan Effect;
}
