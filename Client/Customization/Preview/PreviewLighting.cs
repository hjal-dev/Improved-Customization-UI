using System;
using System.Collections.Generic;
using EFT.UI;
using UnityEngine;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public class PreviewLighting : IDisposable
    {
        private readonly GameObject _host;
        private readonly Light _light;
        private readonly CameraLightSwitcher _switcher;

        private readonly List<Light> _rig = new List<Light>();
        private readonly Dictionary<Light, float> _originalIntensity = new Dictionary<Light, float>();
        private readonly Dictionary<Light, LightShadows> _originalShadows = new Dictionary<Light, LightShadows>();

        public PreviewLighting(PlayerProfilePreview preview)
        {
            Camera camera = preview._camera;

            Transform space = preview._cameraContainer != null && preview._cameraContainer.parent != null
                ? preview._cameraContainer.parent
                : preview.transform;

            _host = new GameObject("ImprovedCustomizationUIExtraLight");
            _host.transform.SetParent(space, false);
            _host.layer = camera.gameObject.layer;

            _light = _host.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.color = new Color(1f, 0.96f, 0.9f);
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;
            _light.cullingMask = camera.cullingMask;
            _light.enabled = false;

            CollectRig(camera, preview);

            _switcher = camera.gameObject.AddComponent<CameraLightSwitcher>();
            _switcher.Lights = new List<Light> { _light };

            Apply();
        }

        private void CollectRig(Camera camera, PlayerProfilePreview preview)
        {
            foreach (CameraLightSwitcher switcher in camera.GetComponents<CameraLightSwitcher>())
            {
                if (switcher.Lights == null)
                {
                    continue;
                }
                foreach (Light light in switcher.Lights)
                {
                    AddToRig(light);
                }
            }

            if (_rig.Count == 0)
            {
                foreach (Light light in preview.GetComponentsInChildren<Light>(true))
                {
                    if (light.name.IndexOf("Muzzle", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        AddToRig(light);
                    }
                }
            }

            string names = "";
            foreach (Light light in _rig)
            {
                names += " " + light.name + "(" + light.intensity.ToString("0.00") + ")";
            }
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] preview light rig:" + (names.Length > 0 ? names : " none found"));
        }

        private void AddToRig(Light light)
        {
            if (light == null || light == _light || _originalIntensity.ContainsKey(light))
            {
                return;
            }
            _rig.Add(light);
            _originalIntensity[light] = light.intensity;
            _originalShadows[light] = light.shadows;
        }

        public void Apply()
        {
            bool on = Plugin.PreviewLights.Value;
            _light.intensity = Plugin.ExtraLightBrightness.Value;
            _host.transform.localRotation = Quaternion.Euler(Plugin.ExtraLightRotationX.Value, Plugin.ExtraLightRotationY.Value, 0f);
            _switcher.enabled = on;
            if (!on)
            {
                _light.enabled = false;
            }

            float brightness = Plugin.GameLightsBrightness.Value;
            float rim = Plugin.RimLightStrength.Value;
            bool shadows = Plugin.GameLightShadows.Value;
            foreach (Light light in _rig)
            {
                if (light == null)
                {
                    continue;
                }

                float factor = brightness;
                if (light.name.IndexOf("rim", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    factor = factor * rim;
                }
                light.intensity = _originalIntensity[light] * factor;
                light.shadows = shadows ? _originalShadows[light] : LightShadows.None;
            }
        }

        public void Dispose()
        {
            if (_switcher != null)
            {
                UnityEngine.Object.Destroy(_switcher);
            }
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }
        }
    }
}
