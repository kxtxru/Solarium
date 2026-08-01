using System.Collections.Generic;
using UnityEngine;

namespace Solarium
{
    public enum SimpleShape
    {
        Circle,
        Square,
        Triangle,
        Diamond,
        Cross
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SimpleShapeRenderer : MonoBehaviour
    {
        [SerializeField] private SimpleShape shape = SimpleShape.Circle;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private int sortingOrder;

        private static readonly Dictionary<SimpleShape, Sprite> Cache = new();

        public void Configure(SimpleShape newShape, Color newColor, int order = 0)
        {
            shape = newShape;
            color = newColor;
            sortingOrder = order;
            Apply();
        }

        private void Awake() => Apply();
        private void OnValidate() => Apply();

        private void Apply()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer == null)
                return;

            renderer.sprite = GetSprite(shape);
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetSprite(SimpleShape requested)
        {
            if (Cache.TryGetValue(requested, out Sprite sprite) && sprite != null)
                return sprite;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Solarium_{requested}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    bool filled = requested switch
                    {
                        SimpleShape.Circle => nx * nx + ny * ny <= 0.92f,
                        SimpleShape.Triangle => ny >= -0.85f && ny <= 0.9f
                            && Mathf.Abs(nx) <= (0.9f - ny) * 0.58f,
                        SimpleShape.Diamond => Mathf.Abs(nx) + Mathf.Abs(ny) <= 0.9f,
                        SimpleShape.Cross => (Mathf.Abs(nx) < 0.25f && Mathf.Abs(ny) < 0.85f)
                            || (Mathf.Abs(ny) < 0.25f && Mathf.Abs(nx) < 0.85f),
                        _ => Mathf.Abs(nx) <= 0.92f && Mathf.Abs(ny) <= 0.92f
                    };
                    pixels[y * size + x] = filled
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            sprite.name = requested.ToString();
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[requested] = sprite;
            return sprite;
        }
    }
}
