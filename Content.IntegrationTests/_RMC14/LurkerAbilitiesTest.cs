using System.Numerics;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Crippling;
using Content.Shared._RMC14.Xenonids.Invisibility;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class LurkerAbilitiesTest
{
    [Test]
    public async Task InvisibilityToggleKeepsCooldownAfterActionPerformed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;
        EntityUid actionUid = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var actions = entMan.System<SharedActionsSystem>();
                xeno = entMan.SpawnEntity("CMXenoLurker", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                var action = GetAction(entMan, actions, xeno, "ActionXenoTurnInvisible");
                actionUid = action;

                actions.PerformAction(xeno, action);

                Assert.That(entMan.HasComponent<XenoActiveInvisibleComponent>(xeno), Is.True);
                Assert.That(action.Comp.Toggled, Is.True);
                Assert.That(action.Comp.Cooldown, Is.Not.Null);
                Assert.That(action.Comp.Cooldown!.Value.End - server.Timing.CurTime,
                    Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(0.4)));

                actions.PerformAction(xeno, action);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var action = entMan.GetComponent<ActionComponent>(actionUid);
                Assert.That(entMan.HasComponent<XenoActiveInvisibleComponent>(xeno), Is.False);
                Assert.That(action.Toggled, Is.False);
                Assert.That(action.Cooldown, Is.Not.Null);
                Assert.That(action.Cooldown!.Value.End - server.Timing.CurTime,
                    Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.5)));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(xeno))
                    server.EntMan.DeleteEntity(xeno);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CripplingStrikeResetsWeaponAndUserMeleeCooldowns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var actions = entMan.System<SharedActionsSystem>();
                xeno = entMan.SpawnEntity("CMXenoLurker", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                var action = GetAction(entMan, actions, xeno, "ActionXenoCripplingStrike");
                var weapon = entMan.GetComponent<MeleeWeaponComponent>(xeno);
                var userCooldown = entMan.EnsureComponent<RMCMeleeUserCooldownComponent>(xeno);
                var originalCooldown = server.Timing.CurTime + TimeSpan.FromSeconds(1);
                weapon.NextAttack = originalCooldown;
                userCooldown.NextAttack = originalCooldown;

                actions.PerformAction(xeno, action);

                Assert.That(entMan.HasComponent<XenoActiveCripplingStrikeComponent>(xeno), Is.True);
                Assert.That(entMan.HasComponent<MeleeResetComponent>(xeno), Is.True);
                Assert.That(weapon.NextAttack, Is.LessThanOrEqualTo(server.Timing.CurTime));
                Assert.That(userCooldown.NextAttack, Is.LessThanOrEqualTo(server.Timing.CurTime));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(xeno))
                    server.EntMan.DeleteEntity(xeno);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> GetAction(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid performer,
        string prototype)
    {
        foreach (var action in actions.GetActions(performer))
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype)
                return action;
        }

        Assert.Fail($"Expected {performer} to have action {prototype}");
        throw new InvalidOperationException();
    }
}
