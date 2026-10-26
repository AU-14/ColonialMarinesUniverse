using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.Radio;

namespace Content.IntegrationTests.CMU14.Construction;

// the field-built antenna head and the mast it goes on, step by step the way an engineer builds them.
// both graphs swap entities between stages, and a stage prototype missing from its graph makes the
// swap throw and the thing simply vanish - these walk every swap
public sealed class AU14AntennaHeadConstructionTest : InteractionTest
{
    private const string Frame = "AU14MastAntennaHeadFrame";
    private const string Head = "AU14MastAntennaHead";
    private const string Plasteel = "CMPlasteel";
    private const string CableStack = "RMCCable";
    private const string Board = "CMAPCElectronics";
    private const string Multitool = "Multitool";

    [Test]
    public async Task BuildAntennaHeadFromFrame()
    {
        await Server.WaitPost(() =>
            Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillEngineer", 2));

        await SpawnTarget(Frame);

        await InteractUsing(Plasteel, 20);
        AssertPrototype(Frame);

        await Interact(Board, Board, Board, Board);
        AssertPrototype(Frame);

        await InteractUsing(CableStack, 30);
        AssertPrototype(Frame);

        await InteractUsing(Plasteel, 20);
        AssertPrototype(Frame);

        await InteractUsing(Weld);
        AssertPrototype(Frame);

        await InteractUsing(Multitool);
        AssertPrototype(Head);
    }

    // CMU14: both engineering and mast-specific RTO training support the full assembly flow.
    [TestCase("RMCSkillEngineer", 2)]
    [TestCase("RMCSkillJtac", 4)]
    public async Task RaiseKeyAndReopenMast(string skill, int level)
    {
        await Server.WaitPost(() =>
            Server.System<SkillsSystem>().SetSkill(SPlayer, skill, level));

        await SpawnTarget("AU14CommsMastFooting");

        await InteractUsing(Wrench);
        await InteractUsing(CMRodMetal, 30);
        AssertPrototype("AU14CommsMastLattice");

        await InteractUsing(Weld);
        await InteractUsing(CableStack, 15);
        await InteractUsing(Head);
        AssertPrototype("AU14CommsMastRigged");

        await InteractUsing(Plasteel, 10);
        await InteractUsing(Multitool);
        AssertPrototype("AU14CommsMastRigged");

        // sealing an unkeyed feed is refused
        await InteractUsing(Screw);
        await InteractUsing(Weld);
        AssertPrototype("AU14CommsMastRigged");

        await InteractUsing("ANPRCFillCardGOVFOR");
        await InteractUsing(Screw);
        await InteractUsing(Weld);
        AssertPrototype("AU14CommsMastField");

        await Server.WaitAssertion(() =>
        {
            var anchor = SEntMan.GetComponent<ANPRCRelayAnchorComponent>(STarget!.Value);
            Assert.That(anchor.Channels, Does.Contain(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype>("radioGovforAlpha")));
        });

        // open it back up to re-key
        await InteractUsing(Screw);
        await InteractUsing(Cut);
        AssertPrototype("AU14CommsMastRigged");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<AU14MastKeyComponent>(STarget!.Value).Keys, Does.Contain("govfor"),
                "the key survives opening the feed");
        });
    }
}
