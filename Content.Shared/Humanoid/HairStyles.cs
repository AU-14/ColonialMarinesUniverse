using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid
{
    public static class HairStyles
    {
        public static readonly ProtoId<MarkingPrototype> DefaultHairStyle = "HairBald";

        public static readonly ProtoId<MarkingPrototype> DefaultFacialHairStyle = "FacialHairShaved";

        public static readonly IReadOnlyList<Color> RealisticHairColors = new List<Color>
        {
            Color.Yellow,
            Color.Black,
            Color.SandyBrown,
            Color.Brown,
            Color.Wheat,
            Color.Gray
        };

        /// <summary>
        /// Curated pool of UCMJ/SOP-compliant "regulation" haircuts, offered in the character
        /// editor's Regulation Appearance tab and enforced server-side on GOVFOR/military spawn.
        /// </summary>
        public static readonly IReadOnlyList<ProtoId<MarkingPrototype>> RegulationHairStyles = new List<ProtoId<MarkingPrototype>>
        {
            "RMCHumanHairCrew",
            "RMCHumanHairBuzz",
            "RMCHumanHairCleancut",
            "RMCHumanHairHighAndTight",
            "RMCHumanHairFlatTopFade",
            "RMCHumanHairMarineFade",
            "RMCHumanHairMarineFlatTop",
            "RMCHumanHairMarineBun",
            "RMCHumanHairMarineBun2",
            "RMCHumanHairShavedHead",
            "RMCHumanHairHeadStubble",
            "RMCHumanHairLowfade",
            "RMCHumanHairMedfade",
            "RMCHumanHairHighfade",
            "RMCHumanHairNofade",
            "RMCHumanHairSargeant",

            "RMCHumanHairJensen",
            "RMCHumanHairAverageJoe",
            "RMCHumanHairAviator",
            "RMCHumanHairBalding",
            "RMCHumanHairBob",
            "HumanHairBobcut",
            "HumanHairBoddicker",
            "RMCHumanHairBun",
            "HumanHairTightbun",
            "RMCHumanHairBun2",
            "HumanHairBun",
            "HumanHairBun3",
            "RMCHumanHairTall",
            "RMCHumanHairCIA",
            "AU14HumanHairCIA2",
            "AU14HumanHairCIA3",
            "AU14HumanHairCIA5",
            "HumanHairClassicCia",
            "HumanHairClassicMulder",
            "HumanHairCoffeehouse",
            "RMCHumanHairCoffeehouse",
            "RMCHumanHairCombover",
            "RMCHumanHairCornRows",
            "RMCHumanHairRows1",
            "RMCHumanHairRows2",
            "HumanHairCornrowbraid",
            "HumanHairCornrowbun",
            "HumanHairCornrows",
            "RMCHumanHairCplDietrich",
            "RMCHumanHairCut",
            "RMCHumanHairCurls",
            "RMCHumanHairCurlyHair",
            "HumanHairEmofringe",
            "HumanHairBaldfade",
            "HumanHairHighfade",
            "HumanHairLowfade",
            "HumanHairMedfade",
            "HumanHairNofade",
            "RMCHumanHairFlowingHair",
            "RMCHumanHairFringetail",
            "RMCHumanHairGelled",
            "RMCHumanHairHalfbang",
            "HumanHairHalfbang2",
            "RMCHumanHairHalfbangAlt",
            "RMCHumanHairHighlight",
            "RMCHumanHairIceman",
            "AU14HumanHairIRS",
            "HumanHairJensen",
            "AU14HumanHairLowBun",
            "AU14HumanHairLowPonyTailAlt",
            "RMCHumanHairLtRasczak",
            "RMCHumanHairBunMan",
            "HumanHairManbun",
            "HumanHairReversemohawk",
            "HumanHairPart",
            "RMCHumanHairShavedpart",
            "HumanHairPixie",
            "RMCHumanHairPixieCutLeft",
            "RMCHumanHairPixieCutRight",
            "HumanHairPonytail",
            "RMCHumanHairPonytail1",
            "RMCHumanHairPonytail3",
            "RMCHumanHairPonytail7",
            "RMCHumanHairChelsea",
            "RMCHumanHairChelseaSmallHawk",
            "RMCHumanHairChelseaPonytail",
            "RMCHumanHairChelseaFringe",
            "RMCHumanHairChelseaSmallHawkFringe",
            "RMCHumanHairChelseaBigHawkFringe",
            "RMCHumanHairChelseaPonytailFringe",
            "RMCHumanHairPvtJoker",
            "RMCHumanHairPvtRedding",
            "RMCHumanHairPvtClarison",
            "RMCHumanHairPvtVasquez",
            "HumanHairRonin",
            "RMCHumanHairRowbun",
            "HumanHairShaved",
            "RMCHumanHairShavedBalding",
            "HumanHairShavedpart",
            "RMCHumanHairShort",
            "HumanHairE",
            "HumanHairF",
            "RMCHumanHairLong",
            "RMCHumanHairSideUndercutHang",
            "RMCHumanHairSideUndercutReverse",
            "RMCHumanHairSideUndercut",
            "HumanHairSidecut",
            "RMCHumanHairSkinhead",
            "RMCHumanHairSleeze",
            "RMCHumanHairTaper",
            "RMCHumanHairThinning",
            "HumanHairThinningfront",
            "HumanHairThinningrear",
            "RMCHumanHairThinningrear",
            "RMCHumanHairThinningfront",
            "HumanHairTrimflat",
            "HumanHairTrimmed",
            "RMCHumanHairUndercut",
            "HumanHairUndercutleft",
            "HumanHairUndercutright",
            "RMCHumanHairUndercutTop",
            "RMCHumanHairWardaddy",

            "RMCHumanHairPonytail2",
            "AU14HumanHairLowPonyTail",
            "RMCHumanHairMarineMohawk",
            "RMCHumanHairMullet",
            "AU14HumanHairElegant",
            "RMCHumanHairEmo",
            "RMCHumanHairEmobun",
            "HumanHairKagami",
            "RMCHumanHairKagami",

            "HumanHairCia",
            "HumanHairPulato",
            "RMCHumanHairPonytail4",
            "HumanHairPonytail5",
            "HumanHairBusiness",
            "HumanHairBusiness2",
            "HumanHairBusiness3",
            "HumanHairBusiness4",
            "HumanHairClassicBusiness",
        };

        /// <summary>
        /// Curated pool of regulation facial hairstyles - clean shaves, trimmed beards/stubble,
        /// and neat mustaches, matching most real-world UCMJ/SOP grooming standards.
        /// </summary>
        public static readonly IReadOnlyList<ProtoId<MarkingPrototype>> RegulationFacialHairStyles = new List<ProtoId<MarkingPrototype>>
        {
            "HumanFacialHairSmallstache",
            "HumanFacialHairChinlessbeard",
            "HumanFacialHairCroppedfullbeard",
            "HumanFacialHair5oclockmoustache",
            "HumanFacialHairFiveoclock",
            "HumanFacialHairJensen",
            "HumanFacialHair7oclockmoustache",
            "HumanFacialHair7oclock",
            "HumanFacialHair3oclock",
            "HumanFacialHairSelleck",

            "HumanFacialHairSideburn",
            "HumanFacialHairElvis",
            "HumanFacialHairMutton",
            "HumanFacialHairMoustache",
            "HumanFacialHairPencilstache",
            "HumanFacialHairWalrus",
            "HumanFacialHairFumanchu",
            "HumanFacialHairHogan",
            "HumanFacialHairVandyke",
            "RMCHumanFacialHairSoulfulSelleck",
            "HumanFacialHairGt",
        };

        /// <summary>
        /// Named, natural hair colors offered as a searchable list in the Regulation Appearance
        /// tab (used for both hairstyle and facial hairstyle color), instead of a free-pick color wheel.
        /// </summary>
        public static readonly IReadOnlyList<(string Name, Color Color)> RegulationHairColors = new List<(string, Color)>
        {
            // Black
            ("Natural Black", new Color(24, 23, 22)),
            ("Soft Black", new Color(31, 30, 29)),
            ("Faded Black", new Color(39, 38, 37)),
            ("Ash Black", new Color(36, 37, 38)),
            ("Cool Black", new Color(27, 29, 31)),
            ("Brown Black", new Color(37, 32, 29)),

            // Dark Brown
            ("Deep Espresso", new Color(45, 35, 30)),
            ("Espresso Brown", new Color(53, 40, 34)),
            ("Dark Coffee", new Color(59, 45, 38)),
            ("Dark Chocolate", new Color(65, 47, 39)),
            ("Bitter Cocoa", new Color(66, 51, 45)),
            ("Dark Walnut", new Color(67, 50, 42)),

            // Medium Brown
            ("Walnut Brown", new Color(83, 62, 49)),
            ("Muted Chocolate", new Color(75, 55, 45)),
            ("Cocoa Brown", new Color(86, 66, 55)),
            ("Oak Brown", new Color(96, 73, 56)),
            ("Muted Walnut", new Color(93, 70, 54)),
            ("Dusty Chocolate", new Color(82, 63, 54)),

            // Light Brown
            ("Muted Oak", new Color(108, 84, 64)),
            ("Old Oak", new Color(114, 93, 75)),
            ("Hazelnut Brown", new Color(119, 91, 69)),
            ("Weathered Walnut", new Color(105, 85, 71)),
            ("Dusty Walnut", new Color(97, 76, 63)),
            ("Light Cocoa", new Color(118, 94, 77)),

            // Ash Brown
            ("Dark Ash Brown", new Color(72, 65, 61)),
            ("Ash Brown", new Color(91, 82, 76)),
            ("Medium Ash Brown", new Color(107, 96, 87)),
            ("Light Ash Brown", new Color(126, 113, 101)),
            ("Cool Ash Brown", new Color(96, 91, 89)),
            ("Smoky Brown", new Color(87, 79, 75)),

            // Chestnut
            ("Dark Chestnut", new Color(74, 48, 39)),
            ("Chestnut Brown", new Color(91, 59, 45)),
            ("Muted Chestnut", new Color(103, 68, 50)),
            ("Ash Chestnut", new Color(91, 69, 60)),
            ("Dusty Chestnut", new Color(110, 78, 61)),
            ("Weathered Chestnut", new Color(116, 88, 71)),

            // Mahogany
            ("Dark Mahogany", new Color(72, 43, 40)),
            ("Mahogany Brown", new Color(89, 51, 45)),
            ("Muted Mahogany", new Color(100, 57, 49)),
            ("Dusty Mahogany", new Color(108, 67, 58)),
            ("Cool Mahogany", new Color(82, 55, 54)),
            ("Faded Mahogany", new Color(116, 78, 68)),

            // Auburn
            ("Dark Auburn", new Color(82, 47, 39)),
            ("Muted Auburn", new Color(101, 58, 45)),
            ("Dusty Auburn", new Color(111, 68, 53)),
            ("Ash Auburn", new Color(96, 66, 58)),
            ("Brown Auburn", new Color(105, 62, 46)),
            ("Faded Auburn", new Color(120, 79, 63)),

            // Copper
            ("Dark Copper", new Color(94, 54, 40)),
            ("Copper Brown", new Color(112, 67, 47)),
            ("Muted Copper", new Color(125, 75, 51)),
            ("Dusty Copper", new Color(133, 84, 61)),
            ("Weathered Copper", new Color(139, 94, 72)),
            ("Faded Copper", new Color(145, 103, 82)),

            // Ginger
            ("Dark Ginger", new Color(97, 56, 41)),
            ("Deep Ginger", new Color(110, 63, 43)),
            ("Brown Ginger", new Color(119, 74, 52)),
            ("Muted Ginger", new Color(128, 77, 53)),
            ("Dusty Ginger", new Color(139, 88, 65)),
            ("Ash Ginger", new Color(124, 87, 72)),
            ("Light Ginger", new Color(186, 108, 60)),
            ("Burnt Ginger", new Color(111, 40, 20)),
            ("Natural Ginger", new Color(126, 50, 24)),

            // Sandy Brown
            ("Dark Sandy Brown", new Color(103, 81, 64)),
            ("Sandy Brown", new Color(125, 99, 76)),
            ("Muted Sand", new Color(139, 113, 89)),
            ("Dusty Sand", new Color(148, 124, 102)),
            ("Cool Sand", new Color(132, 116, 104)),
            ("Weathered Sand", new Color(154, 132, 110)),

            // Dirty Blonde
            ("Dark Dirty Blonde", new Color(119, 101, 76)),
            ("Dirty Blonde", new Color(139, 119, 90)),
            ("Muted Dirty Blonde", new Color(151, 132, 101)),
            ("Ash Dirty Blonde", new Color(143, 128, 108)),
            ("Dusty Dirty Blonde", new Color(158, 141, 114)),
            ("Mousy Blonde", new Color(133, 119, 101)),

            // Honey Blonde
            ("Dark Honey Blonde", new Color(137, 105, 69)),
            ("Muted Honey Blonde", new Color(155, 121, 80)),
            ("Dusty Honey Blonde", new Color(167, 134, 96)),
            ("Ash Honey Blonde", new Color(153, 132, 103)),
            ("Faded Honey Blonde", new Color(173, 145, 111)),
            ("Weathered Honey Blonde", new Color(162, 137, 105)),

            // Caramel Blonde
            ("Caramel Blonde", new Color(174, 142, 89)),
            ("Olive Blonde", new Color(183, 163, 88)),
            ("Golden Tan", new Color(202, 170, 109)),
            ("Amber Blonde", new Color(192, 152, 93)),

            // Gold
            ("Dark Gold", new Color(146, 108, 21)),
            ("Bright Gold", new Color(255, 195, 0)),
            ("Pale Gold", new Color(255, 225, 105)),

            // Ash Blonde
            ("Dark Ash Blonde", new Color(127, 117, 99)),
            ("Ash Blonde", new Color(148, 138, 119)),
            ("Muted Ash Blonde", new Color(158, 148, 129)),
            ("Dusty Ash Blonde", new Color(168, 158, 139)),
            ("Cool Ash Blonde", new Color(154, 149, 140)),
            ("Light Ash Blonde", new Color(180, 171, 151)),

            // Light Blonde
            ("Dark Natural Blonde", new Color(151, 132, 99)),
            ("Natural Blonde", new Color(170, 151, 116)),
            ("Muted Blonde", new Color(181, 163, 129)),
            ("Dusty Blonde", new Color(188, 173, 143)),
            ("Faded Blonde", new Color(197, 183, 153)),
            ("Pale Blonde", new Color(204, 194, 169)),
            ("Golden Blonde", new Color(241, 204, 143)),
            ("Flaxen Blonde", new Color(221, 193, 138)),
            ("Platinum Blonde", new Color(229, 218, 161)),

            // Grey
            ("Dark Grey", new Color(65, 64, 63)),
            ("Charcoal Grey", new Color(78, 77, 75)),
            ("Medium Grey", new Color(103, 101, 98)),
            ("Ash Grey", new Color(125, 123, 119)),
            ("Light Grey", new Color(151, 149, 144)),
            ("Silver Grey", new Color(174, 173, 169)),

            // White
            ("Dirty White", new Color(177, 174, 166)),
            ("Weathered White", new Color(191, 188, 180)),
            ("Ash White", new Color(202, 201, 196)),
            ("Silver White", new Color(213, 212, 207)),
            ("Natural White", new Color(224, 222, 215)),
            ("Pale White", new Color(233, 231, 224)),
        };
    }
}
