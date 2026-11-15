using System.Numerics;
using Content.Client._RMC14.Storage;
using Content.Client.CMU14.Storage;
using Robust.Client.Graphics;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed partial class ItemGridPiece
{
    private CMUIconLabelPresentation? _cmuIconLabel;

    private void CMUDrawIconLabel(Vector2 position, DrawingHandleScreen handle)
    {
        _entityManager.System<RMCIconLabelSystem>().DrawStorage(Entity, UIScale, position, handle, ref _cmuIconLabel);
    }
}
