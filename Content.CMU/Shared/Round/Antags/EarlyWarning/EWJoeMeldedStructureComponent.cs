// CMU14
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// ARCH-11: a structure the Early Warning Joe Offered to a Meld directive (a camera, a telecomms server). It is
/// melded into the river rather than consumed: it stays, lit orange and switched off for good. The marker is
/// also what keeps it from being offered, and counted, a second time. Added by
/// <c>EWJoeMeldedStructureSystem.Meld</c>.
/// Networked for presence only, like <see cref="EWJoeAssimilatedConsoleComponent"/>: <see cref="MeldedBy"/>
/// stays server-side because <see cref="EWJoeComponent"/> is SendOnlyToOwner, and the antag's identity must
/// not leak through the structures it touched.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EWJoeMeldedStructureComponent : Component
{
    /// <summary>The Joe that melded this structure, if known. Server only (see the class note).</summary>
    [DataField]
    public EntityUid? MeldedBy;
}
