using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffect;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems
{
    public sealed partial class StutteringSystem : SharedStutteringSystem
    {
        [Dependency] private StatusEffectQuerySystem _statusEffectsSystem = default!;
        [Dependency] private IRobustRandom _random = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<StutteringAccentComponent, AccentGetEvent>(OnAccent);
        }

        public override void DoStutter(EntityUid uid, TimeSpan time, bool refresh, StatusEffectsComponent? status = null)
        {
            if (!Resolve(uid, ref status, false))
                return;

            _statusEffectsSystem.TryAddStatusEffect<StutteringAccentComponent>(uid, StutterKey, time, refresh, status);
        }

        private void OnAccent(EntityUid uid, StutteringAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message, component);
        }

        public string Accentuate(string message, StutteringAccentComponent component)
        {
            var finalMessage = new StringBuilder(message.Length);
            foreach (var letter in message)
            {
                if (!IsStutterCharacter(letter) || !_random.Prob(component.MatchRandomProb))
                {
                    finalMessage.Append(letter);
                    continue;
                }

                if (_random.Prob(component.FourRandomProb))
                    AppendStutter(finalMessage, letter, 4);
                else if (_random.Prob(component.ThreeRandomProb))
                    AppendStutter(finalMessage, letter, 3);
                else if (_random.Prob(component.CutRandomProb))
                    continue;
                else
                    AppendStutter(finalMessage, letter, 2);
            }

            return finalMessage.ToString();
        }

        private static bool IsStutterCharacter(char letter)
        {
            return char.ToLowerInvariant(letter) is
                >= 'b' and <= 'd' or
                >= 'f' and <= 'h' or
                >= 'j' and <= 'n' or
                >= 'p' and <= 't' or
                >= 'v' and <= 'z';
        }

        private static void AppendStutter(StringBuilder builder, char letter, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append('-');

                builder.Append(letter);
            }
        }
    }
}
