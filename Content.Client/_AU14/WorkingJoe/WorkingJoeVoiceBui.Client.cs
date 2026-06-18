using Content.Shared._AU14.WorkingJoe;
using Content.Shared.Chat.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.WorkingJoe;

public sealed partial class WorkingJoeVoiceBui : BoundUserInterface
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IResourceManager _resource = default!;

    private WorkingJoeVoiceWindow? _window;
    private WorkingJoeVoiceFavorites? _favorites;

    public WorkingJoeVoiceBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _favorites ??= new WorkingJoeVoiceFavorites(_resource);

        _window = new WorkingJoeVoiceWindow(_favorites);
        _window.OnClose += Close;
        _window.OnLineSelected += OnLineSelected;

        var lines = new List<WorkingJoeVoiceLine>();
        foreach (var emote in _proto.EnumeratePrototypes<EmotePrototype>())
        {
            if (emote.Whitelist?.Tags == null)
                continue;
            if (!emote.Whitelist.Tags.Contains("WorkingJoe"))
                continue;

            lines.Add(new WorkingJoeVoiceLine
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
        SendMessage(new WorkingJoePlayLineMessage(emoteId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
