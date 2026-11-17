using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Comms;

public sealed class ColonyGarrisonLiaisonTest : InteractionTest
{
    [Test]
    public async Task LiaisonOfficeIsAColonyAnnouncementConsoleWithItsOwnSenderAndLimits()
    {
        await SpawnTarget("CMUColonyGarrisonLiaisonOffice");

        var console = SEntMan.GetComponent<ColonyCommsConsoleComponent>(STarget!.Value);
        var protoMan = Server.ResolveDependency<IPrototypeManager>();

        Assert.That(console.AnnouncementTitle?.Id, Is.EqualTo("colony-garrison-liaison-announcement-title"));
        Assert.That(console.Cooldown, Is.GreaterThan(TimeSpan.Zero), "The office must be rate limited.");
        Assert.That(console.MaxLength, Is.GreaterThan(0), "The office must cap the announcement length.");

        // It has to open its own screen, not the plain colony console's.
        var ui = SEntMan.System<Robust.Shared.GameObjects.SharedUserInterfaceSystem>();
        Assert.That(ui.HasUi(STarget.Value, ColonyGarrisonLiaisonUi.Key), Is.True);
        Assert.That(ui.HasUi(STarget.Value, ColonyCommsConsoleUI.Key), Is.False);
        Assert.That(protoMan.HasIndex<EntityPrototype>("CMUColonyGarrisonLiaisonOffice"), Is.True);
    }

    [Test]
    public async Task LiaisonCanMoveMoneyBetweenTheirGarrisonAndTheColony()
    {
        await SpawnTarget("CMUColonyGarrisonLiaisonOffice");
        var office = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            var requisitions = SEntMan.System<SharedRequisitionsSystem>();
            var budget = SEntMan.System<ColonyBudgetSystem>();

            var account = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(PlayerCoords));
            var serialization = Server.ResolveDependency<Robust.Shared.Serialization.Manager.ISerializationManager>();
            var accountComp = serialization.Read<RequisitionsAccountComponent?>(
                new Robust.Shared.Serialization.Markdown.Mapping.MappingDataNode { ["faction"] = new Robust.Shared.Serialization.Markdown.Value.ValueDataNode("govfor") })!;
            SEntMan.AddComponent(account, accountComp);
            requisitions.ChangeBudget(1000, "govfor");
            budget.SetBudget(300);

            var liaison = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.EnsureComponent<MarineComponent>(liaison).Faction = "govfor";

            // Garrison pays the colony.
            SEntMan.EventBus.RaiseLocalEvent(office, new ColonyGarrisonLiaisonTransferBuiMsg(true, 400) { Actor = liaison }, true);
            Assert.That(accountComp.Balance, Is.EqualTo(600));
            Assert.That(budget.GetBudget(), Is.EqualTo(700f));

            // Colony pays the garrison.
            SEntMan.EventBus.RaiseLocalEvent(office, new ColonyGarrisonLiaisonTransferBuiMsg(false, 100) { Actor = liaison }, true);
            Assert.That(accountComp.Balance, Is.EqualTo(700));
            Assert.That(budget.GetBudget(), Is.EqualTo(600f));

            // Neither side can overdraw.
            SEntMan.EventBus.RaiseLocalEvent(office, new ColonyGarrisonLiaisonTransferBuiMsg(true, 5000) { Actor = liaison }, true);
            SEntMan.EventBus.RaiseLocalEvent(office, new ColonyGarrisonLiaisonTransferBuiMsg(false, 5000) { Actor = liaison }, true);
            Assert.That(accountComp.Balance, Is.EqualTo(700));
            Assert.That(budget.GetBudget(), Is.EqualTo(600f));
        });
    }


    [Test]
    public async Task PlainColonyConsoleKeepsDefaultSenderAndHasNoLimits()
    {
        await SpawnTarget("AUColonyCommsConsole");

        var console = SEntMan.GetComponent<ColonyCommsConsoleComponent>(STarget!.Value);
        Assert.That(console.AnnouncementTitle, Is.Null);
        Assert.That(console.Cooldown, Is.EqualTo(TimeSpan.Zero));
        Assert.That(console.MaxLength, Is.EqualTo(0));
    }
}
