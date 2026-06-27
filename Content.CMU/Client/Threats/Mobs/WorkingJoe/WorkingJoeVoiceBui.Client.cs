using Content.Shared._CMU14.Threats.Mobs.WorkingJoe;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Threats.Mobs.WorkingJoe;

public sealed partial class WorkingJoeVoiceBui : BoundUserInterface
{
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _resource = default!;
    private WorkingJoeVoiceFavorites? _favorites;

    private WorkingJoeVoiceWindow? _window;

    public WorkingJoeVoiceBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { IoCManager.InjectDependencies(this); }

    protected override void Open()
    {
        base.Open();

        _favorites ??= new(_resource);

        _window = new(_favorites);
        _window.OnClose += Close;
        _window.OnLineSelected += OnLineSelected;

        var lines = new List<WorkingJoeVoiceLine>();
        foreach (EmotePrototype emote in _proto.EnumeratePrototypes<EmotePrototype>())
        {
            if (emote.Whitelist?.Tags == null)
                continue;
            if (!emote.Whitelist.Tags.Contains("WorkingJoe"))
                continue;

            lines.Add(new()
            {
                EmoteId = emote.ID,
                DisplayName = _loc.GetString(emote.Name),
                Category = emote.Category.ToString()
            });
        }

        _window.SetLines(lines);
        _window.OpenCentered();
    }

    private void OnLineSelected(string emoteId) { SendMessage(new WorkingJoePlayLineMessage(emoteId)); }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
