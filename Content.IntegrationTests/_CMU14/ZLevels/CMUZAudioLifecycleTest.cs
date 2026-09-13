using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZAudioLifecycleTest : GameTest
{
    private const string SoundPath = "/Audio/Effects/beep1.ogg";
    private static readonly AudioParams LoopParameters = AudioParams.Default.WithLoop(true).WithMaxDistance(20);
    private readonly List<EntityUid> _sources = new();
    private EntityUid _lower;
    private EntityUid _upper;
    private EntityUid _network;
    private EntityUid _emitter;
    private EntityUid _listener;
    private EntityUid? _originalAttached;
    private Tile _floor;
    private SharedMapSystem _maps = null!;
    private SharedAudioSystem _audio = null!;
    private IConfigurationManager _configuration = null!;
    private bool _originalEnabled;
    private bool _originalCrossZAudio;

    [SetUp]
    public async Task CreateAudioScenario()
    {
        await Server.WaitAssertion(() =>
        {
            _maps = SEntMan.System<SharedMapSystem>();
            _audio = SEntMan.System<SharedAudioSystem>();
            _configuration = Server.ResolveDependency<IConfigurationManager>();
            _originalEnabled = _configuration.GetCVar(CMUZLevelsCVars.Enabled);
            _originalCrossZAudio = _configuration.GetCVar(CMUZLevelsCVars.CrossZAudio);
            _configuration.SetCVar(CMUZLevelsCVars.Enabled, true);
            _configuration.SetCVar(CMUZLevelsCVars.CrossZAudio, true);
            _floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            _lower = _maps.CreateMap(runMapInit: true);
            _upper = _maps.CreateMap(runMapInit: true);
            SEntMan.EnsureComponent<MapGridComponent>(_lower);
            SEntMan.EnsureComponent<MapGridComponent>(_upper);
            foreach (var map in new[] { _lower, _upper })
                for (var x = -4; x <= 4; x++)
                    for (var y = -4; y <= 4; y++)
                        _maps.SetTile(map, SComp<MapGridComponent>(map), new Vector2i(x, y), _floor);

            SetUpperOpening(Vector2i.Zero, true);
            var z = SEntMan.System<CMUZLevelsSystem>();
            var network = z.CreateZNetwork();
            _network = network;
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [_lower] = 0, [_upper] = 1 }), Is.True);
            _emitter = SEntMan.SpawnEntity(null, new EntityCoordinates(_upper, new Vector2(0.5f)));
            _listener = SEntMan.SpawnEntity(null, new EntityCoordinates(_lower, new Vector2(0.5f)));
            SEntMan.EnsureComponent<EyeComponent>(_listener);
            _originalAttached = ServerSession!.AttachedEntity;
            Server.PlayerMan.SetAttachedEntity(ServerSession, _listener);
        });
        await Pair.RunTicksSync(2);
    }

    [TearDown]
    public async Task CleanAudioScenario()
    {
        await Server.WaitPost(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, _originalAttached);
            _configuration.SetCVar(CMUZLevelsCVars.Enabled, _originalEnabled);
            _configuration.SetCVar(CMUZLevelsCVars.CrossZAudio, _originalCrossZAudio);
            if (SEntMan.EntityExists(_upper))
                _maps.SetPaused(_upper, false);
            foreach (var source in _sources)
                _audio.Stop(source);
        });
        await Pair.RunTicksSync(2);
        foreach (var uid in new[] { _lower, _upper, _network })
            if (uid.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(uid);
    }

    [TestCase("entity")]
    [TestCase("static")]
    [TestCase("predicted")]
    public async Task FinalizedAudienceNeverLeaksToOtherFloor(string kind)
    {
        await Server.WaitAssertion(() =>
        {
            // Both surfaces are open, so the old immediate projection path would
            // reach this listener before the engine installed its final filter.
            _maps.SetTile(_lower, SComp<MapGridComponent>(_lower), Vector2i.Zero, Tile.Empty);
            var sound = new ResolvedPathSpecifier(SoundPath);
            var played = kind switch
            {
                "entity" => _audio.PlayEntity(sound, Filter.Empty(), _emitter, false, LoopParameters),
                "static" => _audio.PlayStatic(sound, Filter.Empty(),
                    new EntityCoordinates(_upper, new Vector2(0.5f)), false, LoopParameters),
                _ => _audio.PlayPredicted(new SoundPathSpecifier(SoundPath), _emitter, _listener, LoopParameters),
            };
            Assert.That(played, Is.Not.Null);
            _sources.Add(played!.Value.Entity);
            Assert.That(AudioOn(_lower), Is.Empty,
                "No automatic projection may run inside the playback call before its audience is final.");
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() => Assert.That(AudioOn(_lower), Is.Empty));
    }

    [Test]
    public async Task LoopCrossesUpperHoleIntoSolidRoomAndTracksMotionAndClosure()
    {
        EntityUid source = default;
        EntityUid firstProjection = default;
        await Server.WaitAssertion(() => source = PlayLoop());
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            firstProjection = AudioOn(_lower).Single();
            Assert.That(SComp<AudioComponent>(firstProjection).IncludedEntities, Does.Contain(_listener));
            Assert.That(_maps.GetTileRef(_lower, SComp<MapGridComponent>(_lower), Vector2i.Zero).Tile, Is.EqualTo(_floor));
            SetUpperOpening(new Vector2i(2, 0), true);
            SetUpperOpening(Vector2i.Zero, false);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(_emitter,
                new EntityCoordinates(_upper, new Vector2(2.5f, 0.5f)));
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Is.EqualTo(new[] { firstProjection }),
                "Motion with the same audience reuses the owned projection.");
            Assert.That(SEntMan.System<SharedTransformSystem>().GetWorldPosition(firstProjection),
                Is.EqualTo(new Vector2(2.5f, 0.5f)));
            SetUpperOpening(new Vector2i(2, 0), false);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Is.Empty);
            SetUpperOpening(new Vector2i(2, 0), true);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            var projection = AudioOn(_lower).Single();
            var length = _audio.GetAudioLength(new ResolvedPathSpecifier(SoundPath)).TotalSeconds;
            var originalPhase = (SGameTiming.CurTime - SComp<AudioComponent>(source).AudioStart).TotalSeconds % length;
            var projectedPhase = (SGameTiming.CurTime - SComp<AudioComponent>(projection).AudioStart).TotalSeconds % length;
            var difference = Math.Abs(originalPhase - projectedPhase);
            Assert.That(Math.Min(difference, length - difference), Is.LessThan(0.025),
                "Reopening the route resumes the source's playback phase.");
            _audio.Stop(source);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(AudioOn(_lower), Is.Empty));
    }

    [Test]
    public async Task LoopReconcilesListenerRangeAndPauseState()
    {
        EntityUid source = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedTransformSystem>().SetCoordinates(_listener,
                new EntityCoordinates(_lower, new Vector2(40, 40)));
            source = PlayLoop();
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Is.Empty);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(_listener,
                new EntityCoordinates(_lower, new Vector2(0.5f)));
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Has.Count.EqualTo(1));
            _audio.SetState(source, AudioState.Paused);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<AudioComponent>(AudioOn(_lower).Single()).State, Is.EqualTo(AudioState.Paused));
            _audio.SetState(source, AudioState.Playing);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<AudioComponent>(AudioOn(_lower).Single()).State, Is.EqualTo(AudioState.Playing));
            SEntMan.DeleteEntity(source);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(AudioOn(_lower), Is.Empty));
    }

    [Test]
    public async Task StationaryLoopReconcilesMapDetachAndReattach()
    {
        await Server.WaitAssertion(() => PlayLoop());
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Has.Count.EqualTo(1));
            Assert.That(SEntMan.System<CMUZLevelsSystem>().TryRemoveMapFromZNetwork(_upper), Is.True);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Is.Empty);
            Assert.That(SEntMan.System<CMUZLevelsSystem>().TryAddMapsIntoZNetwork(
                (_network, SComp<CMUZLevelsNetworkComponent>(_network)), new() { [_upper] = 1 }), Is.True);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(AudioOn(_lower), Has.Count.EqualTo(1)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StationaryLoopReturnsAfterPropagationIsReenabled(bool masterSwitch)
    {
        var setting = masterSwitch ? CMUZLevelsCVars.Enabled : CMUZLevelsCVars.CrossZAudio;
        await Server.WaitAssertion(() => PlayLoop());
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Has.Count.EqualTo(1));
            _configuration.SetCVar(setting, false);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(AudioOn(_lower), Is.Empty);
            _configuration.SetCVar(setting, true);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(AudioOn(_lower), Has.Count.EqualTo(1)));
    }

    [Test]
    public async Task PausingOnlySourceMapFreezesAndResumesPersistentProjection()
    {
        EntityUid source = default;
        EntityUid projection = default;
        var resumedTicks = 0;
        await Server.WaitAssertion(() => source = PlayLoop());
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            projection = AudioOn(_lower).Single();
            _maps.SetPaused(_upper, true);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<AudioComponent>(source).State, Is.EqualTo(AudioState.Playing),
                "Map pause does not set the source AudioComponent.State.");
            Assert.That(SComp<AudioComponent>(projection).State, Is.EqualTo(AudioState.Paused));
            _maps.SetPaused(_upper, false);
            resumedTicks = (int) Math.Ceiling((_audio.GetAudioLength(new ResolvedPathSpecifier(SoundPath)).TotalSeconds +
                SharedAudioSystem.AudioDespawnBuffer + 0.5) * 60);
        });
        await Pair.RunTicksSync(resumedTicks);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(source), Is.True);
            Assert.That(AudioOn(_lower), Is.EqualTo(new[] { projection }),
                "A resumed owned loop must not receive a one-shot despawn timer.");
            Assert.That(SComp<AudioComponent>(projection).State, Is.EqualTo(AudioState.Playing));
        });
    }

    [Test]
    public async Task SourceStoppedInPlaybackTickNeverProjects()
    {
        await Server.WaitAssertion(() =>
        {
            var played = _audio.PlayPvs(new ResolvedPathSpecifier(SoundPath), _emitter);
            Assert.That(played, Is.Not.Null);
            _sources.Add(played!.Value.Entity);
            _audio.Stop(played.Value.Entity);
            SEntMan.System<CMUZLevelsSystem>().Update(0f);
            Assert.That(AudioOn(_lower), Is.Empty,
                "An audio entity queued for deletion must not create a projection before shutdown runs.");
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() => Assert.That(AudioOn(_lower), Is.Empty));
    }

    private EntityUid PlayLoop()
    {
        var sound = _audio.PlayPvs(new ResolvedPathSpecifier(SoundPath), _emitter, LoopParameters);
        Assert.That(sound, Is.Not.Null);
        _sources.Add(sound!.Value.Entity);
        return sound.Value.Entity;
    }

    private void SetUpperOpening(Vector2i tile, bool open)
    {
        _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), tile, open ? Tile.Empty : _floor);
    }

    private List<EntityUid> AudioOn(EntityUid map)
    {
        var result = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var transform))
            if (transform.MapUid == map && audio.FileName == SoundPath)
                result.Add(uid);
        return result;
    }
}
