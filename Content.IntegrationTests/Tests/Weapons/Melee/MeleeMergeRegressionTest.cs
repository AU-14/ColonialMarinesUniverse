using Content.Client.Weapons.Melee;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests.Weapons.Melee;

[TestFixture]
[TestOf(typeof(MeleeWeaponSystem))]
public sealed class MeleeMergeRegressionTest
{
    [Test]
    public void ClientStopPredicateUnionsUpstreamHoldAndWideAttackInputs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MeleeWeaponSystem.ShouldStopAttack(
                    true,
                    BoundKeyState.Down,
                    BoundKeyState.Down,
                    BoundKeyState.Down,
                    false),
                Is.True,
                "AutoAttack stops even while all attack keys remain down");
            Assert.That(MeleeWeaponSystem.ShouldStopAttack(
                    false,
                    BoundKeyState.Down,
                    BoundKeyState.Down,
                    BoundKeyState.Down,
                    true),
                Is.True,
                "hold-to-attack CVar stops through the inherited path with keys down");
            Assert.That(MeleeWeaponSystem.ShouldStopAttack(
                    false,
                    BoundKeyState.Up,
                    BoundKeyState.Up,
                    BoundKeyState.Up,
                    false),
                Is.True,
                "releasing use, alternate, and RMC wide inputs stops attacking");
            Assert.That(MeleeWeaponSystem.ShouldStopAttack(
                    false,
                    BoundKeyState.Up,
                    BoundKeyState.Up,
                    BoundKeyState.Down,
                    false),
                Is.False,
                "the RMC wide-swing key alone keeps the attack active");
        });
    }
}
