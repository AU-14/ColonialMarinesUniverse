using System.Collections.Generic;
using Content.Shared.Traits;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [Test]
    public async Task RepeatedTraitOnlyEditsPreserveUniqueTraits()
    {
        var db = GetDb(Server);
        var user = NewUserId();
        var profile = CharlieCharlieson().WithJobPriorities(new Dictionary<ProtoId<JobPrototype>, JobPriority>());
        // Trait-only profiles also need removal flushing; there may be no jobs to remove.
        typeof(Content.Shared.Preferences.HumanoidCharacterProfile)
            .GetField("_traitPreferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(profile, new HashSet<ProtoId<TraitPrototype>> { "TestTraitA", "TestTraitB" });
        await db.InitPrefsAsync(user, profile);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => db.SaveCharacterSlotAsync(user, profile, 0))));
        var prefs = await db.GetPlayerPreferencesAsync(user);
        Assert.That(prefs!.Profiles.Single().Traits.Select(t => t.TraitName),
            Is.EquivalentTo(new[] { "TestTraitA", "TestTraitB" }));
    }

    [Test]
    public async Task DeletingSelectedCharacterAndStaleSelectionKeepAnExistingSelection()
    {
        var db = GetDb(Server);
        var user = NewUserId();
        await db.InitPrefsAsync(user, CharlieCharlieson());
        await db.SaveCharacterSlotAsync(user, CharlieCharlieson(), 1);
        await db.SaveSelectedCharacterIndexAsync(user, 1);
        await Task.WhenAll(db.SaveCharacterSlotAsync(user, null, 1), db.SaveSelectedCharacterIndexAsync(user, 1));
        var prefs = await db.GetPlayerPreferencesAsync(user);
        Assert.That(prefs!.SelectedCharacterSlot, Is.EqualTo(0));
        Assert.That(prefs.Profiles.Select(p => p.Slot), Is.EqualTo(new[] { 0 }));

        await db.SaveCharacterSlotAsync(user, null, 0);
        prefs = await db.GetPlayerPreferencesAsync(user);
        Assert.That(prefs!.Profiles.Select(p => p.Slot), Does.Contain(prefs.SelectedCharacterSlot),
            "A stale delete must not remove the last selectable character.");
    }

    [Test]
    public async Task DeleteAndSelectValidatesTheReplacementBeforeDeleting()
    {
        var db = GetDb(Server);
        var user = NewUserId();
        await db.InitPrefsAsync(user, CharlieCharlieson());
        await db.SaveCharacterSlotAsync(user, CharlieCharlieson(), 1);
        await db.DeleteSlotAndSetSelectedIndex(user, 0, 7);
        var prefs = await db.GetPlayerPreferencesAsync(user);
        Assert.That(prefs!.Profiles, Has.Count.EqualTo(2));
        Assert.That(prefs.SelectedCharacterSlot, Is.Zero);

        await db.DeleteSlotAndSetSelectedIndex(user, 0, 1);
        prefs = await db.GetPlayerPreferencesAsync(user);
        Assert.That(prefs!.SelectedCharacterSlot, Is.EqualTo(1));
        Assert.That(prefs.Profiles.Single().Slot, Is.EqualTo(1));
    }
}
