using Content.IntegrationTests.Fixtures;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Labels.EntitySystems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Labels;

[TestFixture]
[TestOf(typeof(LabelSystem))]
public sealed class LabelMergeRegressionTest : GameTest
{
    [Test]
    public async Task HandLabelEscapesTextAndBlockExamineHidesItFromXeno()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var labelSystem = Server.System<LabelSystem>();
            var paper = SEntMan.SpawnEntity("Paper", map.GridCoords);
            var labeler = SEntMan.SpawnEntity("HandLabeler", map.GridCoords);
            var ordinary = SEntMan.SpawnEntity(null, map.GridCoords);
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var raw = "[bold]not markup[/bold]";
            var escaped = FormattedMessage.EscapeText(raw);

            try
            {
                SEntMan.GetComponent<HandLabelerComponent>(labeler).AssignedLabel = raw;
                var interact = new AfterInteractEvent(ordinary, labeler, paper, default, true);
                SEntMan.EventBus.RaiseLocalEvent(labeler, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.False);
                    Assert.That(labelSystem.HasLabel(paper), Is.True);
                    Assert.That(labelSystem.GetLabelText(paper), Is.EqualTo(escaped));
                    Assert.That(SEntMan.GetComponent<LabelComponent>(paper).CurrentLabel,
                        Is.EqualTo(escaped));
                });

                var ordinaryExamine = new ExaminedEvent(new FormattedMessage(), paper, ordinary, true, false);
                SEntMan.EventBus.RaiseLocalEvent(paper, ordinaryExamine);
                var ordinaryMarkup = ordinaryExamine.GetTotalMessage().ToMarkup();
                Assert.That(ordinaryMarkup, Does.Contain(escaped),
                    "ordinary examiners must see the escaped dynamic label");

                var xenoExamine = new ExaminedEvent(new FormattedMessage(), paper, xeno, true, false);
                SEntMan.EventBus.RaiseLocalEvent(paper, xenoExamine);
                Assert.That(xenoExamine.GetTotalMessage().ToMarkup(), Does.Not.Contain(escaped),
                    "Paper's BlockExamine whitelist must hide the label from Xeno viewers");

                Assert.Multiple(() =>
                {
                    Assert.That(labelSystem.RemoveLabel(paper), Is.True);
                    Assert.That(labelSystem.HasLabel(paper), Is.False);
                    Assert.That(labelSystem.GetLabelText(paper), Is.Null);
                    Assert.That(labelSystem.RemoveLabel(paper), Is.False);
                });

                labelSystem.Label(paper, raw);
                labelSystem.Label(paper, string.Empty);
                Assert.That(labelSystem.HasLabel(paper), Is.False);
                labelSystem.Label(paper, raw);
                labelSystem.Label(paper, null);
                Assert.That(labelSystem.HasLabel(paper), Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(paper);
                SEntMan.DeleteEntity(labeler);
                SEntMan.DeleteEntity(ordinary);
                SEntMan.DeleteEntity(xeno);
            }
        });
    }
}
