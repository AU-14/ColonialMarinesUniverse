using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Guidebook;

/// <summary>
/// Sends all extracted prototype data needed by GuidebookDataSystem.
/// Raised by the server directed at newly-connected clients.
/// Also raised by the server at ALL clients when prototype data is hot-reloaded.
/// </summary>
[Serializable, NetSerializable]
public sealed class UpdateGuidebookDataEvent : EntityEventArgs
{
    public GuidebookData Data;

    public UpdateGuidebookDataEvent(GuidebookData data)
    {
        Data = data;
    }
}

/// <summary>
/// Raised by the server at a specific client to open guidebook entries.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenGuidebookEvent : EntityEventArgs
{
    public List<ProtoId<GuideEntryPrototype>> Guides { get; }

    public OpenGuidebookEvent(List<ProtoId<GuideEntryPrototype>> guides)
    {
        Guides = guides;
    }
}
