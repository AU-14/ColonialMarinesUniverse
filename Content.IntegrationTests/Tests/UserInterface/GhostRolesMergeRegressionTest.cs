using System.Reflection;
using System.Numerics;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Roles;
using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Roles;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class GhostRolesMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task EligibilityGroupsStayVisibleLockedAndPreserveBatchSearchState()
    {
        var localMap = await Pair.CreateTestMap();
        var remoteMap = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        NetEntity source = default;
        GhostRolesEui? eui = null;
        GhostRolesWindow? window = null;
        GhostRoleRulesWindow? rules = null;
        BoxContainer? entryContainer = null;
        Label? countLabel = null;
        ScrollContainer? roleScroll = null;
        PanelContainer? contentPanel = null;
        Label? noRolesMessage = null;
        bool originalTimers = default;
        bool originalEnabled = default;
        string originalColor = string.Empty;

        await Task.WhenAll(
            Server.WaitPost(() => originalTimers = Server.CfgMan.GetCVar(CCVars.GameRoleTimers)),
            Client.WaitPost(() =>
            {
                originalEnabled = Client.CfgMan.GetCVar(CCVars.CrtUiEnabled);
                originalColor = Client.CfgMan.GetCVar(CCVars.CrtUiColor);
            }));

        try
        {
            await Server.WaitPost(() =>
            {
                Server.CfgMan.SetCVar(CCVars.GameRoleTimers, true);
                var local = SEntMan.SpawnEntity("CMMobHuman", localMap.GridCoords);
                var remote = SEntMan.SpawnEntity("CMMobHuman", remoteMap.GridCoords);
                source = SEntMan.GetNetEntity(remote);
                Server.PlayerMan.SetAttachedEntity(ServerSession, local);
            });
            await Pair.RunUntilSynced();

            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.CfgMan.GetCVar(CCVars.GameRoleTimers), Is.True,
                    "the server-owned role timer setting must replicate before eligibility is evaluated");
                var requirements = Client.ResolveDependency<JobRequirementsManager>();
                Assert.That(requirements.IsAllowed(
                        new List<ProtoId<JobPrototype>> { "Captain" },
                        null,
                        null,
                        out _),
                    Is.False,
                    "the connected zero-playtime client supplies a stable ineligible role discriminator");

                eui = new GhostRolesEui();
                window = GetPrivate<GhostRolesWindow>(eui, "_window");
                entryContainer = window.FindControl<BoxContainer>("EntryContainer");
                countLabel = window.FindControl<Label>("CountLabel");
                roleScroll = window.FindControl<ScrollContainer>("RoleScroll");
                contentPanel = window.FindControl<PanelContainer>("ContentPanel");
                noRolesMessage = window.FindControl<Label>("NoRolesMessage");
                var roles = Roles(source);

                eui.HandleState(new GhostRolesEuiState(roles));

                var entries = entryContainer.Children.OfType<GhostRoleInfoBox>().ToArray();
                var sharedEntries = entries
                    .Where(entry => entry.FindControl<Label>("Title").Text == "Shared role")
                    .ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Length.EqualTo(roles.Length),
                        "identical role text must still split by eligibility and denial reason");
                    Assert.That(sharedEntries, Has.Length.EqualTo(2));
                    Assert.That(GetPrivate<int>(window, "_availableRoleCount"), Is.EqualTo(roles.Length));
                    Assert.That(countLabel.Text, Does.Contain(roles.Length.ToString()));
                    Assert.That(sharedEntries.Select(entry =>
                            Descendants(entry).OfType<GhostRoleEntryButtons>().Single().RequestButton.Disabled),
                        Is.EquivalentTo(new[] { false, true }),
                        "the ineligible group stays visible but exposes a locked request action");
                });

                var firstDummies = GetPrivate<List<EntityUid>>(window, "_previewDummies");
                Assert.That(firstDummies, Has.Count.EqualTo(1),
                    "the remote mapped source with no EntityPrototype must use the valid Captain job-preview route");
                Assert.That(CEntMan.EntityExists(firstDummies.Single()), Is.True);
                window.OpenCentered();
            });
            await Pair.RunTicksSync(5);

            await Client.WaitAssertion(() =>
            {
                var roles = Roles(source);
                roleScroll!.SetScrollValue(new Vector2(0, 1000));
                var beforeScroll = roleScroll.GetScrollValue(ignoreVisible: true);
                Assert.That(beforeScroll.Y, Is.GreaterThan(0),
                    "the overflowing role list supplies a nonzero scroll restoration discriminator");
                var firstDummy = GetPrivate<List<EntityUid>>(window, "_previewDummies").Single();

                eui!.HandleState(new GhostRolesEuiState(roles));
                var replacement = GetPrivate<List<EntityUid>>(window, "_previewDummies").Single();
                Assert.Multiple(() =>
                {
                    Assert.That(CEntMan.EntityExists(firstDummy), Is.False,
                        "rebuilding entries must delete the obsolete job-preview dummy");
                    Assert.That(CEntMan.EntityExists(replacement), Is.True);
                    Assert.That(replacement, Is.Not.EqualTo(firstDummy));
                    Assert.That(roleScroll.GetScrollValue(ignoreVisible: true), Is.EqualTo(beforeScroll));
                    Assert.That(roleScroll.HScrollTarget, Is.EqualTo(beforeScroll.X));
                    Assert.That(roleScroll.VScrollTarget, Is.EqualTo(beforeScroll.Y));
                    Assert.That(entryContainer!.ChildCount, Is.EqualTo(roles.Length),
                        "BeginEntryUpdate and EndEntryUpdate replace rather than append groups");
                });

                SetPrivate(window, "_searchText", "searchable");
                InvokePrivate(window, "UpdateVisibleEntries");
                Assert.Multiple(() =>
                {
                    Assert.That(entryContainer.Children.Count(child => child.Visible), Is.EqualTo(1));
                    Assert.That(contentPanel!.Visible, Is.True);
                    Assert.That(noRolesMessage!.Visible, Is.False);
                    Assert.That(GetPrivate<int>(window, "_availableRoleCount"), Is.EqualTo(roles.Length),
                        "search filtering must not rewrite the total available count");
                });

                SetPrivate(window, "_searchText", "no matching role");
                InvokePrivate(window, "UpdateVisibleEntries");
                Assert.Multiple(() =>
                {
                    Assert.That(entryContainer.Children, Has.All.Matches<Control>(child => !child.Visible));
                    Assert.That(contentPanel!.Visible, Is.False);
                    Assert.That(noRolesMessage!.Visible, Is.True);
                    Assert.That(noRolesMessage.Text,
                        Is.EqualTo(Loc.GetString("ghost-roles-window-no-results-label")));
                });

                rules = new GhostRoleRulesWindow(
                    "Rules",
                    GhostRoleKind.RaffleJoined,
                    _ => { });
                Assert.That(GetPrivate<GhostRoleKind>(rules, "_ghostRoleKind"),
                    Is.EqualTo(GhostRoleKind.RaffleJoined),
                    "the rules window retains the grouped role's raffle interaction kind");

                Client.CfgMan.SetCVar(CCVars.CrtUiColor,
                    originalColor == CCVars.CrtUiColorBlue ? CCVars.CrtUiColorRed : CCVars.CrtUiColorBlue);
                Client.CfgMan.SetCVar(CCVars.CrtUiEnabled, !originalEnabled);
                Assert.That(window.Stylesheet,
                    Is.SameAs(Client.ResolveDependency<IStylesheetManager>().SheetNano),
                    "live ghost role windows follow the rebuilt CRT palette");

                window.Dispose();
                Assert.That(CEntMan.EntityExists(replacement), Is.False,
                    "disposing the window must delete its current job-preview dummy");
                window = null;
            });
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                rules?.Dispose();
                window?.Dispose();
                Client.CfgMan.SetCVar(CCVars.CrtUiColor, originalColor);
                Client.CfgMan.SetCVar(CCVars.CrtUiEnabled, originalEnabled);
            });
            await Server.WaitPost(() =>
            {
                Server.CfgMan.SetCVar(CCVars.GameRoleTimers, originalTimers);
                Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached);
            });
            await Pair.RunUntilSynced();
        }
    }

    [Test]
    public async Task MakeRoleBuildsDefaultCustomRaffleAndSentienceCommands()
    {
        await Client.WaitAssertion(() =>
        {
            var eui = new MakeGhostRoleEui();
            var recorder = DispatchProxy.Create<IClientConsoleHost, RecordingConsoleHost>();
            var commands = ((RecordingConsoleHost) recorder).Commands;
            SetPrivate(eui, "_consoleHost", recorder);
            var entity = new NetEntity(42);
            var window = GetPrivate<MakeGhostRoleWindow>(eui, "_window");

            try
            {
                InvokeMake(eui, entity, "Default role", "Default description", "Default rules", false, null);
                Assert.That(commands, Is.EqualTo(new[]
                {
                    "makeghostrole \"42\" \"Default role\" \"Default description\" \"Default rules\"",
                }));

                commands.Clear();
                var settings = new GhostRoleRaffleSettings
                {
                    InitialDuration = 12,
                    JoinExtendsDurationBy = 3,
                    MaxDuration = 45,
                };
                InvokeMake(eui, entity, "Raffle role", "Raffle description", "Raffle rules", true, settings);
                Assert.That(commands, Is.EqualTo(new[]
                {
                    "makeghostroleraffled \"42\" \"Raffle role\" \"Raffle description\" 12 3 45 \"Raffle rules\"",
                    "makesentient \"42\"",
                }));
            }
            finally
            {
                window.Dispose();
            }
        });
    }

    private static GhostRoleInfo[] Roles(NetEntity source)
    {
        var roles = new List<GhostRoleInfo>
        {
            Role(1, "Shared role", "Same description", null),
            Role(2, "Shared role", "Same description", "Captain"),
            Role(3, "Unique role", "Searchable description", null),
            Role(4, "Preview role", "Remote job preview", null, source, "Captain"),
        };

        for (uint i = 0; i < 20; i++)
            roles.Add(Role(100 + i, $"Overflow role {i}", $"Overflow description {i}", null));

        return roles.ToArray();
    }

    private static GhostRoleInfo Role(
        uint id,
        string name,
        string description,
        ProtoId<JobPrototype>? eligibilityJob,
        NetEntity entity = default,
        string? previewJob = null)
    {
        return new GhostRoleInfo
        {
            Identifier = id,
            Entity = entity,
            JobPrototype = previewJob,
            Name = name,
            Description = description,
            Rules = "Rules",
            Kind = GhostRoleKind.FirstComeFirstServe,
            RolePrototypes = (eligibilityJob == null
                ? null
                : new List<ProtoId<JobPrototype>> { eligibilityJob.Value }, null),
        };
    }

    private static void InvokeMake(
        MakeGhostRoleEui eui,
        NetEntity entity,
        string name,
        string description,
        string rules,
        bool sentient,
        GhostRoleRaffleSettings? raffle)
    {
        typeof(MakeGhostRoleEui)
            .GetMethod("OnMake", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(eui, new object?[] { entity, name, description, rules, sentient, raffle });
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

    private static T GetPrivate<T>(object instance, string field)
    {
        return (T) instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void SetPrivate(object instance, string field, object value)
    {
        instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string method)
    {
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, null);
    }

    public class RecordingConsoleHost : DispatchProxy
    {
        public List<string> Commands { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IClientConsoleHost.ExecuteCommand) &&
                args?.OfType<string>().LastOrDefault() is { } command)
            {
                Commands.Add(command);
            }

            return targetMethod?.ReturnType == typeof(bool) ? false : null;
        }
    }
}
