using System.Numerics;
using Content.Client.IoC;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private sealed partial class RevolverStatusControl
    {
        private static readonly Color LoadedA = Color.FromHex("#b68f0e");
        private static readonly Color LoadedB = Color.FromHex("#d7df60");
        private static readonly Color SpentA = Color.FromHex("#b50e25");
        private static readonly Color SpentB = Color.FromHex("#d3745f");
        private static readonly Color MissingA = Color.FromHex("#000000");
        private static readonly Color MissingB = Color.FromHex("#222222");
        private (Control Box, TextureRect Bullet)[] _cmuRows = [];
        private bool?[] _cmuChambers = [];
        private readonly TextureRect _cmuActiveChamber = new()
        {
            TextureScale = new Vector2(1.3f), ModulateSelfOverride = Color.LimeGreen,
        };
        private Texture? _cmuLoadedTexture;
        private Texture? _cmuSpentTexture;
        private int _cmuIndex = -1;

        private void CMUUpdateChambers(int currentIndex, bool?[] bullets)
        {
            var rebuild = _cmuRows.Length != bullets.Length;
            if (rebuild)
            {
                _cmuActiveChamber.Orphan();
                _bulletsList.RemoveAllChildren();
                _cmuIndex = -1;
                _cmuRows = new (Control, TextureRect)[bullets.Length];
                _cmuChambers = new bool?[bullets.Length];
                var texturePath = bullets.Length <= 20 ? "/Textures/Interface/ItemStatus/Bullets/normal.png"
                    : bullets.Length <= 30 ? "/Textures/Interface/ItemStatus/Bullets/small.png"
                    : "/Textures/Interface/ItemStatus/Bullets/tiny.png";
                _cmuLoadedTexture = StaticIoC.ResC.GetTexture(texturePath);
                _cmuSpentTexture = StaticIoC.ResC.GetTexture("/Textures/Interface/ItemStatus/Bullets/empty.png");
                _cmuActiveChamber.Texture = _cmuLoadedTexture;
                for (var i = 0; i < bullets.Length; i++)
                {
                    var box = new Control { MinSize = _cmuLoadedTexture.Size * 1.3f };
                    var bullet = new TextureRect { Stretch = TextureRect.StretchMode.KeepCentered };
                    box.AddChild(bullet);
                    _bulletsList.AddChild(box);
                    _cmuRows[i] = (box, bullet);
                }
            }

            for (var i = 0; i < bullets.Length; i++)
            {
                var state = bullets[i];
                if (!rebuild && state == _cmuChambers[i])
                    continue;
                _cmuChambers[i] = state;
                var alternate = (i & 1) != 0;
                var icon = _cmuRows[i].Bullet;
                icon.Texture = state == false ? _cmuSpentTexture : _cmuLoadedTexture;
                icon.ModulateSelfOverride = state switch
                {
                    true => alternate ? LoadedA : LoadedB,
                    false => alternate ? SpentA : SpentB,
                    null => alternate ? MissingA : MissingB,
                };
            }

            if (currentIndex == _cmuIndex)
                return;
            _cmuIndex = currentIndex;
            _cmuActiveChamber.Orphan();
            if (currentIndex >= 0 && currentIndex < _cmuRows.Length)
            {
                _cmuRows[currentIndex].Box.AddChild(_cmuActiveChamber);
                _cmuActiveChamber.SetPositionFirst();
            }
        }
    }
}
