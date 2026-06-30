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

        private const float DefaultWidth = 1;

        private bool _inRange;
        private ShaderInstance? _inRangeShader;
        private ShaderInstance? _outOfRangeShader;
        private int _lastRenderScale;

        public void OnMouseEnter(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            _lastRenderScale = renderScale;
            _inRange = inInteractionRange;
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite) && sprite.PostShader == null)
            {
                sprite.PostShader = GetShader(inInteractionRange, renderScale);
            }
        }

        public void OnMouseLeave(EntityUid uid)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                if (IsOutlineShader(sprite.PostShader))
                    sprite.PostShader = null;
                sprite.RenderOrder = 0;
            }
        }

        public void UpdateInRange(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite)
                && IsOutlineShader(sprite.PostShader)
                && (inInteractionRange != _inRange || _lastRenderScale != renderScale))
            {
                _inRange = inInteractionRange;
                _lastRenderScale = renderScale;

                sprite.PostShader = GetShader(_inRange, _lastRenderScale);
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

        private bool IsOutlineShader(ShaderInstance? shader)
        {
            return shader != null &&
                   (ReferenceEquals(shader, _inRangeShader) ||
                    ReferenceEquals(shader, _outOfRangeShader));
        }

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
