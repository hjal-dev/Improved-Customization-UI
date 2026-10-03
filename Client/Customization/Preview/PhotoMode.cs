using System;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public class PhotoMode : IDisposable
    {
        private static readonly string[] RESOLUTION_NAMES = { "1080P", "1440P", "4K", "SQUARE 2048" };
        private static readonly Vector2Int[] RESOLUTIONS = { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160), new Vector2Int(2048, 2048) };
        private static readonly string[] FRAMING_NAMES = { "CURRENT VIEW", "FULL BODY" };
        private static readonly int[] COUNTDOWNS = { 0, 3, 5 };

        private static int _resolution;
        private static int _framing = 1;
        private static int _countdown;

        private readonly PlayerProfilePreview _preview;
        private readonly string _nickname;
        private readonly GameObject _panel;
        private readonly DefaultUIButton _resolutionButton;
        private readonly DefaultUIButton _framingButton;
        private readonly DefaultUIButton _countdownButton;
        private readonly DefaultUIButton _captureButton;
        private readonly DefaultUIButton _template;
        private readonly RectTransform _anchor;
        private bool _busy;
        private bool _disposed;

        public PhotoMode(PlayerProfilePreview preview, string nickname, RectTransform anchor, DefaultUIButton template)
        {
            _preview = preview;
            _nickname = nickname;
            _template = template;

            _panel = new GameObject("PhotoModePanel", typeof(RectTransform));
            RectTransform rect = (RectTransform)_panel.transform;
            rect.SetParent(anchor.parent.parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            _anchor = anchor;
            rect.sizeDelta = new Vector2(560f, 0f);

            VerticalLayoutGroup layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            ContentSizeFitter fitter = _panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _resolutionButton = AddButton("PhotoResolution", delegate { _resolution = (_resolution + 1) % RESOLUTIONS.Length; Refresh(); });
            _framingButton = AddButton("PhotoFraming", delegate { _framing = (_framing + 1) % FRAMING_NAMES.Length; Refresh(); });
            _countdownButton = AddButton("PhotoCountdown", delegate { _countdown = (_countdown + 1) % COUNTDOWNS.Length; Refresh(); });
            _captureButton = AddButton("PhotoCapture", CaptureClicked);

            Refresh();
            _panel.SetActive(false);
        }

        public bool Open
        {
            get { return _panel != null && _panel.activeSelf; }
        }

        public void Toggle()
        {
            if (_panel != null)
            {
                if (!_panel.activeSelf)
                {
                    Vector3[] corners = new Vector3[4];
                    _anchor.GetWorldCorners(corners);
                    _panel.transform.position = corners[1];
                }
                _panel.SetActive(!_panel.activeSelf);
            }
        }

        private DefaultUIButton AddButton(string name, Action clicked)
        {
            DefaultUIButton button = UnityEngine.Object.Instantiate(_template, _panel.transform, false);
            button.name = name;
            foreach (ContentSizeFitter sizeFitter in button.GetComponentsInChildren<ContentSizeFitter>(true))
            {
                sizeFitter.enabled = false;
            }
            button.SetIcon(null, null);
            button.OnClick.AddListener(delegate { clicked(); });
            button.gameObject.SetActive(true);
            return button;
        }

        private void SetText(DefaultUIButton button, string text)
        {
            button.SetRawText(text, _template.HeaderSize);
            foreach (TMPro.TextMeshProUGUI label in button.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                label.alignment = TMPro.TextAlignmentOptions.Left;
            }
        }

        private void Refresh()
        {
            SetText(_resolutionButton, "SIZE: " + RESOLUTION_NAMES[_resolution]);
            SetText(_framingButton, "FRAME: " + FRAMING_NAMES[_framing]);
            SetText(_countdownButton, "TIMER: " + (COUNTDOWNS[_countdown] == 0 ? "OFF" : COUNTDOWNS[_countdown] + "S"));
            if (!_busy)
            {
                SetText(_captureButton, "CAPTURE");
            }
        }

        private async void CaptureClicked()
        {
            if (_busy)
            {
                return;
            }
            _busy = true;
            try
            {
                for (int left = COUNTDOWNS[_countdown]; left > 0; left--)
                {
                    SetText(_captureButton, left + "...");
                    await Task.Delay(1000);
                    if (_disposed)
                    {
                        return;
                    }
                }

                string path = Capture();
                SetText(_captureButton, "SAVED");
                Plugin.Log.LogInfo("[ImprovedCustomizationUI] photo saved to " + path);
                await Task.Delay(1500);
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("[ImprovedCustomizationUI] photo failed: " + error);
                SetText(_captureButton, "FAILED (SEE LOG)");
                await Task.Delay(2000);
            }
            finally
            {
                _busy = false;
                if (!_disposed)
                {
                    Refresh();
                }
            }
        }

        private string Capture()
        {
            Camera camera = _preview._camera;
            Vector2Int size = RESOLUTIONS[_resolution];

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.localPosition;
            Quaternion rotation = cameraTransform.localRotation;
            Quaternion containerRotation = _preview._cameraContainer.localRotation;
            RenderTexture oldTarget = camera.targetTexture;
            CameraClearFlags oldClear = camera.clearFlags;
            Color oldBackground = camera.backgroundColor;
            Matrix4x4 oldProjection = camera.projectionMatrix;
            RenderTexture target = oldTarget != null
                ? new RenderTexture(size.x, size.y, oldTarget.depth, oldTarget.format)
                : new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            if (oldTarget != null)
            {
                target.antiAliasing = oldTarget.antiAliasing;
            }
            Texture2D result;
            try
            {
                camera.targetTexture = target;
                camera.ResetAspect();
                if (_framing == 1)
                {
                    camera.ResetProjectionMatrix();
                    FrameWholeBody(camera);
                }
                camera.clearFlags = CameraClearFlags.SolidColor;

                result = RenderOnce(camera, target, new Color(0f, 0f, 0f, 0f));
                float ownAlpha = result.GetPixel(2, 2).a;
                float gameAlpha = -1f;
                if (oldTarget != null)
                {
                    Texture2D game = ReadTexture(oldTarget);
                    gameAlpha = game.GetPixel(2, 2).a;
                    if (ownAlpha > 0.99f && gameAlpha < 0.99f)
                    {
                        UnityEngine.Object.Destroy(result);
                        result = game;
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(game);
                    }
                }
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.ResetAspect();
                camera.projectionMatrix = oldProjection;
                camera.clearFlags = oldClear;
                camera.backgroundColor = oldBackground;
                cameraTransform.localPosition = position;
                cameraTransform.localRotation = rotation;
                _preview._cameraContainer.localRotation = containerRotation;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            string folder = PhotoFolder();
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, SafeName(_nickname) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png");
            File.WriteAllBytes(file, result.EncodeToPNG());
            UnityEngine.Object.Destroy(result);
            return file;
        }

        private void FrameWholeBody(Camera camera)
        {
            Bounds bounds = new Bounds();
            bool found = false;
            foreach (Renderer renderer in _preview.PlayerModelView.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)))
                {
                    continue;
                }
                if (renderer.transform.parent != null && renderer.transform.parent.name.Contains("Shadow"))
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            if (!found)
            {
                return;
            }

            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            float margin = 1.08f;
            float halfHeight = bounds.extents.y * margin;
            float halfWidth = Mathf.Max(bounds.extents.x, bounds.extents.z) * margin;
            float verticalTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalTan = verticalTan * camera.aspect;
            float distance = Mathf.Max(halfHeight / verticalTan, halfWidth / horizontalTan) + bounds.extents.z;

            camera.transform.position = bounds.center - forward * distance;
            camera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private static Texture2D ReadTexture(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        private static Texture2D RenderOnce(Camera camera, RenderTexture target, Color background)
        {
            camera.backgroundColor = background;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        public static string PhotoFolder()
        {
            string custom = Plugin.PhotoFolder.Value;
            if (!string.IsNullOrEmpty(custom) && custom.Trim().Length > 0)
            {
                return custom.Trim();
            }

            string root = BepInEx.Paths.GameRootPath;
            foreach (string server in new[] { "SPT", "SPT_Runtime" })
            {
                string mod = Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, server), "user"), "mods"), "ImprovedCustomizationUI");
                if (Directory.Exists(mod))
                {
                    return Path.Combine(mod, "Photos");
                }
            }
            return Path.Combine(Path.Combine(BepInEx.Paths.PluginPath, "ImprovedCustomizationUI"), "Photos");
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "PMC";
            }
            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }
            return name;
        }

        public void Dispose()
        {
            _disposed = true;
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }
        }
    }
}
