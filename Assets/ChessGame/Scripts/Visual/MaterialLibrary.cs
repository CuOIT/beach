using UnityEngine;
using UnityEngine.Rendering;

namespace Chess.Visual
{
    /// <summary>
    /// Creates the handful of materials the game needs at runtime. URP shaders are looked up by
    /// name with a Built-in fallback so the scene still renders if the pipeline asset is missing.
    /// </summary>
    public static class MaterialLibrary
    {
        private static Shader _litShader;
        private static Shader _unlitShader;

        /// <summary>
        /// A shader reached only through <see cref="Shader.Find"/> is invisible to the build's
        /// dependency scan and gets stripped, so in a player the lookup returns null even though
        /// it works in the editor. ChessProjectSetup keeps these in the build by adding them to
        /// Always Included Shaders; this chain is the seatbelt, and it names the cause rather
        /// than letting a null shader throw out of Awake and take the rest of the game with it.
        /// </summary>
        private static Shader Resolve(ref Shader cached, string preferred, string fallback)
        {
            if (cached != null) return cached;

            cached = Shader.Find(preferred);
            if (cached != null) return cached;

            cached = Shader.Find(fallback);
            if (cached != null)
            {
                Debug.LogWarning("[Chess] Shader '" + preferred + "' is missing from this build; falling back to '" + fallback + "'.");
                return cached;
            }

            // Last resorts, which ship with any project that uses uGUI.
            cached = Shader.Find("Sprites/Default");
            if (cached == null) cached = Shader.Find("UI/Default");

            Debug.LogError(
                "[Chess] Neither '" + preferred + "' nor '" + fallback + "' is in this build. " +
                "Run Chess > Include Runtime Shaders In Builds to add them to Always Included Shaders.");

            return cached;
        }

        private static Shader LitShader => Resolve(ref _litShader, "Universal Render Pipeline/Lit", "Standard");

        private static Shader UnlitShader => Resolve(ref _unlitShader, "Universal Render Pipeline/Unlit", "Unlit/Color");

        public static Material CreateOpaque(string name, Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            Shader shader = LitShader;
            if (shader == null) return null;

            var material = new Material(shader) { name = name };
            ApplyColor(material, color);
            TrySetFloat(material, "_Smoothness", smoothness);
            TrySetFloat(material, "_Glossiness", smoothness);
            TrySetFloat(material, "_Metallic", metallic);
            return material;
        }

        /// <summary>Flat, unlit and see-through: used for the square highlights laid over the board.</summary>
        public static Material CreateOverlay(string name, Color color, float alpha)
        {
            color.a = alpha;

            Shader shader = UnlitShader;
            if (shader == null) return null;

            var material = new Material(shader) { name = name };
            ApplyColor(material, color);
            MakeTransparent(material);
            return material;
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void TrySetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        /// <summary>
        /// URP decides opaque versus transparent from shader properties plus a keyword, so all of
        /// these have to be set together for a material built in code.
        /// </summary>
        private static void MakeTransparent(Material material)
        {
            TrySetFloat(material, "_Surface", 1f);
            TrySetFloat(material, "_Blend", 0f);
            TrySetFloat(material, "_ZWrite", 0f);
            TrySetFloat(material, "_AlphaClip", 0f);

            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>A rounded-corner sprite generated in code, for UI panels and buttons.</summary>
        public static Sprite CreateRoundedSprite(int size = 64, int cornerRadius = 16)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RoundedRect",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedRectCoverage(x + 0.5f, y + 0.5f, size, cornerRadius);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            int border = cornerRadius + 1;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>Antialiased coverage of a rounded rectangle, sampled at one point per pixel.</summary>
        private static float RoundedRectCoverage(float x, float y, float size, float radius)
        {
            float half = size * 0.5f;
            float dx = Mathf.Abs(x - half) - (half - radius);
            float dy = Mathf.Abs(y - half) - (half - radius);

            if (dx <= 0f || dy <= 0f) return 1f;

            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        /// <summary>A soft radial dot, used for the legal-move markers.</summary>
        public static Sprite CreateCircleSprite(int size = 64)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
                    float alpha = Mathf.Clamp01(half - 0.5f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
