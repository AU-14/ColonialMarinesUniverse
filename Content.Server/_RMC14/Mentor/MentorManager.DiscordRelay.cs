using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Content.Shared.CCVar;

namespace Content.Server._RMC14.Mentor;

public sealed partial class MentorManager
{
    private const ushort MessageLengthCap = 3500;
    private const string TooLongText = "... **(msg too long)**";

    private readonly ConcurrentQueue<MentorHelpWebhookPayload> _pendingMentorHelpPayloads = new();
    private readonly HttpClient _mentorHelpHttpClient = new();

    private GameTicker _ticker = default!;
    private string _mentorHelpWebhookUrl = string.Empty;
    private int _mentorHelpQueueProcessorStarted;

    private sealed class MentorHelpWebhookPayload
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("embeds")]
        public List<MentorHelpWebhookEmbed>? Embeds { get; set; } = new();
    }

    private sealed class MentorHelpWebhookEmbed
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("color")]
        public int? Color { get; set; }

        [JsonPropertyName("footer")]
        public MentorHelpWebhookEmbedFooter? Footer { get; set; }
    }

    private sealed class MentorHelpWebhookEmbedFooter
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private void InitializeDiscordRelay()
    {
        if (_config.IsCVarRegistered(CCVars.DiscordMentorHelpWebhook.Name))
            _config.OnValueChanged(CCVars.DiscordMentorHelpWebhook, OnMentorHelpWebhookChanged, true);
        else
            OnMentorHelpWebhookChanged(string.Empty);
    }

    private void OnMentorHelpWebhookChanged(string url)
    {
        _mentorHelpWebhookUrl = url;
    }

    private void QueueMentorHelpWebhook(
        string destinationName,
        string? authorName,
        string message,
        bool create)
    {
        if (!create || string.IsNullOrWhiteSpace(_mentorHelpWebhookUrl))
            return;

        var cappedMessage = message.Length > MessageLengthCap
            ? message[..(MessageLengthCap - TooLongText.Length)] + TooLongText
            : message;
        var payload = GenerateMentorHelpPayload(destinationName, authorName, cappedMessage);
        _pendingMentorHelpPayloads.Enqueue(payload);
        StartMentorHelpQueueProcessor();
    }

    private MentorHelpWebhookPayload GenerateMentorHelpPayload(
        string destinationName,
        string? authorName,
        string text)
    {
        var username = authorName != null ? $"{authorName} → {destinationName}" : $"System → {destinationName}";

        _ticker ??= _entMan.System<GameTicker>();
        var roundId = _ticker.RoundId;
        var roundState = _ticker.RunLevel switch
        {
            GameRunLevel.PreRoundLobby => "Lobby",
            GameRunLevel.InRound => $"Round {roundId}",
            GameRunLevel.PostRound => $"Post-round {roundId}",
            _ => "Unknown",
        };

        return new MentorHelpWebhookPayload
        {
            Username = username,
            Embeds =
            [
                new MentorHelpWebhookEmbed
                {
                    Description = text,
                    Color = 0xFFA500,
                    Footer = new MentorHelpWebhookEmbedFooter { Text = $"Mentor Help – {roundState}" },
                },
            ],
        };
    }

    private async Task PostMentorHelpWebhook(MentorHelpWebhookPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _mentorHelpHttpClient.PostAsync(_mentorHelpWebhookUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _log.RootSawmill.Error(
                    $"MentorHelp webhook failed: {(int) response.StatusCode} {response.StatusCode}\n{body}");
            }
        }
        catch (Exception e)
        {
            _log.RootSawmill.Error($"MentorHelp webhook error: {e}");
        }
    }

    private void StartMentorHelpQueueProcessor()
    {
        if (Interlocked.Exchange(ref _mentorHelpQueueProcessorStarted, 1) == 1)
            return;

        Task.Run(ProcessMentorHelpQueueAsync);
    }

    private async Task ProcessMentorHelpQueueAsync()
    {
        while (true)
        {
            while (_pendingMentorHelpPayloads.TryDequeue(out var payload))
            {
                await PostMentorHelpWebhook(payload);
                await Task.Delay(1200);
            }

            _mentorHelpQueueProcessorStarted = 0;

            if (_pendingMentorHelpPayloads.IsEmpty ||
                Interlocked.Exchange(ref _mentorHelpQueueProcessorStarted, 1) == 1)
            {
                return;
            }
        }
    }
}
