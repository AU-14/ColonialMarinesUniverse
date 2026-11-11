using System.Numerics;
using Content.Client.IoC;
using Content.Client.Items;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Weapons.Ranged.Components;
using Content.Client.Weapons.Ranged.ItemStatus;
using Robust.Client.Animations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void OnAmmoCounterCollect(Entity<AmmoCounterComponent> ent, ref ItemStatusCollectMessage args)
    {
        // cmu edit start: without an ammo counter you can't see the rounds left; energy guns keep their charge display
        if (!CMUShowsAmmoCount(ent) && !HasComp<Content.Shared.Weapons.Ranged.Components.BatteryAmmoProviderComponent>(ent))
            return;
        // cmu edit end

        RefreshControl(ent);

        if (ent.Comp.Control != null)
            args.Controls.Add(ent.Comp.Control);
    }

    /// <summary>
    /// Refreshes the control being used to show ammo. Useful if you change the AmmoProvider.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    private void RefreshControl(Entity<AmmoCounterComponent> ent)
    {
        ent.Comp.Control?.Release();
        ent.Comp.Control = null;

        var ev = new AmmoCounterControlEvent();
        RaiseLocalEvent(ent, ev, false);

        // Fallback to default if none specified
        ev.Control ??= new DefaultStatusControl();

        ent.Comp.Control = ev.Control;
        UpdateAmmoCount(ent);
    }

    private void UpdateAmmoCount(Entity<AmmoCounterComponent> ent, int artificialIncrease = 0)
    {
        if (ent.Comp.Control == null)
            return;

        var ev = new UpdateAmmoCounterEvent()
        {
            Control = ent.Comp.Control,
            ArtificialIncrease = artificialIncrease
        };

        RaiseLocalEvent(ent, ev, false);
    }

    public override void UpdateAmmoCount(EntityUid uid, bool prediction = true, int artificialIncrease = 0)
    {
        // Don't use resolves because the method is shared and there's no compref and I'm trying to
        // share as much code as possible
        if (prediction && !Timing.IsFirstTimePredicted ||
            !TryComp<AmmoCounterComponent>(uid, out var clientComp))
        {
            return;
        }

        UpdateAmmoCount((uid, clientComp), artificialIncrease);
    }

    /// <summary>
    /// Raised when an ammocounter is requesting a control.
    /// </summary>
    public sealed class AmmoCounterControlEvent : EntityEventArgs
    {
        public Control? Control;
    }

    /// <summary>
    /// Raised whenever the ammo count / magazine for a control needs updating.
    /// </summary>
    public sealed class UpdateAmmoCounterEvent : HandledEntityEventArgs
    {
        public Control Control = default!;
        public int ArtificialIncrease = 0;
    }

    #region Controls

    private sealed class DefaultStatusControl : Control
    {
        private readonly BulletRender _bulletRender;

        public DefaultStatusControl()
        {
            MinHeight = 15;
            HorizontalExpand = true;
            VerticalAlignment = VAlignment.Center;
            AddChild(_bulletRender = new BulletRender
            {
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Bottom
            });
        }

        public void Update(int count, int capacity)
        {
            _bulletRender.Count = count;
            _bulletRender.Capacity = capacity;

            _bulletRender.Type = capacity switch
            {
                > 50 => BulletRender.BulletType.Tiny,
                > 15 => BulletRender.BulletType.Normal,
                _ => BulletRender.BulletType.Large
            };
        }
    }

    public sealed class BoxesStatusControl : Control
    {
        private readonly BatteryBulletRenderer _bullets;
        private readonly Label _ammoCount;

        public BoxesStatusControl()
        {
            MinHeight = 15;
            HorizontalExpand = true;
            VerticalAlignment = Control.VAlignment.Center;

            AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    (_bullets = new BatteryBulletRenderer
                    {
                        Margin = new Thickness(0, 0, 5, 0),
                        HorizontalExpand = true
                    }),
                    (_ammoCount = new Label
                    {
                        StyleClasses = { StyleClass.ItemStatus },
                        HorizontalAlignment = HAlignment.Right,
                        VerticalAlignment = VAlignment.Bottom
                    }),
                }
            });
        }

        public void Update(int count, int max)
        {
            _ammoCount.Visible = true;

            _ammoCount.Text = $"x{count:00}";

            _bullets.Capacity = max;
            _bullets.Count = count;
        }
    }

    public sealed class CustomIconStatusControl : Control
    {
        private readonly CustomBulletRenderer _customBulletRenderer;

        public CustomIconStatusControl(Texture loadedSprite, Texture spentTexture, int numberOfRows)
        {
            MinHeight = 15;
            HorizontalExpand = true;
            VerticalAlignment = VAlignment.Center;

            AddChild(_customBulletRenderer = new CustomBulletRenderer(loadedSprite, spentTexture, numberOfRows)
            {
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Bottom,
            });
        }

        public void Update(int count, int capacity)
        {
            _customBulletRenderer.Capacity = capacity;
            _customBulletRenderer.Count = count;
        }
    }

    private sealed class ChamberMagazineStatusControl : Control
    {
        private readonly BulletRender _bulletRender;
        private readonly TextureRect _chamberedBullet;
        private readonly Label _noMagazineLabel;
        private readonly Label _ammoCount;

        public ChamberMagazineStatusControl()
        {
            MinHeight = 15;
            HorizontalExpand = true;
            VerticalAlignment = Control.VAlignment.Center;

            AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Children =
                {
                    new Control
                    {
                        HorizontalExpand = true,
                        Margin = new Thickness(0, 0, 5, 0),
                        Children =
                        {
                            (_bulletRender = new BulletRender
                            {
                                HorizontalAlignment = HAlignment.Right,
                                VerticalAlignment = VAlignment.Bottom
                            }),
                            (_noMagazineLabel = new Label
                            {
                                Text = "No Magazine!",
                                StyleClasses = {StyleClass.ItemStatus}
                            })
                        }
                    },
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        VerticalAlignment = VAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 2),
                        Children =
                        {
                            (_ammoCount = new Label
                            {
                                StyleClasses = {StyleClass.ItemStatus},
                                HorizontalAlignment = HAlignment.Right,
                            }),
                            (_chamberedBullet = new TextureRect
                            {
                                Texture = StaticIoC.ResC.GetTexture("/Textures/Interface/ItemStatus/Bullets/chambered.png"),
                                HorizontalAlignment = HAlignment.Left,
                            }),
                        }
                    }
                }
            });
        }

        public void Update(bool chambered, bool magazine, int count, int capacity)
        {
            _chamberedBullet.ModulateSelfOverride =
                chambered ? Color.FromHex("#d7df60") : Color.Black;

            if (!magazine)
            {
                _bulletRender.Visible = false;
                _noMagazineLabel.Visible = true;
                _ammoCount.Visible = false;
                return;
            }

            _bulletRender.Visible = true;
            _noMagazineLabel.Visible = false;
            _ammoCount.Visible = true;

            _bulletRender.Count = count;
            _bulletRender.Capacity = capacity;

            _bulletRender.Type = capacity switch
            {
                > 50 => BulletRender.BulletType.Tiny,
                > 15 => BulletRender.BulletType.Normal,
                _ => BulletRender.BulletType.Large
            };

            _ammoCount.Text = $"x{count:00}";
        }

        public void PlayAlarmAnimation(Animation animation)
        {
            _noMagazineLabel.PlayAnimation(animation, "alarm");
        }
    }

    // CMU14: retain chamber controls and update only changed contents or the active index.
    private sealed partial class RevolverStatusControl : Control
    {
        private readonly BoxContainer _bulletsList;

        public RevolverStatusControl()
        {
            MinHeight = 15;
            HorizontalExpand = true;
            VerticalAlignment = Control.VAlignment.Center;
            AddChild((_bulletsList = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                SeparationOverride = 0
            }));
        }

        public void Update(int currentIndex, bool?[] bullets)
        {
            CMUUpdateChambers(currentIndex, bullets);
        }
    }

    #endregion
}
