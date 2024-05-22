#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Kamgam.UIPreview
{
    public static class RenderTexturePool
    {
        static int MaxCapacity = 2;
        static Dictionary<int, RenderTexture> renderTextures = new Dictionary<int, RenderTexture>();

        public static RenderTexture Get(int width, int height, int depth, RenderTextureFormat renderTextureFormat)
        {
            int key = getKey(width, height);

            // Clean up destroyed textures
            if (renderTextures.ContainsKey(key))
            {
                if (renderTextures[key] == null)
                {
                    renderTextures.Remove(key);
                }
            }

            // Find or create texture
            if (renderTextures.ContainsKey(key))
            {
                return renderTextures[key];
            }
            else
            {
                // Remove if above capacity
                if (renderTextures.Count > MaxCapacity)
                {
                    var e = renderTextures.Keys.GetEnumerator();
                    e.MoveNext();
                    renderTextures.Remove(e.Current);
                }

                // add new
                renderTextures.Add(key, new RenderTexture(width, height, depth, renderTextureFormat));
                return renderTextures[key];
            }
        }

        static int getKey(int width, int height)
        {
            return width * 10000 + height;
        }
    }
}
#endif
