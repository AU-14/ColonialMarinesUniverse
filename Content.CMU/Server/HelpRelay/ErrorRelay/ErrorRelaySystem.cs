using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Content.Server.CMU14.HelpRelay.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Serilog.Events;

namespace Content.Server.CMU14.HelpRelay.ErrorRelay;

/// <summary>
/// Mirrors server-side error logs to Discord through the help relay: every Error and Fatal log
/// line the server emits, plus Info-and-up traffic from sawmills named in
/// cmu.help_relay_errors_sawmills. The log handler buffers lines from any thread; the main thread
/// flushes them as chained embeds, so context gathering never leaves the game thread. Fatal entries
/// post immediately with the last context snapshot, because Fatal usually precedes a shutdown
/// that would outrun the next flush.
/// </summary>
public sealed class ErrorRelaySystem : EntitySystem, ILogHandler
{
    [Dependency] private readonly HelpRelayManager _relay = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    // One batch stays under the payload builder's 3000 char cap, so the suppression note is never
    // the part that gets cut.
    private const int BatchChars = 2800;

    // Posts per flush: three per five seconds stays under the relay queue's one-per-1.2s drain, so
    // a storm drains over successive flushes instead of overflowing the queue.
    private const int MaxPostsPerFlush = 3;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    // Buffer overflow drops the newest lines behind a counter; the server console still carries
    // everything it logged.
    private const int BufferCap = 256;

    // The admin log feed groups Extreme admin logs into the ahelp conversation model: one Discord
    // message edited in place, chained when full. Lines are rarer than error lines, so its buffer
    // and caps are smaller.
    private const int AdminQueueCap = 128;
    private const int AdminLineCap = 1000;

    /// <summary>Conversation id of the admin log feed; a permanent key that no player id collides with.</summary>
    private static readonly Guid AdminFeedId = new("dc1e5c0d-c0de-4000-8000-5e1f605e0001");

    private const string RelaySawmill = "helprelay";
    private const string SelfSawmill = "errorrelay";

    private readonly ConcurrentQueue<ErrorLine> _buffer = new();

    // Main-thread only: entries a flush's post budget did not reach wait here for the next flush.
    private readonly List<ErrorLine> _carry = new();

    private readonly ConcurrentQueue<string> _adminQueue = new();
    private int _dropped;
    private int _adminDropped;
    private float _untilFlush;

    // Volatile snapshots for cross-thread reads: the allowlist swaps wholesale on cvar change, the
    // context refreshes on the main thread and serves Fatal posts raised on other threads.
    private volatile HashSet<string> _allow = new();
    private volatile HelpContext? _context;

    public override void Initialize()
    {
        // Wire the relay's cvars before the log handler attaches, so boot-time errors cannot hit a
        // window where the webhook cvar still reads empty. Idempotent in the manager.
        _relay.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _context = null);
        SubscribeLocalEvent<AdminLogExtremeEvent>(OnAdminLogExtreme);
        _config.OnValueChanged(CCVars.HelpRelayErrorsSawmills, OnSawmillsChanged, true);
        _log.RootSawmill.AddHandler(this);
    }

    public override void Shutdown()
    {
        // One last flush picks up lines logged during the shutdown path itself.
        Flush();
        FlushAdminFeed();
        _log.RootSawmill.RemoveHandler(this);
    }

    public override void Update(float frameTime)
    {
        if (_buffer.IsEmpty && _carry.Count == 0 && _adminQueue.IsEmpty
            && Volatile.Read(ref _dropped) == 0 && Volatile.Read(ref _adminDropped) == 0)
            return;

        // Every flush rides the interval; the buffer cap and its drop counter absorb a storm, so a
        // repeating exception costs one flush every few seconds instead of one per batch.
        if (_untilFlush > 0)
        {
            _untilFlush -= frameTime;
            return;
        }

        _untilFlush = (float) FlushInterval.TotalSeconds;
        Flush();
        FlushAdminFeed();
    }

    private void OnAdminLogExtreme(ref AdminLogExtremeEvent ev)
    {
        if (!_relay.IsEnabled(HelpSource.Errors))
            return;

        // Upstream marks player explosions Extreme past an intensity threshold; normal rounds place
        // enough charges to flood the feed. The in-game admin chat alert still fires from the source.
        if (ev.Type == LogType.Explosion)
            return;

        var message = ErrorRelayFormat.Truncate(ErrorRelayFormat.Sanitize(ev.Message), AdminLineCap);
        var line = $"**{DateTime.UtcNow:HH:mm:ss} {ev.Type}** {message}";

        if (_adminQueue.Count >= AdminQueueCap)
        {
            Interlocked.Increment(ref _adminDropped);
            return;
        }

        _adminQueue.Enqueue(line);
    }

    private void FlushAdminFeed()
    {
        var dropped = Interlocked.Exchange(ref _adminDropped, 0);

        // One line per RelayConversationText call: each line is capped at AdminLineCap, and the
        // conversation machinery chains a fresh message when the description fills, so a burst
        // group can never build one line past the embed cap and lose the whole post.
        while (_adminQueue.TryDequeue(out var line))
            _relay.RelayConversationText(HelpSource.Errors, AdminFeedId, "admin log feed", line);

        if (dropped > 0)
            _relay.RelayConversationText(HelpSource.Errors, AdminFeedId, "admin log feed", $"_({dropped} suppressed)_");
    }

    void ILogHandler.Log(string sawmillName, LogEvent message)
    {
        // A throw here would land inside whatever code was logging; write to the console directly
        // so the relay can never feed back through the sawmills.
        try { Relay(sawmillName, message); }
        catch (Exception e) { Console.Error.WriteLine($"error relay handler failed:\n{e}"); }
    }

    private void Relay(string sawmillName, LogEvent message)
    {
        if (!_relay.IsEnabled(HelpSource.Errors))
            return;

        // A failed delivery logs an error through the relay's own sawmills; relaying those would
        // feed the loop that logged it.
        if (sawmillName == RelaySawmill || sawmillName == SelfSawmill
            || sawmillName.StartsWith($"{RelaySawmill}.") || sawmillName.StartsWith($"{SelfSawmill}."))
            return;

        var level = message.Level;
        var mirrored = level >= LogEventLevel.Error
            || (level >= LogEventLevel.Information && MatchesAllowlist(sawmillName));
        if (!mirrored)
            return;

        var line = ErrorRelayFormat.From(sawmillName, level, message);

        // Urgent: Fatal means the server is likely on its way down; the queue worker can still
        // land this post during shutdown, the next flush cannot. Boot fatals buffer instead: no
        // context snapshot exists yet.
        var urgent = level == LogEventLevel.Fatal && _context != null;
        if (!urgent)
        {
            if (_buffer.Count >= BufferCap)
            {
                Interlocked.Increment(ref _dropped);
                return;
            }

            _buffer.Enqueue(line);
            return;
        }

        _relay.Post(HelpSource.Errors, new HelpPost
        {
            Title = "Server fatal",
            Description = line.Render(),
            Author = "server log",
            Context = _context,
        });
    }

    private bool MatchesAllowlist(string sawmillName)
    {
        var allow = _allow;
        foreach (var entry in allow)
        {
            if (sawmillName == entry || sawmillName.StartsWith($"{entry}."))
                return true;
        }

        return false;
    }

    private void OnSawmillsChanged(string value)
        => _allow = new HashSet<string>(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private void Flush()
    {
        if (!_relay.IsEnabled(HelpSource.Errors))
            return;

        var dropped = Interlocked.Exchange(ref _dropped, 0);
        while (_buffer.TryDequeue(out var line))
            _carry.Add(line);

        if (_carry.Count == 0 && dropped == 0)
            return;

        var context = _relay.GetContext();
        _context = context;

        for (var posts = 0; posts < MaxPostsPerFlush && _carry.Count > 0; posts++)
        {
            var description = ErrorRelayFormat.BuildDescription(_carry, BatchChars, out var overflow);
            if (dropped > 0)
            {
                description = $"{description}\n_({dropped} entries suppressed: overflow)_";
                dropped = 0;
            }

            _relay.Post(HelpSource.Errors, new HelpPost
            {
                Title = "Server errors",
                Description = description,
                Author = "server log",
                Context = context,
            });

            // BuildDescription stops at the first entry past the cap, so the unconsumed tail stays
            // in _carry for the next post or flush; entries only die at the buffer cap, counted in
            // the note. LineCap leaves headroom, so a single entry always fits one post.
            _carry.RemoveRange(0, _carry.Count - overflow);
        }
    }
}

/// <summary>
/// Raised from upstream's AdminLogManager.Add (CMU14 tag there) for every Extreme-impact admin
/// log, with the message already rendered. The error relay feeds these into its Discord
/// conversation feed.
/// </summary>
[ByRefEvent]
public record struct AdminLogExtremeEvent(LogType Type, string Message);

/// <summary>One log entry as it travels from handler to batch builder.</summary>
public sealed record ErrorLine
{
    public required string Sawmill { get; init; }

    /// <summary>Display label of the log level ("Error", "Fatal", ...).</summary>
    public required string Level { get; init; }

    public required string Text { get; init; }

    public string Render() => $"**{Level} `{Sawmill}`** {Text}";
}

/// <summary>
/// Formatting for the error relay: one Discord-ready entry per log line, and batch descriptions
/// that collapse consecutive repeats and stay under a char cap. Pure and static so tests can pin
/// the shapes.
/// </summary>
public static class ErrorRelayFormat
{
    // One entry may use this many chars; the batch builder assumes every entry fits its budget.
    public const int LineCap = 1200;

    private const string TruncatedTail = "… *(truncated)*";

    // PII scrub applied to every line before it leaves for Discord: addresses, hardware
    // identifiers and long platform ids go; ckeys and character names stay deliberately. The IPv4
    // pattern also eats rare 4-part version strings, which is cheaper than leaking an address.
    // Hwid first: its label-anchored value would otherwise confuse the numeric scanners. Order is
    // otherwise longest-match first; every pattern is linear, because player-supplied text (like
    // votekick reasons) reaches these lines.
    private static readonly Regex Hwid = new(@"\bhwid\b\s*[:=]?\s*[A-Za-z0-9+/=_-]{4,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Mac = new(@"\b[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5}\b", RegexOptions.Compiled);
    private static readonly Regex IpV4 = new(@"\b\d{1,3}(?:\.\d{1,3}){3}\b", RegexOptions.Compiled);
    private static readonly Regex IpV6 = new(@"\b(?:[0-9A-Fa-f]{1,4}:){3,}[0-9A-Fa-f]{1,4}\b|\b[0-9A-Fa-f]{1,4}::[0-9A-Fa-f:]{1,29}", RegexOptions.Compiled);
    private static readonly Regex LongId = new(@"\b\d{19,}\b", RegexOptions.Compiled);

    /// <summary>
    /// Scrubs PII from log text headed to Discord: hwid values, MAC addresses, IP addresses and
    /// 19+ digit platform ids (Discord snowflakes) become placeholder tokens. Bare `::1`-style
    /// addresses, uncompressed 1-3 group IPv6, hostnames and file paths are not matched; the
    /// console log stays the full-fidelity record.
    /// </summary>
    public static string Sanitize(string text) =>
        LongId.Replace(
            IpV6.Replace(
                IpV4.Replace(
                    Mac.Replace(
                        Hwid.Replace(text, "[hwid]"),
                        "[mac]"),
                    "[ip]"),
                "[ip]"),
            "[id]");

    /// <summary>Clamps text to the cap, marking the cut. The cap must leave room for the tail marker.</summary>
    public static string Truncate(string text, int cap) =>
        text.Length <= cap ? text : text[..(cap - TruncatedTail.Length)] + TruncatedTail;

    public static ErrorLine From(string sawmill, LogEventLevel level, LogEvent message)
    {
        var text = new StringBuilder(message.RenderMessage());
        if (message.Exception is { } exception)
        {
            // Outermost type, message and first stack frame: enough to triage, the console log
            // carries the full trace.
            text.Append($"\n{exception.GetType().Name}: {exception.Message}");
            var stack = exception.StackTrace;
            if (stack != null)
            {
                var newline = stack.IndexOf('\n');
                text.Append('\n');
                text.Append(newline < 0 ? stack : stack[..newline]);
            }
        }

        var rendered = Truncate(Sanitize(text.ToString()), LineCap);

        return new ErrorLine
        {
            Sawmill = sawmill,
            Level = LevelLabel(level),
            Text = rendered,
        };
    }

    /// <summary>
    /// Joins entries into one description, collapsing runs of identical entries into a ×N marker.
    /// Entries that do not fit the cap are counted into overflow instead of half-rendered.
    /// </summary>
    public static string BuildDescription(IReadOnlyList<ErrorLine> lines, int maxChars, out int overflow)
    {
        overflow = 0;
        var builder = new StringBuilder();
        var index = 0;
        while (index < lines.Count)
        {
            var line = lines[index];
            var count = 1;
            while (index + count < lines.Count && lines[index + count] == line)
                count++;

            var block = line.Render();
            if (count > 1)
                block += $"\n_×{count}_";

            var separator = builder.Length == 0 ? string.Empty : "\n\n";
            if (builder.Length + separator.Length + block.Length > maxChars)
            {
                overflow = lines.Count - index;
                break;
            }

            builder.Append(separator).Append(block);
            index += count;
        }

        return builder.ToString();
    }

    private static string LevelLabel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "Verbose",
        LogEventLevel.Debug => "Debug",
        LogEventLevel.Information => "Info",
        LogEventLevel.Warning => "Warn",
        LogEventLevel.Error => "Error",
        _ => "Fatal",
    };
}
