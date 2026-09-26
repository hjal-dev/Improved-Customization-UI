using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT.UI;
using UnityEngine;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public class PreviewSounds : IDisposable
    {
        public static PreviewSounds Current;

        private readonly PlayerProfilePreview _preview;
        private readonly GameObject _host;
        private readonly AudioSource _source;

        private readonly HashSet<string> _missing = new HashSet<string>();

        public PreviewSounds(PlayerProfilePreview preview, Transform parent)
        {
            _preview = preview;
            _host = new GameObject("ImprovedCustomizationUIPreviewSounds");
            _host.transform.SetParent(parent, false);

            _source = _host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.outputAudioMixerGroup = FindUiMixerGroup();

            Current = this;
        }

        public static void TryPlay(MenuPlayerPoser poser, string soundName)
        {
            if (Current == null || Plugin.PreviewWeaponSounds == null || !Plugin.PreviewWeaponSounds.Value || string.IsNullOrEmpty(soundName))
            {
                return;
            }
            Current.Play(poser, soundName);
        }

        private void Play(MenuPlayerPoser poser, string soundName)
        {
            PlayerModelView view = _preview.PlayerModelView;
            if (view == null || !view.LoadingComplete || view.ModelPlayerPoser != poser)
            {
                return;
            }

            BaseSoundPlayer.SoundElement sound = FindSound(poser, soundName);
            AudioClip clip = sound != null ? sound.RandomSoundClip : null;
            if (clip == null)
            {
                if (_missing.Add(soundName))
                {
                    Plugin.Log.LogInfo("[ImprovedCustomizationUI] preview sound '" + soundName + "': no clip on this weapon");
                }
                return;
            }

            float volume = Plugin.PreviewWeaponSoundsVolume != null ? Plugin.PreviewWeaponSoundsVolume.Value : 1f;
            _source.PlayOneShot(clip, sound.Volume * volume);
        }

        private static BaseSoundPlayer.SoundElement FindSound(MenuPlayerPoser poser, string soundName)
        {
            string prefixed = "Snd" + soundName;
            foreach (BaseSoundPlayer player in poser.GetComponentsInChildren<BaseSoundPlayer>(true))
            {
                if (player.AdditionalSounds != null)
                {
                    foreach (BaseSoundPlayer.SoundElement sound in player.AdditionalSounds)
                    {
                        if (sound != null && (sound.EventName == soundName || sound.EventName == prefixed))
                        {
                            return sound;
                        }
                    }
                }

                if (player.MainSounds != null)
                {
                    foreach (BaseSoundPlayer.SoundElement sound in player.MainSounds)
                    {
                        if (sound != null && (sound.EventName == soundName || sound.EventName == prefixed))
                        {
                            return sound;
                        }
                    }
                }
            }
            return null;
        }

        private static UnityEngine.Audio.AudioMixerGroup FindUiMixerGroup()
        {
            try
            {
                GUISounds sounds = Singleton<GUISounds>.Instance;
                if (sounds == null)
                {
                    return null;
                }

                foreach (FieldInfo field in typeof(GUISounds).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.FieldType != typeof(AudioSource))
                    {
                        continue;
                    }

                    AudioSource source = field.GetValue(sounds) as AudioSource;
                    if (source != null && source.outputAudioMixerGroup != null)
                    {
                        return source.outputAudioMixerGroup;
                    }
                }
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] preview sounds: UI audio channel not found (" + error.Message + ")");
            }
            return null;
        }

        public void Dispose()
        {
            if (Current == this)
            {
                Current = null;
            }
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }
        }
    }
}
