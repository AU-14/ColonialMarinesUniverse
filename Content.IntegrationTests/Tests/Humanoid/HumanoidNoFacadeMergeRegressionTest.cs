using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Humanoid;
using Content.Shared.Body;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestFixture]
[TestOf(typeof(HumanoidProfileSystem))]
public sealed class HumanoidNoFacadeMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HumanoidNoFacadeExamine
  name: Profile Subject
  components:
  - type: HumanoidProfile
    species: Human
    age: 30

- type: entity
  parent: HumanoidNoFacadeExamine
  id: HumanoidNoFacadeExamineOverride
  components:
  - type: RMCHumanoidRepresentationOverride
    species: identity-age-young
    age: identity-age-old
";

    [SidedDependency(Side.Server)] private HumanoidProfileSystem _profiles = default!;

    [Test]
    public async Task ExamineUsesDefaultOverrideSelfAwareLocaleAndPriorityOneHundred()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var examiner = SEntMan.Spawn("HumanoidNoFacadeExamine");
            var target = SEntMan.Spawn("HumanoidNoFacadeExamine");
            var overridden = SEntMan.Spawn("HumanoidNoFacadeExamineOverride");
            try
            {
                var examined = new ExaminedEvent(new FormattedMessage(), target, examiner, true, false);
                examined.PushText("priority-101", 101);
                examined.PushText("priority-099", 99);
                SEntMan.EventBus.RaiseLocalEvent(target, examined);

                var markup = examined.GetTotalMessage().ToMarkup();
                var age = _profiles.GetAgeRepresentation("Human", 30).ToLowerInvariant();
                var species = _profiles.GetSpeciesRepresentation("Human").ToLowerInvariant();
                Assert.Multiple(() =>
                {
                    Assert.That(markup, Does.Contain(age));
                    Assert.That(markup, Does.Contain(species));
                    Assert.That(markup.IndexOf("priority-101", StringComparison.Ordinal),
                        Is.LessThan(markup.IndexOf(age, StringComparison.Ordinal)),
                        "the HumanoidProfile examine line must sort below priority 101");
                    Assert.That(markup.IndexOf(age, StringComparison.Ordinal),
                        Is.LessThan(markup.IndexOf("priority-099", StringComparison.Ordinal)),
                        "the HumanoidProfile examine line must retain priority 100");
                });

                var overrideExamine = new ExaminedEvent(new FormattedMessage(), overridden, examiner, true, false);
                SEntMan.EventBus.RaiseLocalEvent(overridden, overrideExamine);
                Assert.That(overrideExamine.GetTotalMessage().ToMarkup().ToLowerInvariant(),
                    Does.Contain("old young"),
                    "RMC species and age localization overrides must replace the profile representation");

                var selfExamine = new ExaminedEvent(new FormattedMessage(), target, target, true, false);
                SEntMan.EventBus.RaiseLocalEvent(target, selfExamine);
                Assert.That(selfExamine.GetTotalMessage().ToMarkup(), Does.StartWith("You are "),
                    "self examine must use the AU self-aware locale");
            }
            finally
            {
                SEntMan.DeleteEntity(examiner);
                SEntMan.DeleteEntity(target);
                SEntMan.DeleteEntity(overridden);
            }
        });
    }

    [Test]
    public async Task ImaginaryFriendUsesHumanInitialBodyAndCanonicalOrganHands()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var friend = SEntMan.Spawn("RMCImaginaryFriendHumanoid");
            try
            {
                var initial = SEntMan.GetComponent<InitialBodyComponent>(friend);
                var body = SEntMan.GetComponent<BodyComponent>(friend);
                var hands = SEntMan.GetComponent<HandsComponent>(friend);
                Assert.That(body.Organs, Is.Not.Null);

                var categories = body.Organs!.ContainedEntities
                    .Select(organ => SEntMan.GetComponent<OrganComponent>(organ).Category!.Value)
                    .ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<VisualBodyComponent>(friend), Is.True);
                    Assert.That(SEntMan.GetComponent<HumanoidProfileComponent>(friend).Species.Id,
                        Is.EqualTo("Human"));
                    Assert.That(categories, Is.EquivalentTo(initial.Organs.Keys));
                    Assert.That(hands.Hands.Keys, Is.EquivalentTo(new[] { "left", "right" }));
                    Assert.That(hands.Hands.Keys.Any(id => id.StartsWith("hand_", StringComparison.Ordinal)),
                        Is.False,
                        "the removed static hand facade must not duplicate organ-owned hands");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(friend);
            }
        });
    }
}
