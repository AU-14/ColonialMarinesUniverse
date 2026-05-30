using Content.Shared._AU14.WorkingJoe;
using Content.Shared._CMU14.Wendigo;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Wendigo;

public sealed partial class WendigoVoiceBui : BoundUserInterface
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private WendigoVoiceWindow? _window;

    public WendigoVoiceBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = new _CMU14.Wendigo.WendigoVoiceWindow();
        _window.OnClose += Close;
        _window.OnLineSelected += OnLineSelected;

        // Build list from all emote prototypes tagged for WorkingJoe
        var lines = new List<WendigoVoiceLine>();
        foreach (var emote in _proto.EnumeratePrototypes<EmotePrototype>())
        {
            if (emote.Whitelist?.Tags == null)
                continue;
            if (!emote.Whitelist.Tags.Contains("Wendigo"))
                continue;

            lines.Add(new WendigoVoiceLine
            {
                EmoteId = emote.ID,
                DisplayName = _loc.GetString(emote.Name),
                Category = emote.Category.ToString(),
            });
        }

        _window.SetLines(lines);
        _window.OpenCentered();
    }

    private void OnLineSelected(string emoteId)
    {
        SendMessage(new WendigoPlayLineMessage(emoteId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
