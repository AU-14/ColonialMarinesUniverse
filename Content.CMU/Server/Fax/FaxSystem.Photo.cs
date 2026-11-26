using Content.Shared.CMU14.Photography;
using Content.Shared.Fax.Components;
using Content.Shared.Labels.Components;
using Content.Shared.NameModifier.Components;
using Content.Shared.Paper;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Server.Fax;

public sealed partial class FaxSystem
{
    private const string FaxPhotoData = "cmu_fax_photo";

    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IGameTiming _timing = default!;

    private FaxPrintout? ScanDocument(EntityUid document, string? fallbackPrototype = null)
    {
        var metadata = MetaData(document);
        var name = CompOrNull<NameModifierComponent>(document)?.BaseName ?? metadata.EntityName;
        var label = CompOrNull<LabelComponent>(document)?.CurrentLabel;
        var prototype = metadata.EntityPrototype?.ID ?? fallbackPrototype;

        if (TryComp<CMUPhotoComponent>(document, out var photo))
        {
            return new FaxPrintout(string.Empty, name, label, prototype)
            {
                Photo = _serialization.CreateCopy(photo, notNullableOverride: true),
            };
        }

        if (!TryComp<PaperComponent>(document, out var paper))
            return null;

        return new FaxPrintout(paper.Content, name, label, prototype, paper.StampState, paper.StampedBy, paper.EditingDisabled);
    }

    private EntityUid SpawnFaxDocument(EntityUid fax, FaxMachineComponent machine, FaxPrintout printout)
    {
        var coords = Transform(fax).Coordinates;
        if (printout.Photo is not { } snapshot)
            return Spawn(machine.PrintPaperId, coords);

        var printed = Spawn("CMUPhoto", coords);
        var photo = Comp<CMUPhotoComponent>(printed);
        _serialization.CopyTo(snapshot, ref photo, notNullableOverride: true);
        photo.Printed = _timing.CurTime;
        Dirty(printed, photo);
        return printed;
    }
}
