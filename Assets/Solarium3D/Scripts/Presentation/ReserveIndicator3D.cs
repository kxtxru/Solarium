using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Solarium.ThreeD
{
    [DisallowMultipleComponent]
    public sealed class ReserveIndicator3D : MonoBehaviour
    {
        private readonly List<GameObject> rationIcons = new();
        private Transform visualRoot;
        private Text countText;
        private Light reserveGlow;
        private Camera cachedCamera;

        public int DisplayedCount { get; private set; }
        public bool HasActiveGlow => reserveGlow != null && reserveGlow.enabled;

        public void Initialize(SolariumPalette3D palette)
        {
            if (visualRoot != null)
                return;

            var root = new GameObject("Reserved Rations");
            root.layer = SolariumLayers3D.Effects;
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;

            for (int i = 0; i < 3; i++)
            {
                Transform icon = LowPolyFactory3D.CreatePart(
                    visualRoot,
                    $"Ration {i + 1}",
                    PrimitiveType.Sphere,
                    new Vector3((i - 1) * 0.32f, 1.62f + Mathf.Abs(i - 1) * 0.08f, 0f),
                    new Vector3(0.24f, 0.28f, 0.24f),
                    palette.Get(VisualKind3D.Food));
                rationIcons.Add(icon.gameObject);
            }

            var labelObject = new GameObject("Ration Count", typeof(RectTransform), typeof(Canvas), typeof(Text));
            labelObject.layer = SolariumLayers3D.Effects;
            labelObject.transform.SetParent(visualRoot, false);
            RectTransform rect = (RectTransform)labelObject.transform;
            rect.localPosition = new Vector3(0f, 2.02f, 0f);
            rect.localScale = Vector3.one * 0.006f;
            rect.sizeDelta = new Vector2(180f, 70f);
            Canvas canvas = labelObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            countText = labelObject.GetComponent<Text>();
            countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countText.fontSize = 46;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = new Color(0.75f, 1f, 0.65f);
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.verticalOverflow = VerticalWrapMode.Overflow;

            var glowObject = new GameObject("Reserve Glow", typeof(Light));
            glowObject.layer = SolariumLayers3D.Effects;
            glowObject.transform.SetParent(visualRoot, false);
            glowObject.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            reserveGlow = glowObject.GetComponent<Light>();
            reserveGlow.type = LightType.Point;
            reserveGlow.color = new Color(0.52f, 1f, 0.42f);
            reserveGlow.range = 3f;
            reserveGlow.intensity = 1.35f;
            reserveGlow.shadows = LightShadows.None;
            SetCount(0);
        }

        public void SetCount(int value)
        {
            DisplayedCount = Mathf.Max(0, value);
            if (visualRoot == null)
                return;
            visualRoot.gameObject.SetActive(DisplayedCount > 0);
            for (int i = 0; i < rationIcons.Count; i++)
                rationIcons[i].SetActive(i < Mathf.Min(DisplayedCount, rationIcons.Count));
            if (countText != null)
                countText.text = $"×{DisplayedCount}";
            if (reserveGlow != null)
                reserveGlow.enabled = DisplayedCount > 0;
        }

        private void LateUpdate()
        {
            if (visualRoot == null || !visualRoot.gameObject.activeInHierarchy)
                return;
            cachedCamera ??= Camera.main;
            if (cachedCamera == null)
                return;
            Vector3 direction = visualRoot.position - cachedCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                visualRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (reserveGlow != null)
                reserveGlow.intensity = 1.25f + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.18f;
        }
    }
}
