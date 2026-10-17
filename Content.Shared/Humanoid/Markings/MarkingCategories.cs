using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings
{
    [Serializable, NetSerializable]
    public enum MarkingCategories : byte
    {
        Special,
        Hair,
        FacialHair,
        Head,
        HeadTop,
        HeadSide,
        Eyes,
        Snout,
        Chest,
        UndergarmentTop,
        UndergarmentBottom,
        Arms,
        Legs,
        Tail,
        Overlay
    }

    public static class MarkingCategoriesConversion
    {
        public static MarkingCategories FromHumanoidVisualLayers(HumanoidVisualLayers layer)
        {
            return layer switch
            {
                HumanoidVisualLayers.Special => MarkingCategories.Special,
                HumanoidVisualLayers.Hair => MarkingCategories.Hair,
                HumanoidVisualLayers.FacialHair => MarkingCategories.FacialHair,
                HumanoidVisualLayers.Head => MarkingCategories.Head,
                HumanoidVisualLayers.HeadTop => MarkingCategories.HeadTop,
                HumanoidVisualLayers.HeadSide => MarkingCategories.HeadSide,
                HumanoidVisualLayers.Eyes => MarkingCategories.Eyes,
                HumanoidVisualLayers.Snout => MarkingCategories.Snout,
                HumanoidVisualLayers.SnoutCover => MarkingCategories.Snout,
                HumanoidVisualLayers.Chest => MarkingCategories.Chest,
                HumanoidVisualLayers.UndergarmentTop => MarkingCategories.UndergarmentTop,
                HumanoidVisualLayers.UndergarmentBottom => MarkingCategories.UndergarmentBottom,
                HumanoidVisualLayers.RArm => MarkingCategories.Arms,
                HumanoidVisualLayers.LArm => MarkingCategories.Arms,
                HumanoidVisualLayers.RHand => MarkingCategories.Arms,
                HumanoidVisualLayers.LHand => MarkingCategories.Arms,
                HumanoidVisualLayers.LLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.RLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.LFoot => MarkingCategories.Legs,
                HumanoidVisualLayers.RFoot => MarkingCategories.Legs,
                HumanoidVisualLayers.Tail => MarkingCategories.Tail,
                HumanoidVisualLayers.TailOverlay => MarkingCategories.Tail,
                _ => MarkingCategories.Overlay
            };
        }

        public static IReadOnlyList<HumanoidVisualLayers> ToHumanoidVisualLayers(MarkingCategories category)
        {
            return category switch
            {
                MarkingCategories.Special => [HumanoidVisualLayers.Special],
                MarkingCategories.Hair => [HumanoidVisualLayers.Hair],
                MarkingCategories.FacialHair => [HumanoidVisualLayers.FacialHair],
                MarkingCategories.Head => [HumanoidVisualLayers.Head],
                MarkingCategories.HeadTop => [HumanoidVisualLayers.HeadTop],
                MarkingCategories.HeadSide => [HumanoidVisualLayers.HeadSide],
                MarkingCategories.Eyes => [HumanoidVisualLayers.Eyes],
                MarkingCategories.Snout => [HumanoidVisualLayers.Snout, HumanoidVisualLayers.SnoutCover],
                MarkingCategories.Chest => [HumanoidVisualLayers.Chest],
                MarkingCategories.UndergarmentTop => [HumanoidVisualLayers.UndergarmentTop],
                MarkingCategories.UndergarmentBottom => [HumanoidVisualLayers.UndergarmentBottom],
                MarkingCategories.Arms =>
                [
                    HumanoidVisualLayers.RArm,
                    HumanoidVisualLayers.LArm,
                    HumanoidVisualLayers.RHand,
                    HumanoidVisualLayers.LHand
                ],
                MarkingCategories.Legs =>
                [
                    HumanoidVisualLayers.RLeg,
                    HumanoidVisualLayers.LLeg,
                    HumanoidVisualLayers.RFoot,
                    HumanoidVisualLayers.LFoot
                ],
                MarkingCategories.Tail => [HumanoidVisualLayers.Tail, HumanoidVisualLayers.TailOverlay],
                MarkingCategories.Overlay => [HumanoidVisualLayers.Overlay],
                _ => []
            };
        }
    }
}
