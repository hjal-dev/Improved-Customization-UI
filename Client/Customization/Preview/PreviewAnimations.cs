using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.Settings;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public enum PreviewAction
    {
        Inspect,
        CheckAmmo,
        CheckChamber,
        Reload
    }

    public class PreviewAnimations
    {
        private static readonly FieldInfo _weaponAnimatorField = AccessTools.Field(typeof(MenuPlayerPoser), "_weaponAnimator");
        private static readonly FieldInfo _weaponAnimationsField = AccessTools.Field(typeof(MenuPlayerPoser), "_weaponAnimations");
        private static readonly FieldInfo _weaponAnimationTypeField = AccessTools.Field(typeof(MenuPlayerPoser), "_weaponAnimationType");
        private static readonly FieldInfo _gestureAnimationTypeField = AccessTools.Field(typeof(MenuPlayerPoser), "_gestureAnimationType");

        public static readonly EInteraction[] GESTURES = new EInteraction[]
        {
            EInteraction.ThereGesture,
            EInteraction.HoldGesture,
            EInteraction.FriendlyGesture,
            EInteraction.GetOffGesture,
            EInteraction.OkGesture,
            EInteraction.NoGesture,
            EInteraction.ComeWithMeGesture
        };

        private const float BUSY_TIMEOUT = 20f;

        private readonly PlayerProfilePreview _preview;
        private float _startedAt = -100f;

        public PreviewAnimations(PlayerProfilePreview preview)
        {
            _preview = preview;
        }

        private MenuPlayerPoser Poser()
        {
            PlayerModelView view = _preview.PlayerModelView;
            if (view == null || !view.LoadingComplete)
            {
                return null;
            }
            return view.ModelPlayerPoser;
        }

        private static IAnimator WeaponAnimator(MenuPlayerPoser poser)
        {
            return (IAnimator)_weaponAnimatorField.GetValue(poser);
        }

        private static int Parameter(PreviewAction action)
        {
            if (action == PreviewAction.Inspect)
            {
                return AnimationControllerParametersTable.TRIGGER_LOOK;
            }
            if (action == PreviewAction.CheckAmmo)
            {
                return AnimationControllerParametersTable.TRIGGER_CHECKAMMO;
            }
            if (action == PreviewAction.CheckChamber)
            {
                return AnimationControllerParametersTable.TRIGGER_CHECKCHAMBER;
            }
            return AnimationControllerParametersTable.TRIGGER_RELOAD;
        }

        private static int IndexOf(MenuPlayerPoser poser, PreviewAction action)
        {
            IAnimator animator = WeaponAnimator(poser);
            if (animator == null)
            {
                return -1;
            }

            int parameter = Parameter(action);
            List<(int Parameter, Action<IAnimator> AnimateAction)> animations =
                (List<(int Parameter, Action<IAnimator> AnimateAction)>)_weaponAnimationsField.GetValue(poser);
            if (animations == null)
            {
                return -1;
            }

            for (int i = 0; i < animations.Count; i++)
            {
                if (animations[i].Parameter == parameter)
                {
                    return animator.HasParameter(parameter) ? i : -1;
                }
            }
            return -1;
        }

        public bool CanPlay(PreviewAction action)
        {
            MenuPlayerPoser poser = Poser();
            return poser != null && IndexOf(poser, action) >= 0;
        }

        public bool CanGesture()
        {
            MenuPlayerPoser poser = Poser();
            return poser != null && WeaponAnimator(poser) != null;
        }

        public bool IsBusy()
        {
            MenuPlayerPoser poser = Poser();
            if (poser == null || poser.PlayerAnimatorController == null)
            {
                return false;
            }
            if (Time.unscaledTime - _startedAt > BUSY_TIMEOUT)
            {
                return false;
            }
            return poser.PlayerAnimatorController.GetBool(PlayerAnimator.UI_WEAPON_OPERATION);
        }

        public bool Play(PreviewAction action)
        {
            MenuPlayerPoser poser = Poser();
            if (poser == null || IsBusy())
            {
                return false;
            }

            int index = IndexOf(poser, action);
            if (index < 0)
            {
                return false;
            }

            _weaponAnimationTypeField.SetValue(poser, index);
            _gestureAnimationTypeField.SetValue(poser, EInteraction.None);
            Start(poser);
            return true;
        }

        public bool PlayGesture(EInteraction gesture)
        {
            MenuPlayerPoser poser = Poser();
            if (poser == null || IsBusy() || WeaponAnimator(poser) == null)
            {
                return false;
            }

            _weaponAnimationTypeField.SetValue(poser, -1);
            _gestureAnimationTypeField.SetValue(poser, gesture);
            Start(poser);
            WatchGestureLayer(WeaponAnimator(poser));
            return true;
        }

        private const float GESTURE_BLEND_TIME = 0.15f;
        private const float GESTURE_TIMEOUT = 8f;

        private IAnimator _gestureAnimator;
        private int _gestureLayer = -1;
        private int _gestureIdleState;
        private bool _gestureEntered;
        private float _gestureRequestedAt;

        private void WatchGestureLayer(IAnimator animator)
        {
            _gestureAnimator = null;
            if (animator == null)
            {
                return;
            }

            _gestureLayer = animator.GetLayerIndex("LActions");
            if (_gestureLayer < 0)
            {
                return;
            }

            _gestureIdleState = animator.GetCurrentAnimatorStateInfo(_gestureLayer).shortNameHash;
            _gestureEntered = false;
            _gestureRequestedAt = Time.unscaledTime;
            _gestureAnimator = animator;
        }

        public void UpdateGestureLayer()
        {
            if (_gestureAnimator == null)
            {
                return;
            }

            MenuPlayerPoser poser = Poser();
            if (poser == null || WeaponAnimator(poser) != _gestureAnimator)
            {
                _gestureAnimator = null;
                return;
            }

            AnimatorStateInfoWrapper state = _gestureAnimator.GetCurrentAnimatorStateInfo(_gestureLayer);
            bool playing = state.shortNameHash != _gestureIdleState || _gestureAnimator.IsInTransition(_gestureLayer);
            if (playing)
            {
                _gestureEntered = true;
            }

            float target = playing ? 1f : 0f;
            float weight = Mathf.MoveTowards(_gestureAnimator.GetLayerWeight(_gestureLayer), target, Time.unscaledDeltaTime / GESTURE_BLEND_TIME);
            _gestureAnimator.SetLayerWeight(_gestureLayer, weight);

            bool finished = _gestureEntered && !playing && weight <= 0f;
            bool gaveUp = Time.unscaledTime - _gestureRequestedAt > GESTURE_TIMEOUT;
            if (finished || gaveUp)
            {
                _gestureAnimator.SetLayerWeight(_gestureLayer, 0f);
                _gestureAnimator = null;
            }
        }

        private void Start(MenuPlayerPoser poser)
        {
            _startedAt = Time.unscaledTime;
            poser.TurnOffLights();
        }

        private static readonly EGameKey[] KEYS = new EGameKey[]
        {
            EGameKey.ExamineWeapon, EGameKey.CheckAmmo, EGameKey.CheckChamber, EGameKey.ReloadWeapon
        };

        private static readonly PreviewAction[] KEY_ACTIONS = new PreviewAction[]
        {
            PreviewAction.Inspect, PreviewAction.CheckAmmo, PreviewAction.CheckChamber, PreviewAction.Reload
        };

        public void CheckKeys()
        {
            List<KeyGroup> binds;
            try
            {
                binds = Singleton<SettingsManager>.Instance.Control.Settings.UserKeyBindings.Value;
            }
            catch (Exception)
            {
                return;
            }
            if (binds == null)
            {
                return;
            }

            int best = -1;
            int bestLength = 0;
            for (int i = 0; i < KEYS.Length; i++)
            {
                foreach (KeyGroup bind in binds)
                {
                    if (bind == null || bind.keyName != KEYS[i] || bind.variants == null)
                    {
                        continue;
                    }

                    foreach (InputSource variant in bind.variants)
                    {
                        int length = PressedLength(variant);
                        if (length > bestLength)
                        {
                            best = i;
                            bestLength = length;
                        }
                    }
                }
            }

            if (best >= 0)
            {
                Play(KEY_ACTIONS[best]);
            }
        }

        private static int PressedLength(InputSource variant)
        {
            if (variant == null || variant.isAxis || variant.keyCode == null || variant.keyCode.Count == 0)
            {
                return 0;
            }

            bool justPressed = false;
            foreach (KeyCode key in variant.keyCode)
            {
                if (!Input.GetKey(key))
                {
                    return 0;
                }
                if (Input.GetKeyDown(key))
                {
                    justPressed = true;
                }
            }
            return justPressed ? variant.keyCode.Count : 0;
        }
    }
}
