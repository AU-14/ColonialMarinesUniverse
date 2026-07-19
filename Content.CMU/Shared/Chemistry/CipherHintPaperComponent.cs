using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._CMU14.Chemistry;

[RegisterComponent]
public sealed partial class CipherHintPaperComponent : Component
{
    // will this add a xeno crate to the nearest req elevator?
    [DataField]
    public bool SpawnCrate = false;
}
