using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._CMU14.GasMask;

[RegisterComponent]
public sealed partial class GasMaskFilterDamageComponent : Component
{
    [DataField]
    public float Damage = 10f;

    [DataField]
    public bool Neurotoxin = false;
}
