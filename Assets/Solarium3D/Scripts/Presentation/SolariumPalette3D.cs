using UnityEngine;

namespace Solarium.ThreeD
{
    public enum VisualKind3D
    {
        Ground,
        Wall,
        Obstacle,
        Sol,
        SolAccent,
        Food,
        GoldenFood,
        Healing,
        Supply,
        Shelter,
        Hunter,
        Patroller,
        Mud,
        Lava,
        Hazard,
        Foliage,
        Stone
    }

    [CreateAssetMenu(menuName = "Solarium 3D/Visual Palette", fileName = "SolariumPalette3D")]
    public sealed class SolariumPalette3D : ScriptableObject
    {
        [SerializeField] private Material[] materials = new Material[17];

        public Material Get(VisualKind3D kind)
        {
            int index = (int)kind;
            return materials != null && index >= 0 && index < materials.Length
                ? materials[index]
                : null;
        }

        public void Configure(Material[] values)
        {
            materials = values == null ? new Material[17] : (Material[])values.Clone();
        }
    }
}
