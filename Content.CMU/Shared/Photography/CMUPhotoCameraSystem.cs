using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared.CMU14.Photography;

/// <summary>Frame size selection and examine text for photo cameras and prints.</summary>
public sealed partial class CMUPhotoCameraSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUPhotoCameraComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUPhotoCameraComponent, ExaminedEvent>(OnCameraExamined);
        SubscribeLocalEvent<CMUPhotoComponent, ExaminedEvent>(OnPhotoExamined);
    }

    private void OnGetVerbs(Entity<CMUPhotoCameraComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Sizes.Length == 0)
            return;

        var user = args.User;
        if (ent.Comp.RequiresPhotoMode)
        {
            var photoMode = !ent.Comp.PhotoMode;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(photoMode ? "cmu-photo-camera-verb-photo-mode" : "cmu-photo-camera-verb-video-mode"),
                Priority = 1,
                Act = () => SetPhotoMode(ent, photoMode, user),
            });

            if (!ent.Comp.PhotoMode)
                return;
        }

        var next = NextSize(ent.Comp);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-photo-camera-verb-frame", ("size", next)),
            Act = () => SetSize(ent, next, user),
        });
    }

    private static int NextSize(CMUPhotoCameraComponent camera)
    {
        var index = Array.IndexOf(camera.Sizes, camera.Size);
        return camera.Sizes[(index + 1) % camera.Sizes.Length];
    }

    public void SetSize(Entity<CMUPhotoCameraComponent> ent, int size, EntityUid? user)
    {
        ent.Comp.Size = size;
        Dirty(ent);
        if (user != null)
            _popup.PopupClient(Loc.GetString("cmu-photo-camera-frame-set", ("size", size)), ent, user.Value);
    }

    /// <summary>Switches a camera that needs it between taking photos and its other use. Broadcasting carries on.</summary>
    public void SetPhotoMode(Entity<CMUPhotoCameraComponent> ent, bool photoMode, EntityUid? user)
    {
        ent.Comp.PhotoMode = photoMode;
        Dirty(ent);
        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString(photoMode ? "cmu-photo-camera-photo-mode-on" : "cmu-photo-camera-photo-mode-off"),
                ent, user.Value);
        }
    }

    private void OnCameraExamined(Entity<CMUPhotoCameraComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.RequiresPhotoMode)
        {
            args.PushMarkup(Loc.GetString(ent.Comp.PhotoMode
                ? "cmu-photo-camera-examine-photo-mode"
                : "cmu-photo-camera-examine-video-mode"));
        }

        args.PushMarkup(Loc.GetString("cmu-photo-camera-examine", ("size", ent.Comp.Size)));
        args.PushMarkup(Loc.GetString("cmu-photo-camera-shots", ("shots", ent.Comp.Shots), ("max", ent.Comp.MaxShots)));
    }

    private void OnPhotoExamined(Entity<CMUPhotoComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-photo-examine",
            ("size", ent.Comp.Size),
            ("photographer", ent.Comp.Photographer)));
        args.PushMarkup(Describe(ent.Comp));
    }

    /// <summary>The print's contents as one line of plain text, or a note that nothing could be made out.</summary>
    public static string Describe(CMUPhotoComponent photo)
    {
        if (photo.Contents.Count == 0)
            return Robust.Shared.Localization.Loc.GetString("cmu-photo-shows-nothing");

        var escaped = new List<string>();
        foreach (var item in photo.Contents)
            escaped.Add(Robust.Shared.Utility.FormattedMessage.EscapeText(item));

        return Robust.Shared.Localization.Loc.GetString("cmu-photo-shows", ("contents", string.Join(", ", escaped)));
    }
}
