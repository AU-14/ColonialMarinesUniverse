using System;
using System.Collections.Generic;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Presentation.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CMUMedicalOverlayVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<CMUMedicalOverlayPartVisual> Parts = new();
}

[DataRecord]
[Serializable, NetSerializable]
public partial record struct CMUMedicalOverlayPartVisual
{
    public HumanoidVisualLayers Layer { get; set; }
    public bool Bandaged { get; set; }
    public bool Splinted { get; set; }
    public byte BruteDamageLevel { get; set; }
    public byte BurnDamageLevel { get; set; }
    public int VariantSeed { get; set; }

    public CMUMedicalOverlayPartVisual(
        HumanoidVisualLayers layer,
        bool bandaged,
        bool splinted,
        byte bruteDamageLevel,
        byte burnDamageLevel,
        int variantSeed)
    {
        Layer = layer;
        Bandaged = bandaged;
        Splinted = splinted;
        BruteDamageLevel = bruteDamageLevel;
        BurnDamageLevel = burnDamageLevel;
        VariantSeed = variantSeed;
    }
}
