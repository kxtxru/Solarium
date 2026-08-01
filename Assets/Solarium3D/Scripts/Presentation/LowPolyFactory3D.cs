using UnityEngine;
using UnityEngine.Rendering;

namespace Solarium.ThreeD
{
    public static class LowPolyFactory3D
    {
        public static Transform CreatePart(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Vector3? localEuler = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.layer = SolariumLayers3D.Visual;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEuler ?? Vector3.zero;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying)
                    Object.Destroy(collider);
                else
                    Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return part.transform;
        }

        public static Transform BuildSol(Transform parent, SolariumPalette3D palette)
        {
            Transform root = NewVisualRoot(parent, "Sol Visual");
            CreatePart(root, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f),
                new Vector3(0.75f, 0.85f, 0.75f), palette.Get(VisualKind3D.Sol));
            CreatePart(root, "Face", PrimitiveType.Sphere, new Vector3(0f, 0.66f, 0.28f),
                new Vector3(0.52f, 0.42f, 0.34f), palette.Get(VisualKind3D.SolAccent));
            CreatePart(root, "Eye L", PrimitiveType.Sphere, new Vector3(-0.14f, 0.73f, 0.44f),
                new Vector3(0.065f, 0.09f, 0.045f), palette.Get(VisualKind3D.Stone));
            CreatePart(root, "Eye R", PrimitiveType.Sphere, new Vector3(0.14f, 0.73f, 0.44f),
                new Vector3(0.065f, 0.09f, 0.045f), palette.Get(VisualKind3D.Stone));
            CreatePart(root, "Leaf", PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f),
                new Vector3(0.12f, 0.32f, 0.42f), palette.Get(VisualKind3D.Foliage),
                new Vector3(18f, 0f, 35f));
            return root;
        }

        public static Transform BuildEnemy(Transform parent, SolariumPalette3D palette, bool patroller)
        {
            Transform root = NewVisualRoot(parent, patroller ? "Patroller Visual" : "Hunter Visual");
            Material body = palette.Get(patroller ? VisualKind3D.Patroller : VisualKind3D.Hunter);
            CreatePart(root, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f),
                new Vector3(0.82f, 0.62f, 0.92f), body);
            CreatePart(root, "Horn L", PrimitiveType.Cylinder, new Vector3(-0.3f, 0.84f, 0.1f),
                new Vector3(0.09f, 0.28f, 0.09f), body, new Vector3(0f, 0f, -25f));
            CreatePart(root, "Horn R", PrimitiveType.Cylinder, new Vector3(0.3f, 0.84f, 0.1f),
                new Vector3(0.09f, 0.28f, 0.09f), body, new Vector3(0f, 0f, 25f));
            parent.gameObject.AddComponent<PlanarMotionVisual3D>().Configure(root, 0.035f, true);
            return root;
        }

        public static Transform BuildResource(Transform parent, SolariumPalette3D palette, VisualKind3D kind)
        {
            Transform root = NewVisualRoot(parent, $"{kind} Visual");
            switch (kind)
            {
                case VisualKind3D.Food:
                    CreatePart(root, "Berry", PrimitiveType.Sphere, new Vector3(0f, 0.46f, 0f),
                        new Vector3(0.52f, 0.58f, 0.52f), palette.Get(kind));
                    CreatePart(root, "Food Leaf", PrimitiveType.Cube, new Vector3(0.12f, 0.82f, 0f),
                        new Vector3(0.08f, 0.16f, 0.3f), palette.Get(VisualKind3D.Foliage),
                        new Vector3(15f, 25f, 42f));
                    break;
                case VisualKind3D.Healing:
                    BuildFlower(root, palette);
                    break;
                case VisualKind3D.Shelter:
                    BuildSanctuary(root, palette);
                    break;
                case VisualKind3D.Supply:
                    CreatePart(root, "Ration Fruit", PrimitiveType.Sphere, new Vector3(0f, 0.48f, 0f),
                        new Vector3(0.5f, 0.56f, 0.5f), palette.Get(VisualKind3D.Food));
                    CreatePart(root, "Ration Wrap", PrimitiveType.Cube, new Vector3(0f, 0.46f, 0f),
                        new Vector3(0.58f, 0.18f, 0.58f), palette.Get(kind), new Vector3(0f, 28f, 0f));
                    CreatePart(root, "Ration Leaf", PrimitiveType.Cube, new Vector3(0.12f, 0.8f, 0f),
                        new Vector3(0.08f, 0.16f, 0.28f), palette.Get(VisualKind3D.Foliage),
                        new Vector3(15f, 25f, 42f));
                    break;
                case VisualKind3D.GoldenFood:
                    CreatePart(root, "Main Crystal", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f),
                        new Vector3(0.42f, 0.72f, 0.42f), palette.Get(kind), new Vector3(20f, 45f, 20f));
                    CreatePart(root, "Crystal Shard", PrimitiveType.Cube, new Vector3(0.28f, 0.35f, -0.08f),
                        new Vector3(0.2f, 0.46f, 0.2f), palette.Get(kind), new Vector3(-18f, 28f, -20f));
                    break;
                default:
                    CreatePart(root, kind.ToString(), PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f),
                        new Vector3(0.55f, 0.7f, 0.55f), palette.Get(kind));
                    break;
            }
            parent.gameObject.AddComponent<PlanarMotionVisual3D>().Configure(root, 0.08f, false);
            return root;
        }

        public static Transform BuildPatch(Transform parent, SolariumPalette3D palette, VisualKind3D kind)
        {
            Transform root = NewVisualRoot(parent, $"{kind} Visual");
            CreatePart(root, kind.ToString(), PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f),
                new Vector3(1f, 0.08f, 1f), palette.Get(kind));
            return root;
        }

        public static Transform BuildSolid(Transform parent, SolariumPalette3D palette, VisualKind3D kind)
        {
            Transform root = NewVisualRoot(parent, $"{kind} Visual");
            PrimitiveType shape = kind == VisualKind3D.Obstacle ? PrimitiveType.Sphere : PrimitiveType.Cube;
            Vector3 shapeScale = kind == VisualKind3D.Obstacle
                ? new Vector3(0.92f, 0.86f, 0.92f)
                : Vector3.one;
            CreatePart(root, kind.ToString(), shape, Vector3.zero, shapeScale, palette.Get(kind),
                kind == VisualKind3D.Obstacle ? new Vector3(0f, 18f, 0f) : null);
            return root;
        }

        public static Transform BuildDecoration(Transform parent, SolariumPalette3D palette, VisualKind3D kind)
        {
            Transform root = NewVisualRoot(parent, $"{kind} Visual");
            if (kind == VisualKind3D.Stone)
            {
                CreatePart(root, "Stone", PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0f),
                    new Vector3(1f, 0.55f, 0.82f), palette.Get(kind), new Vector3(0f, 24f, 8f));
                return root;
            }

            Material foliage = palette.Get(VisualKind3D.Foliage);
            CreatePart(root, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.13f, 0.38f, 0.13f), foliage);
            CreatePart(root, "Crown", PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0f),
                new Vector3(0.74f, 0.58f, 0.72f), foliage);
            CreatePart(root, "Crown L", PrimitiveType.Sphere, new Vector3(-0.38f, 0.72f, 0.02f),
                new Vector3(0.48f, 0.4f, 0.5f), foliage);
            CreatePart(root, "Crown R", PrimitiveType.Sphere, new Vector3(0.38f, 0.76f, -0.04f),
                new Vector3(0.5f, 0.42f, 0.52f), foliage);
            return root;
        }

        private static void BuildFlower(Transform root, SolariumPalette3D palette)
        {
            Material petals = palette.Get(VisualKind3D.Healing);
            CreatePart(root, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0f),
                new Vector3(0.07f, 0.28f, 0.07f), palette.Get(VisualKind3D.Foliage));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                Vector3 position = new(Mathf.Cos(angle) * 0.25f, 0.62f, Mathf.Sin(angle) * 0.25f);
                CreatePart(root, $"Petal {i}", PrimitiveType.Sphere, position,
                    new Vector3(0.3f, 0.16f, 0.22f), petals, new Vector3(0f, -i * 72f, 0f));
            }
            CreatePart(root, "Flower Heart", PrimitiveType.Sphere, new Vector3(0f, 0.64f, 0f),
                new Vector3(0.2f, 0.18f, 0.2f), palette.Get(VisualKind3D.Sol));
        }

        private static void BuildSanctuary(Transform root, SolariumPalette3D palette)
        {
            Material material = palette.Get(VisualKind3D.Shelter);
            CreatePart(root, "Sanctuary Base", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f),
                new Vector3(0.95f, 0.12f, 0.95f), palette.Get(VisualKind3D.Obstacle));
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                Vector3 position = new(Mathf.Cos(angle) * 0.55f, 0.72f, Mathf.Sin(angle) * 0.55f);
                CreatePart(root, $"Sanctuary Pillar {i}", PrimitiveType.Cylinder, position,
                    new Vector3(0.12f, 0.62f, 0.12f), material, new Vector3(0f, 0f, (i - 1) * 8f));
            }
            CreatePart(root, "Sanctuary Crown", PrimitiveType.Sphere, new Vector3(0f, 1.25f, 0f),
                new Vector3(0.85f, 0.22f, 0.85f), material);
        }

        private static Transform NewVisualRoot(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.layer = SolariumLayers3D.Visual;
            root.transform.SetParent(parent, false);
            return root.transform;
        }
    }
}
