using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>Small runtime-only placeholder used until authored VFX arrive.</summary>
    public sealed class CombatPlaceholderEffectView : MonoBehaviour
    {
        private const int TextureSize = 32;
        private static Sprite circleSprite;

        private SpriteRenderer spriteRenderer;
        private Color initialColor;
        private float targetDiameter;
        private float duration;
        private float elapsed;

        public static void CreatePulse(Vector2 position, float radius, Color color, float durationSeconds)
        {
            var effectObject = new GameObject("CombatPlaceholderPulse");
            effectObject.transform.position = position;
            var effect = effectObject.AddComponent<CombatPlaceholderEffectView>();
            effect.Initialize(radius, color, durationSeconds);
        }

        public static SpriteRenderer CreateAreaRenderer(GameObject owner, float radius, Color color, int sortingOrder)
        {
            var renderer = owner.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCircleSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            owner.transform.localScale = Vector3.one * (radius * 2f);
            return renderer;
        }

        private void Initialize(float radius, Color color, float durationSeconds)
        {
            targetDiameter = Mathf.Max(0.1f, radius * 2f);
            duration = Mathf.Max(0.05f, durationSeconds);
            initialColor = color;
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetCircleSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 30;
            transform.localScale = Vector3.one * (targetDiameter * 0.15f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(targetDiameter * 0.15f, targetDiameter, progress);
            var color = initialColor;
            color.a *= 1f - progress;
            spriteRenderer.color = color;
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "RuntimeCombatPlaceholderCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
            var radius = TextureSize * 0.48f;
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01(radius - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            circleSprite.name = "RuntimeCombatPlaceholderCircleSprite";
            circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return circleSprite;
        }
    }
}
