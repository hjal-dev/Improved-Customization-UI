using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ImprovedCustomizationUI.UI
{
    public static class UiHelpers
    {
        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.layer = parent.gameObject.layer;
            return host.GetComponent<RectTransform>();
        }

        public static Sprite LoadSprite(string file)
        {
            if (_sprites.TryGetValue(file, out Sprite cached) && cached != null)
            {
                return cached;
            }

            using (Stream resource = typeof(UiHelpers).Assembly.GetManifestResourceStream("ImprovedCustomizationUI." + file))
            {
                if (resource == null)
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] embedded image " + file + " missing");
                    return null;
                }

                MemoryStream bytes = new MemoryStream();
                resource.CopyTo(bytes);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!texture.LoadImage(bytes.ToArray()))
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] embedded image " + file + " unreadable");
                    return null;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Trilinear;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = Path.GetFileNameWithoutExtension(file);
                _sprites[file] = sprite;
                return sprite;
            }
        }
    }
}
