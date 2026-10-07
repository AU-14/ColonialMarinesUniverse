using Content.Shared.Storage;

namespace Content.Shared.Item;

public abstract partial class SharedItemSystem
{
    private static IReadOnlyList<Box2i> CMUAdjustItemShape(IReadOnlyList<Box2i> shapes, Angle rotation, Vector2i position)
    {
        // Shapes are read-only at every placement and drawing call site. The identity
        // transform can reuse the source, including storage-specific size overrides.
        if (rotation == Angle.Zero && position == Vector2i.Zero)
            return shapes;

        var adjusted = new Box2i[shapes.Count];
        if (rotation == Angle.Zero)
        {
            for (var i = 0; i < shapes.Count; i++)
                adjusted[i] = shapes[i].Translated(position);
            return adjusted;
        }

        var boundingShape = shapes.GetBoundingBox();
        var boundingCenter = ((Box2) boundingShape).Center;
        var transform = Matrix3Helpers.CreateTransform(boundingCenter, rotation);
        var drift = boundingShape.BottomLeft - transform.TransformBox(boundingShape).BottomLeft;
        for (var i = 0; i < shapes.Count; i++)
        {
            var transformed = transform.TransformBox(shapes[i]).Translated(drift);
            adjusted[i] = new Box2i(transformed.BottomLeft.Floored(), transformed.TopRight.Floored()).Translated(position);
        }

        return adjusted;
    }
}
