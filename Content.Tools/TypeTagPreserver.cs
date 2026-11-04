using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Content.Tools;

public sealed class TypeTagPreserver(IEmitter emitter) : IEmitter
{
    private IEmitter Emitter { get; } = emitter;

    public void Emit(ParsingEvent @event)
    {
        if (@event is MappingStart mapping)
        {
            @event = new MappingStart(mapping.Anchor, mapping.Tag, false, mapping.Style, mapping.Start, mapping.End);
        }

        Emitter.Emit(@event);
    }
}
