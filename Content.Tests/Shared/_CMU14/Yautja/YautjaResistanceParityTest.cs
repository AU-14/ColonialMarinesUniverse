using System;
using System.IO;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Damage.Prototypes;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Shared._CMU14.Yautja;

[TestFixture]
public sealed class YautjaResistanceParityTest : ContentUnitTest
{
    private IPrototypeManager _prototypes = default!;

    [OneTimeSetUp]
    public void SetUp()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _prototypes.Initialize();

        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "Resources",
            "Prototypes",
            "_CMU14",
            "Threats",
            "Yautja",
            "Species",
            "damage.yml"));
        _prototypes.LoadString(File.ReadAllText(path));
        _prototypes.ResolveResults();
    }

    [Test]
    public void DamageModifierSetMatchesCmss13SpeciesValues()
    {
        var modifiers = _prototypes.Index<DamageModifierSetPrototype>("CMUYautja").Coefficients;

        Assert.That(modifiers, Has.Count.EqualTo(5));
        Assert.That(modifiers["Blunt"], Is.EqualTo(0.28f));
        Assert.That(modifiers["Slash"], Is.EqualTo(0.28f));
        Assert.That(modifiers["Piercing"], Is.EqualTo(0.28f));
        Assert.That(modifiers["Heat"], Is.EqualTo(0.65f));
        Assert.That(modifiers["Poison"], Is.EqualTo(0f));
        Assert.That(modifiers.ContainsKey("Shock"), Is.False);
        Assert.That(modifiers.ContainsKey("Cold"), Is.False);
        Assert.That(modifiers.ContainsKey("Caustic"), Is.False);
        Assert.That(modifiers.ContainsKey("Radiation"), Is.False);
        Assert.That(modifiers.ContainsKey("Bloodloss"), Is.False);
        Assert.That(modifiers.ContainsKey("Asphyxiation"), Is.False);
    }

    [Test]
    public void StunResistanceMatchesCmss13SpeciesValue()
    {
        Assert.That(new YautjaComponent().StunResistance, Is.EqualTo(1.5f));
    }
}
