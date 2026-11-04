using Content.Shared.CMU14.Crayon;

namespace Content.Client.Crayon.UI;

public sealed partial class CrayonBoundUserInterface
{
    private void SelectCustom(CMUCrayonSymbol symbol)
    {
        SendMessage(new CMUCrayonSelectSymbolMessage(symbol));
    }
}
