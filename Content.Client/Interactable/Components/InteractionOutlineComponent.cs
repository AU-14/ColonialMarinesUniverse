using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Interactable.Components
{
    [RegisterComponent]
    public sealed partial class InteractionOutlineComponent : Component
    {
        private static readonly ProtoId<ShaderPrototype> ShaderInRange = "SelectionOutlineInrange";
        private static readonly ProtoId<ShaderPrototype> ShaderOutOfRange = "SelectionOutline";

        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IEntityManager _entMan = default!;
        private SpriteSystem Sprite => _entMan.System<SpriteSystem>(); // CMU14: systems live in the ESM collection, not root IoC

        private const float DefaultWidth = 1;
        private const string ShaderId = "InteractionOutline"; // CMU14: keyed id required by the v288 multi post-shader API

        private bool _inRange;
        private ShaderInstance? _inRangeShader;
        private ShaderInstance? _outOfRangeShader;
        private int _lastRenderScale;

        public void OnMouseEnter(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            _lastRenderScale = renderScale;
            _inRange = inInteractionRange;
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                // CMU14: keyed set coexists with other post-shaders; the PostShader == null guard only fed the single-slot API
                Sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ShaderId, GetShader(inInteractionRange, renderScale)));
            }
        }

        public void OnMouseLeave(EntityUid uid)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                // CMU14: keyed removal only drops our own entry
                // if (IsOutlineShader(sprite.PostShader))
                //     sprite.PostShader = null;
                Sprite.RemovePostShader(sprite, ShaderId);
                sprite.RenderOrder = 0;
            }
        }

        public void UpdateInRange(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite)
                && Sprite.HasPostShader(sprite, ShaderId) // CMU14: keyed lookup replaces IsOutlineShader
                && (inInteractionRange != _inRange || _lastRenderScale != renderScale))
            {
                _inRange = inInteractionRange;
                _lastRenderScale = renderScale;

                Sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ShaderId, GetShader(_inRange, _lastRenderScale))); // CMU14
            }
        }

        public void OnShutdown(EntityUid uid)
        {
            OnMouseLeave(uid);
            _inRangeShader?.Dispose();
            _outOfRangeShader?.Dispose();
            _inRangeShader = null;
            _outOfRangeShader = null;
        }

        // CMU14: obsolete with keyed post-shader ids
        // private bool IsOutlineShader(ShaderInstance? shader)
        // {
        //     return shader != null &&
        //            (ReferenceEquals(shader, _inRangeShader) ||
        //             ReferenceEquals(shader, _outOfRangeShader));
        // }

        private ShaderInstance GetShader(bool inRange, int renderScale)
        {
            var instance = inRange
                ? _inRangeShader ??= MakeNewShader(ShaderInRange)
                : _outOfRangeShader ??= MakeNewShader(ShaderOutOfRange);

            instance.SetParameter("outline_width", DefaultWidth * renderScale);
            return instance;
        }

        private ShaderInstance MakeNewShader(ProtoId<ShaderPrototype> shaderName)
        {
            var instance = _prototypeManager.Index(shaderName).InstanceUnique();
            return instance;
        }
    }
}
