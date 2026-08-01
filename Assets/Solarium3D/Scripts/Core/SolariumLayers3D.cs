namespace Solarium.ThreeD
{
    public static class SolariumLayers3D
    {
        public const int Gameplay = 8;
        public const int Ground = 9;
        public const int Visual = 10;
        public const int Effects = 11;
        public const int SensorMask = (1 << Gameplay) | (1 << Ground);
    }
}
