using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Preferences.Managers;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Threats;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class CMUCallInCharacterTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true, Connected = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task WinnerChoosesIdentityBeforeTakingControl(bool random)
    {
        var map = await Pair.CreateTestMap();
        var player = ServerSession!;
        const string used = "Avery Miller";
        const string alternate = "Morgan Ellis";
        EntityUid observer = default;
        EntityUid body = default;
        string generated = "";
        string roleName = "";
        await Server.WaitAssertion(() =>
        {
            var prefs = Server.ResolveDependency<IServerPreferencesManager>();
            var profile = new HumanoidCharacterProfile();
            prefs.SetProfile(player.UserId, 0, profile.WithName(used)).Wait();
            prefs.SetProfile(player.UserId, 1, profile.WithName(alternate).WithAge(44)).Wait();

            var previous = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<MetaDataSystem>().SetEntityName(previous, used);
            Server.PlayerMan.SetAttachedEntity(player, previous);
            observer = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(player, observer);
            SEntMan.DeleteEntity(previous);

            body = SEntMan.SpawnEntity("CMUJobUSCMCombatEngineer", map.GridCoords);
            SEntMan.EnsureComponent<CMUCallInCharacterComponent>(body);
            generated = SEntMan.GetComponent<MetaDataComponent>(body).EntityName;
            var role = SEntMan.GetComponent<GhostRoleComponent>(body);
            roleName = role.RoleName;
            var ghosts = Server.System<GhostRoleSystem>();
            ghosts.Request(player, role.Identifier);
            ghosts.Update((float) SEntMan.GetComponent<GhostRoleRaffleComponent>(body).Countdown.TotalSeconds + 1);
        });

        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();
        Assert.That(player.AttachedEntity, Is.EqualTo(observer));
        Button chosen = null!;
        await Client.WaitAssertion(() =>
        {
            var title = Loc.GetString("cmu-call-in-character-title", ("role", roleName));
            var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                .OfType<DefaultWindow>().Single(w => w.Title == title);
            var buttons = Descendants(window.Contents).OfType<Button>()
                .Where(button => !string.IsNullOrEmpty(button.Text)).ToDictionary(button => button.Text!);
            Assert.That(buttons.Keys, Does.Not.Contain(used));
            Assert.That(buttons.Keys, Does.Contain(alternate));
            chosen = buttons[random ? Loc.GetString("cmu-call-in-character-random") : alternate];
        });
        var click = new ScreenCoordinates(chosen.GlobalPixelPosition + chosen.PixelSize / 2, chosen.Root!.Window!.Id);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
            await Client.DoGuiEvent(chosen, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
                state, click, true, chosen.Size / 2, chosen.PixelSize / 2));
        await Pair.RunTicksSync(8);
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var expected = random ? generated : alternate;
            Assert.That(player.AttachedEntity, Is.EqualTo(body));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(body).EntityName, Is.EqualTo(expected));
            Assert.That(Server.System<SharedMindSystem>().GetCharacterName(player.UserId), Is.EqualTo(expected));
            Assert.That(Server.System<SharedIdCardSystem>().TryFindIdCard(body, out var id), Is.True);
            Assert.That(id.Comp.FullName, Is.EqualTo(expected));
            if (!random)
                Assert.That(SEntMan.GetComponent<HumanoidProfileComponent>(body).Age, Is.EqualTo(44));
        });
    }

    [Test]
    public async Task SpecialPartiesAreExcludedAndMixedTeamsKeepTheirSynthetic()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            foreach (var party in new[] { "MigratingCivs", "CMUUSArmyReinforcements", "CMUUSCMEngineerTeam" })
                Assert.That(SProtoMan.Index<ThirdPartyPrototype>(party).AllowSavedCharacters, Is.True, party);
            foreach (var party in new[] { "CMUYautjaMilitaryCaste", "CMUDutchsDozen", "WYPMCWhiteout", "UPPLineCommandos",
                         "ProfessorVonBandolier", "WorkingJoesAPOLLO", "ApeThirdParty", "biomorphThirdParty" })
                Assert.That(SProtoMan.Index<ThirdPartyPrototype>(party).AllowSavedCharacters, Is.False, party);

            var observer = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, observer);
            var body = SEntMan.SpawnEntity("AU14JobUSArmySynthArmored", map.GridCoords);
            SEntMan.EnsureComponent<CMUCallInCharacterComponent>(body);
            var role = SEntMan.GetComponent<GhostRoleComponent>(body);
            // The fixture player can take the body without the synthetic job's whitelist.
            role.JobProto = null;
            var ghosts = Server.System<GhostRoleSystem>();
            ghosts.Request(ServerSession!, role.Identifier);
            ghosts.Update((float) SEntMan.GetComponent<GhostRoleRaffleComponent>(body).Countdown.TotalSeconds + 1);
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(body));
        });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
