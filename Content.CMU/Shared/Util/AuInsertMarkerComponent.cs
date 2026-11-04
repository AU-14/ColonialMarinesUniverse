using Content.Shared.CMU14;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.util;
[RegisterComponent]
public sealed partial class AuInsertMarkerComponent : Component
{

    [DataField("markerID")]
    public string markerID { get; private set; } =  "genericthirdparty";






}

