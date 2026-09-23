using System.Numerics;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>The classic canvas stores coordinates at three pixels per tile with inverted Y.</summary>
public static class CMUReconDrawingCoordinates
{
    public static Vector2 ToWorld(Vector2 point, Vector2i min, Vector2i max) =>
        new(point.X / 3 + min.X, max.Y - point.Y / 3);

    public static Vector2 ToCanvas(Vector2 point, Vector2i min, Vector2i max) =>
        new((point.X - min.X) * 3, (max.Y - point.Y) * 3);

    public static Color InkColor(CMUReconInk ink) => Color.FromHex(ink switch
    {
        CMUReconInk.Red => "#FF7775", CMUReconInk.Blue => "#76BFFF", CMUReconInk.Green => "#8FE5A2",
        CMUReconInk.White => "#F2EEDF", CMUReconInk.Purple => "#CD9DF5", _ => "#F5D47D",
    });
}
