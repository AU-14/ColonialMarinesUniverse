#nullable enable

using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[TestOf(typeof(GameTicker))]
public sealed class RandomizedCharacterJobPrioritiesTest
{
    private static readonly ProtoId<JobPrototype> Rifleman = "CMRifleman";
    private static readonly ProtoId<JobPrototype> Passenger = "Passenger";

    [Test]
    public async Task RandomizedProfilePreservesJobPriorities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });
        var server = pair.Server;
        var cfg = server.CfgMan;
        var ticker = server.System<GameTicker>();
        var capture = server.System<RandomizedCharacterProfileCaptureSystem>();
        var preferences = server.ResolveDependency<IServerPreferencesManager>();
        var oldRandomCharacters = cfg.GetCVar(CCVars.ICRandomCharacters);

        try
        {
            await pair.SetJobPriorities(
                (Rifleman, JobPriority.High),
                (Passenger, JobPriority.Medium));

            var player = pair.Player ?? throw new AssertionException("Connected player was unavailable.");
            var selected = (HumanoidCharacterProfile)
                preferences.GetPreferences(player.UserId).SelectedCharacter;

            capture.CaptureNext();
            await server.WaitPost(() =>
            {
                cfg.SetCVar(CCVars.ICRandomCharacters, true);
                ticker.MakeJoinGame(player, EntityUid.Invalid);
            });

            var randomized = capture.CapturedProfile;
            Assert.That(randomized, Is.Not.Null);
            Assert.That(randomized, Is.Not.SameAs(selected));

            Assert.Multiple(() =>
            {
                Assert.That(randomized!.JobPriorities, Is.EquivalentTo(selected.JobPriorities));
                Assert.That(randomized.JobPriorities[Rifleman], Is.EqualTo(JobPriority.High));
                Assert.That(randomized.JobPriorities[Passenger], Is.EqualTo(JobPriority.Medium));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                capture.Reset();
                cfg.SetCVar(CCVars.ICRandomCharacters, oldRandomCharacters);
            });
            await pair.CleanReturnAsync();
        }
    }
}

public sealed class RandomizedCharacterProfileCaptureSystem : EntitySystem
{
    public HumanoidCharacterProfile? CapturedProfile { get; private set; }

    private bool _captureNext;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    public void CaptureNext()
    {
        CapturedProfile = null;
        _captureNext = true;
    }

    public void Reset()
    {
        CapturedProfile = null;
        _captureNext = false;
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (!_captureNext)
            return;

        _captureNext = false;
        CapturedProfile = args.Profile;
        args.Handled = true;
    }
}
