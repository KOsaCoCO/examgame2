using UnityEngine;
using UnityEngine.UI;

namespace NTGD124
{
    // Fills the whole screen with a radial dark-red vignette while the player is standing in the
    // miasma fog (see MiasmaFogArea) - center of the screen stays fully clear, only the corners
    // show real colour, and it fades smoothly in/out rather than popping. Drives the "UI-
    // InMiasma" canvas's RawImageMiasma purely through GameEvents.OnPlayerEnteredMiasma/
    // OnPlayerExitedMiasma - no direct reference to MiasmaFogArea needed either way.
    public class MiasmaOverlayUI : MonoBehaviour
    {
        [Header("Lookup (auto-found by name)")]
        [Tooltip("Name of the RawImage GameObject under the UI-InMiasma canvas.")]
        public string rawImageObjectName = "RawImageMiasma";

        [Header("Look")]
        [Tooltip("Tint at full visibility (corners) - keep the alpha modest, this should read as dimmed, not invasive.")]
        public Color miasmaColor = new Color(0.4f, 0.05f, 0.05f, 0.55f);
        [Tooltip("Generated gradient texture resolution - it's a smooth blur, doesn't need to be large.")]
        public int gradientTextureSize = 256;
        [Tooltip("Seconds to fade fully in, or fully out.")]
        public float fadeDuration = 0.6f;

        private RawImage rawImage;
        private float targetVisibility; // 0 = fully hidden, 1 = fully shown
        private float currentVisibility;

        void Start()
        {
            GameObject rawImageObject = GameObject.Find(rawImageObjectName);
            if (rawImageObject == null)
            {
                Debug.LogWarning($"MiasmaOverlayUI: no '{rawImageObjectName}' object found - miasma overlay won't show.");
                return;
            }

            rawImage = rawImageObject.GetComponent<RawImage>();
            if (rawImage == null) return;

            // Authored as a small fixed-size box anchored to screen center - stretch it to fill
            // the whole screen instead, so the radial gradient's corners land on the actual
            // screen corners regardless of resolution/aspect ratio.
            StretchToFullScreen(rawImage.rectTransform);
            rawImage.texture = BuildRadialGradientTexture();
            rawImage.raycastTarget = false; // purely visual, must never block clicks

            ApplyVisibility();

            GameEvents.OnPlayerEnteredMiasma += HandleEnteredMiasma;
            GameEvents.OnPlayerExitedMiasma += HandleExitedMiasma;
        }

        void OnDestroy()
        {
            GameEvents.OnPlayerEnteredMiasma -= HandleEnteredMiasma;
            GameEvents.OnPlayerExitedMiasma -= HandleExitedMiasma;
        }

        void Update()
        {
            if (rawImage == null || Mathf.Approximately(currentVisibility, targetVisibility)) return;

            float step = Time.deltaTime / Mathf.Max(fadeDuration, 0.01f);
            currentVisibility = Mathf.MoveTowards(currentVisibility, targetVisibility, step);
            ApplyVisibility();
        }

        private void HandleEnteredMiasma() => targetVisibility = 1f;
        private void HandleExitedMiasma() => targetVisibility = 0f;

        private void ApplyVisibility()
        {
            rawImage.color = new Color(miasmaColor.r, miasmaColor.g, miasmaColor.b, miasmaColor.a * currentVisibility);
        }

        private static void StretchToFullScreen(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        // Center of the texture (UV 0.5,0.5) is fully transparent; alpha ramps up with distance
        // from center, reaching full right at the UV-space corner distance (~0.707) - true
        // regardless of the RawImage's actual pixel aspect ratio, since UV space is always 0..1
        // on both axes no matter how the rect is stretched.
        private Texture2D BuildRadialGradientTexture()
        {
            Texture2D texture = new Texture2D(gradientTextureSize, gradientTextureSize, TextureFormat.RGBA32, false);
            const float maxCornerDistance = 0.70710678f; // sqrt(0.5^2 + 0.5^2)
            Vector2 center = new Vector2(0.5f, 0.5f);

            for (int y = 0; y < gradientTextureSize; y++)
            {
                for (int x = 0; x < gradientTextureSize; x++)
                {
                    float u = (x + 0.5f) / gradientTextureSize;
                    float v = (y + 0.5f) / gradientTextureSize;
                    float distance = Vector2.Distance(new Vector2(u, v), center);
                    float alpha = Mathf.Clamp01(distance / maxCornerDistance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}

// Implementation Steps:
// 1. Add this component to any GameObject in the scene (e.g. the UI-InMiasma canvas itself) -
//    it finds RawImageMiasma by name, no manual reference needed.
// 2. Tune miasmaColor (RGB + max alpha) / fadeDuration to taste - defaults to a dimmed dark red,
//    modest alpha (0.55 at full corner intensity) per this session's "not too invasive" spec.
// 3. Nothing else to wire - GameEvents.OnPlayerEnteredMiasma/OnPlayerExitedMiasma (raised by
//    MiasmaFogArea) drive the fade automatically.
