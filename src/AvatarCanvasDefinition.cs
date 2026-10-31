namespace Cthangover.Mods.AvatarLayers
{
    /// <summary>Logical canvas size; the host can scale the root to fit its portrait slot.</summary>
    public sealed class AvatarCanvasDefinition
    {
        /// <summary>Positive finite logical width, defaults to 512 pixels.</summary>
        public float Width { get; set; } = 512;
        /// <summary>Positive finite logical height, defaults to 1024 pixels.</summary>
        public float Height { get; set; } = 1024;
    }
}
