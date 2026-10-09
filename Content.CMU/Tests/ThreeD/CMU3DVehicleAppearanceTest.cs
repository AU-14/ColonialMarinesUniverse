using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DVehicleAppearanceTest
{
    [Test]
    public void RemovingHardpointAndChangingDamageStateRebuildsOnlyVisibleAssemblies()
    {
        var hull = new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.One };
        var cannon = new CMU3DModelPart { Min = new Vector3(0, -2, 1), Max = new Vector3(.1f, 0, 1.1f) };
        var damaged = new CMU3DModelPart { Min = new Vector3(0, -.3f, 1), Max = new Vector3(.1f, 0, 1.1f) };
        var model = new CMU3DModelPrototype
        {
            VehicleLayers =
            [
                new() { Rsi = "tank.rsi", State = "hull", Parts = [hull] },
                new() { Rsi = "gun.rsi", State = "ready", Parts = [cannon] },
                new() { Rsi = "gun.rsi", State = "broken", Parts = [damaged] },
            ],
        };
        var catalog = new CMU3DSceneCatalog([model], _ => null);
        Assert.That(catalog.TryVehicleParts(model, [("/Textures/tank.rsi", "hull"), ("gun.rsi", "ready")], out var armed), Is.True);
        Assert.That(armed, Is.EqualTo(new[] { hull, cannon }));
        Assert.That(catalog.TryVehicleParts(model, [("tank.rsi", "hull")], out var removed), Is.True);
        Assert.That(removed, Is.EqualTo(new[] { hull }));
        Assert.That(catalog.TryVehicleParts(model, [("tank.rsi", "hull"), ("gun.rsi", "broken")], out var broken), Is.True);
        Assert.That(broken, Is.EqualTo(new[] { hull, damaged }));
        Assert.That(armed, Is.EqualTo(new[] { hull, cannon }), "A later composition must not mutate a published assembly.");
    }

    [Test]
    public void UnknownStateOrResourceCannotSilentlyDiscardInstalledEquipment()
    {
        var model = new CMU3DModelPrototype
        {
            VehicleLayers = [new() { Rsi = "tank.rsi", State = "hull", Parts = [new()] }],
        };
        var catalog = new CMU3DSceneCatalog([model], _ => null);
        Assert.That(catalog.TryVehicleParts(model, [("tank.rsi", "hull"), ("gun.rsi", "ready")], out _), Is.False);
        Assert.That(catalog.TryVehicleParts(model, [("tank.rsi", "broken")], out _), Is.False);
        Assert.That(catalog.TryVehicleParts(model, [("other.rsi", "hull")], out _), Is.False);
    }

    [Test]
    public void MountedTurretUsesItsOwnPhysicalYawWithoutBindingDroppedOrUnrelatedItems()
    {
        var model = new CMU3DModelPrototype { VehicleTurretPrototypes = ["Cannon"], UseEntityRotation = true };
        var catalog = new CMU3DSceneCatalog([model], id => id == "Child" ? ["Cannon"] : null);
        var mounted = catalog.ResolveVehicleTurret("Cannon")!.Value;
        Assert.That(mounted.Model, Is.SameAs(model));
        Assert.That(catalog.Resolve("Cannon"), Is.Null);
        Assert.That(catalog.ResolveVehicleTurret("Child"), Is.Null);
        Assert.That(CMU3DSceneLayout.RenderYaw(mounted.Model, .63f, true, true), Is.EqualTo(.63f));
        Assert.That(CMU3DSceneLayout.RenderYaw(mounted.Model, 1.18f, true, true), Is.EqualTo(1.18f));
    }
}
