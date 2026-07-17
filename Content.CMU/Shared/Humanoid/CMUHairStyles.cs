using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Humanoid
{
    /// <summary>
    /// Curated hairstyle lists used by the "Tie Hair Back" verb.
    /// </summary>
    public static class CMUHairStyles
    {
        /// <summary>
        /// Long, loose hairstyles that are eligible to be tied back via the "Tie Hair Back" verb.
        /// </summary>
        public static readonly IReadOnlyList<ProtoId<MarkingPrototype>> TieableHairStyles = new List<ProtoId<MarkingPrototype>>
        {
            // Ahoge
            "HumanHairAntenna",
            // Beehive / Beehive 2
            "HumanHairBeehive",
            "RMCHumanHairBeehive",
            "HumanHairBeehivev2",
            "RMCHumanHairBeehive2",
            // Bob variants
            "HumanHairBob",
            "HumanHairBob2",
            "HumanHairBobcut",
            "HumanHairBob4",
            "HumanHairBob5",
            "HumanHairBobcurl",
            "RMCHumanHairBob",
            "RMCHumanHairBobcurl",
            // Braid / braided variants
            "RMCHumanHairBraid2",
            "RMCHumanHairRowBraid",
            "RMCHumanHairGentlebraid",
            "HumanHairBraid",
            "HumanHairBraided",
            "HumanHairBraidfront",
            "HumanHairBraid2",
            "HumanHairHbraid",
            "HumanHairShortbraid",
            "HumanHairBraidtail",
            "HumanHairCornrowbraid",
            // Bunhead 2
            "HumanHairBunhead2",
            // Chrono
            "RMCHumanHairCrono",
            // Hair with "long"/"longer" in the name
            "RMCHumanHairLonger",
            "RMCHumanHairLongerAlt",
            "RMCHumanHairLongest",
            "RMCHumanHairLongFringe",
            "RMCHumanHairLongestAlt",
            "RMCHumanHairLongEmo",
            "RMCHumanHairLongOvereye",
            "HumanHairLongBedhead",
            "HumanHairLongBedhead2",
            "HumanHairClassicLong2",
            "HumanHairClassicLong3",
            "HumanHairLong",
            "HumanHairLong2",
            "HumanHairLong3",
            "HumanHairLongWithBundles",
            "HumanHairLongovereye",
            "HumanHairLbangs",
            "HumanHairLongemo",
            "HumanHairLongfringe",
            "HumanHairLongsidepart",
            "HumanHairVlong",
            "HumanHairLongest",
            "HumanHairLongest2",
            "HumanHairVlongfringe",
            "HumanHairSpookyLong",
            "HumanHairProtagonist",
            // Classic modern / classic wisp / modern / messy
            "HumanHairClassicModern",
            "HumanHairClassicWisp",
            "HumanHairModern",
            "HumanHairMessy",
            // Cornrow tail
            "HumanHairCornrowtail",
            // Croft
            "RMCHumanHairCroft",
            // Curls
            "HumanHairCurls",
            "RMCHumanHairCurls",
            // Double bun / double bun long
            "HumanHairDoublebun",
            "HumanHairDoublebunLong",
            // Drill hair / drillruru
            "HumanHairDrillhairextended",
            "HumanHairDrillruru",
            // Mohawk (unshaven)
            "HumanHairUnshavenMohawk",
            // Mullet
            "RMCHumanHairMullet",
            // Ombre
            "RMCHumanHairOmbre",
            "HumanHairOmbre",
            // One shoulder
            "HumanHairOneshoulder",
            // Overeye variants
            "RMCHumanHairShortOvereye",
            "HumanHairShortovereye",
            "HumanHairBAlt",
            "HumanHairVeryshortovereyealternate",
            // Emo
            "RMCHumanHairEmo",
            "HumanHairEmo",
            // Feather
            "RMCHumanHairFeather",
            "HumanHairFeather",
            // Flair / Flaired Hair / Flaired Hair 2
            "HumanHairFlair",
            "RMCHumanHairFlair",
            "RMCHumanHairFlair2",
            // Hair with "gentle" in the name
            "RMCHumanHairGentle",
            "RMCHumanHairGentle2",
            "RMCHumanHairGantlePonytail",
            "HumanHairGentle",
            // Haircuts with "hime" in the name
            "RMCHumanHairHimecut",
            "HumanHairHimecut",
            "HumanHairHimecut2",
            "HumanHairShorthime",
            "HumanHairHimeup",
            // Bedhead hairs
            "RMCHumanHairBedhead",
            "RMCHumanHairBedhead2",
            "RMCHumanHairBedhead3",
            "HumanHairBedhead",
            "HumanHairBedheadv2",
            "HumanHairBedheadv3",
            // Emo fringe
            "HumanHairEmofringe",
            // Ponytail (spiky)
            "HumanHairSpikyponytail",
            // Pvt. Redding
            "RMCHumanHairPvtRedding",
            // Scully variants
            "RMCHumanHairScully",
            "RMCHumanHairScully2",
            "RMCHumanHairScully2Alt",
            // Shaped
            "HumanHairShaped",
            // Short hair 2/3/7/80s/rosa
            "HumanHairShorthair2",
            "HumanHairShorthair3",
            "RMCHumanHairShorthair3",
            "HumanHairShorthairg",
            "HumanHair80s",
            "HumanHairRosa",
            // Shoulder-length hair variants
            "RMCHumanHairLong",
            "RMCHumanHairLongAlt",
            "HumanHairB",
            // Trailed (Tailed)
            "HumanHairTailed",
            // Tress shoulder
            "HumanHairTressshoulder",
            // Twintails
            "HumanHairTwintail",
            // Two strands
            "HumanHairTwoStrands",
            // Uneven
            "HumanHairUneven",
            // Unkept
            "HumanHairUnkept",
            // Volaju
            "HumanHairVolaju",
            // Wisp
            "HumanHairWisp",
        };

        /// <summary>
        /// Tied-back hairstyle options offered by the "Tie Hair Back" verb menu.
        /// </summary>
        public static readonly IReadOnlyList<ProtoId<MarkingPrototype>> TiedBackHairStyles = new List<ProtoId<MarkingPrototype>>
        {
            "HumanHairHbraid", // Braid (Low)
            "RMCHumanHairBun", // Bun
            "HumanHairManbun", // Bun (Manbun)
            "HumanHairTightbun", // Bun (Tight)
            "RMCHumanHairBun2", // Bun 2
            "HumanHairBun", // Bun Head
            "HumanHairBun3", // Bunhead 3
            "RMCHumanHairBunTopknot", // Bun, Topknot
            "HumanHairCornrowbun", // Cornrow Bun
            "AU14HumanHairElegant", // Elegant Bun
            "RMCHumanHairEmobun", // Emo Little Bun
            "AU14HumanHairLowBun", // Low Bun
            "AU14HumanHairLowPonyTail", // Low Ponytail
            "AU14HumanHairLowPonyTailAlt", // Low Profile Ponytail
            "RMCHumanHairMarineBun", // Marine Bun
            "RMCHumanHairMarineBun2", // Marine Bun 2
            "HumanHairKagami", // Pigtails
            "RMCHumanHairKagami", // Pigtails
            "RMCHumanHairRowBraid", // Row Braid
            "RMCHumanHairShavedbun", // Shaved Bun
        };
    }
}
