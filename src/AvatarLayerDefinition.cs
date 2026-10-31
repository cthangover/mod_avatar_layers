namespace Cthangover.Mods.AvatarLayers
{
    /// <summary>One composited image in logical canvas pixels. Omitted size fills the canvas.</summary>
    public sealed class AvatarLayerDefinition
    {
        /// <summary>Exact PNG path relative to a content mod's avatar_layers directory, including extension.</summary>
        public string Image { get; set; }
        /// <summary>Ascending draw order; equal orders use ordinal binding IDs.</summary>
        public int Order { get; set; }
        /// <summary>Horizontal offset in logical canvas pixels.</summary>
        public float X { get; set; }
        /// <summary>Vertical offset in logical canvas pixels.</summary>
        public float Y { get; set; }
        /// <summary>Image rectangle width; zero uses the canvas width.</summary>
        public float Width { get; set; }
        /// <summary>Image rectangle height; zero uses the canvas height.</summary>
        public float Height { get; set; }
        /// <summary>False removes the visual layer without loading its image.</summary>
        public bool Visible { get; set; } = true;
        /// <summary>Layer alpha in [0,1].</summary>
        public float Opacity { get; set; } = 1;
        /// <summary>Mirror the image inside its rectangle horizontally.</summary>
        public bool FlipH { get; set; }
        /// <summary>Mirror the image inside its rectangle vertically.</summary>
        public bool FlipV { get; set; }
    }
}
