// ReSharper disable CheckNamespace
using Content.Shared.CMU14.Yautja.Slow;

// pred-only cmss13 slow timings for the RMC xeno abilities. null keeps the RMC slow, so CMU castes that borrow
// these components are untouched unless their yaml opts in

namespace Content.Shared._RMC14.Xenonids.Crippling
{
    public sealed partial class XenoCripplingStrikeComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.MeleeSlow
{
    public sealed partial class XenoMeleeSlowComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.Charge
{
    public sealed partial class XenoChargeComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.Stomp
{
    public sealed partial class XenoStompComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;

        // circular stomp only, for preds standing on the stomper's own tile (cmss13 charger Crush)
        [DataField]
        public YautjaCmssSlow? YautjaSlowUnder;
    }
}

namespace Content.Shared._RMC14.Xenonids.Doom
{
    public sealed partial class XenoDoomComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.TailTrip
{
    public sealed partial class XenoTailTripComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.Abduct
{
    public sealed partial class XenoAbductComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.Tail_Lash
{
    public sealed partial class XenoTailLashComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.HighGallop
{
    public sealed partial class XenoHighGallopComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.ScissorCut
{
    public sealed partial class XenoScissorCutComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.TailSeize
{
    public sealed partial class VictimTailSeizedComponent
    {
        // added straight onto the victim, no yaml in the way, so the cmss13 tail hook timing lives here.
        // xeno_slow for 0.5s
        [DataField]
        public YautjaCmssSlow? YautjaSlow = new() { Effect = TimeSpan.FromSeconds(0.5) };
    }
}

namespace Content.Shared._RMC14.Xenonids.TailJab
{
    public sealed partial class XenoTailJabComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}

namespace Content.Shared._RMC14.Xenonids.Fling
{
    public sealed partial class XenoFlingComponent
    {
        [DataField]
        public YautjaCmssSlow? YautjaSlow;
    }
}
