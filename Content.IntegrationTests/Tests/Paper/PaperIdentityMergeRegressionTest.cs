using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using static Content.Shared.Paper.PaperComponent;

namespace Content.IntegrationTests.Tests.Paper;

[TestFixture]
[TestOf(typeof(PaperSystem))]
public sealed class PaperIdentityMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: rank
  id: PaperMergeRank
  name: Paper Merge Rank
  prefix: PMR

- type: entity
  parent: CMMobHuman
  id: PaperMergeSigner
  name: True Paper Signer
  components:
  - type: Rank
    rank: PaperMergeRank
  - type: IdentityBlocker

- type: entity
  id: PaperMergeDocument
  components:
  - type: Paper
";

    [Test]
    public async Task SignatureUsesPresentedIdentityRankAndRoleAndReplacesOnlyRequestedTag()
    {
        await Server.WaitAssertion(() =>
        {
            var identity = Server.System<IdentitySystem>();
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var mindSystem = Server.System<MindSystem>();
            var roleSystem = Server.System<SharedRoleSystem>();
            var paperSystem = Server.System<PaperSystem>();
            var signer = SEntMan.Spawn("PaperMergeSigner");
            var paper = SEntMan.Spawn("PaperMergeDocument");
            var mind = mindSystem.CreateMind(null, "Paper signer");
            mindSystem.TransferTo(mind, signer, mind: mind.Comp);
            roleSystem.MindAddJobRole(mind, mind.Comp, jobPrototype: "CMHospitalCorpsman");

            var presentedName = identity.GetEntityIdentity(signer);
            var role = localization.GetString("cm-job-name-hospital-corpsman");
            var expectedSignature = $"PMR {presentedName}, {role}";
            Assert.Multiple(() =>
            {
                Assert.That(presentedName, Is.Not.EqualTo("True Paper Signer"),
                    "a full identity blocker must hide the signer's true metadata name");
                Assert.That(roleSystem.MindGetAllRoleInfo((mind.Owner, mind.Comp)).Single().Name,
                    Is.EqualTo("cm-job-name-hospital-corpsman"));
            });

            paperSystem.SetContent((paper, SEntMan.GetComponent<PaperComponent>(paper)),
                "first [signature] second [signature] third [signature]");
            var request = new PaperSignatureRequestMessage(1)
            {
                Actor = signer,
            };
            SEntMan.EventBus.RaiseLocalEvent(paper, request);

            var content = SEntMan.GetComponent<PaperComponent>(paper).Content;
            Assert.Multiple(() =>
            {
                Assert.That(content,
                    Is.EqualTo($"first [signature] second {expectedSignature} third [signature]"));
                Assert.That(content, Does.Not.Contain("True Paper Signer"),
                    "paper signing must use IdentitySystem's presented identity, not the entity name");
                Assert.That(content.Count(character => character == '['), Is.EqualTo(2),
                    "only the requested signature placeholder is replaced");
            });
        });
    }
}
