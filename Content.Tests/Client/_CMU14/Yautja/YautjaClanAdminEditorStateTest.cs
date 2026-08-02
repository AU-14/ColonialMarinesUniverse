using System.Linq;
using Content.Client._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminEditorStateTest
{
    [Test]
    public void CreateDraftSurvivesErrorAndManualRefresh()
    {
        var editor = new YautjaClanAdminEditorState();
        editor.CaptureDraft("Draft", "Draft description", "#123456");

        editor.ApplyState(State(0, null, YautjaClanAdminMutationKind.None));
        editor.ApplyState(State(0, null, YautjaClanAdminMutationKind.None));

        Assert.Multiple(() =>
        {
            Assert.That(editor.EditingClanId, Is.Null);
            Assert.That(editor.Name, Is.EqualTo("Draft"));
            Assert.That(editor.Description, Is.EqualTo("Draft description"));
            Assert.That(editor.Color, Is.EqualTo("#123456"));
        });
    }

    [Test]
    public void SuccessfulCreateClearsCreateDraft()
    {
        var editor = new YautjaClanAdminEditorState();
        editor.CaptureDraft("Draft", "Draft description", "#123456");

        editor.ApplyState(State(
            1,
            7,
            YautjaClanAdminMutationKind.Created,
            Clan(7, "Saved", "Saved description", "#123456")));

        Assert.Multiple(() =>
        {
            Assert.That(editor.EditingClanId, Is.Null);
            Assert.That(editor.Name, Is.Empty);
            Assert.That(editor.Description, Is.Empty);
            Assert.That(editor.Color, Is.Empty);
        });
    }

    [Test]
    public void DraftSurvivesErrorsThenSynchronizesOrResetsOnSuccessfulMutation()
    {
        var editor = new YautjaClanAdminEditorState();
        var original = Clan(3, "Original", "Original description", "#111111");
        editor.ApplyState(State(0, null, YautjaClanAdminMutationKind.None, original));
        editor.BeginEdit(original);
        editor.CaptureDraft("Draft", "Draft description", "#222222");

        editor.ApplyState(State(0, null, YautjaClanAdminMutationKind.None, original));
        Assert.That(editor.Name, Is.EqualTo("Draft"));

        var updated = Clan(3, "Saved", "Saved description", "#333333");
        editor.ApplyState(State(1, 3, YautjaClanAdminMutationKind.Updated, updated));
        Assert.Multiple(() =>
        {
            Assert.That(editor.EditingClanId, Is.EqualTo(3));
            Assert.That(editor.Name, Is.EqualTo("Saved"));
            Assert.That(editor.Description, Is.EqualTo("Saved description"));
            Assert.That(editor.Color, Is.EqualTo("#333333"));
        });

        editor.ApplyState(State(2, 3, YautjaClanAdminMutationKind.Deleted));
        Assert.Multiple(() =>
        {
            Assert.That(editor.EditingClanId, Is.Null);
            Assert.That(editor.Name, Is.Empty);
            Assert.That(editor.Description, Is.Empty);
            Assert.That(editor.Color, Is.Empty);
        });
    }

    private static YautjaClanAdminClanState Clan(
        int id,
        string name,
        string description,
        string color)
    {
        return new(id, name, description, 0, color, 0);
    }

    private static YautjaClanAdminEuiState State(
        long version,
        int? clanId,
        YautjaClanAdminMutationKind kind,
        params YautjaClanAdminClanState[] clans)
    {
        return new(clans.ToList(), "", "", "", version, clanId, kind);
    }
}
