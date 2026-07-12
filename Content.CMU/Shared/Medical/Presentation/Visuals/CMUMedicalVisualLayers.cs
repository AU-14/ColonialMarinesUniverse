using Content.Shared.Body.Part;
using Content.Shared.Humanoid;

namespace Content.Shared._CMU14.Medical.Presentation.Visuals;

public static class CMUMedicalVisualLayers
{
    public static HumanoidVisualLayers? ForBodyPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) => HumanoidVisualLayers.LArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => HumanoidVisualLayers.RArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => HumanoidVisualLayers.LHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => HumanoidVisualLayers.RHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => HumanoidVisualLayers.LLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => HumanoidVisualLayers.RLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => HumanoidVisualLayers.LFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => HumanoidVisualLayers.RFoot,
            _ => null,
        };
}
