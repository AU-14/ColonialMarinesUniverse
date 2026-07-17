using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Paper;

[Serializable, NetSerializable]
public enum UniversalPaperToolUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class UniversalPaperToolBuiState : BoundUserInterfaceState
{
    public List<UniversalPaperToolTemplateEntry> Templates;
    public bool HasPaper;

    public UniversalPaperToolBuiState(List<UniversalPaperToolTemplateEntry> templates, bool hasPaper)
    {
        Templates = templates;
        HasPaper = hasPaper;
    }
}

[Serializable, NetSerializable]
public sealed record UniversalPaperToolTemplateEntry(
    string Prototype,
    string Name,
    string Description);

[Serializable, NetSerializable]
public sealed class UniversalPaperToolPrintMessage : BoundUserInterfaceMessage
{
    public string Prototype;

    public UniversalPaperToolPrintMessage(string prototype)
    {
        Prototype = prototype;
    }
}
