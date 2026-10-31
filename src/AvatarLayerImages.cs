using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Godot;

namespace Cthangover.Mods.AvatarLayers
{
    /// <summary>Loads exact PNG paths through mod providers, so ZIP assets and overrides work alike.</summary>
    internal static class AvatarLayerImages
    {
        // The renderer owns the returned texture and must dispose it after detaching all node references.
        internal static Texture2D Load(string path)
        {
            if (!path.EndsWith(".png", StringComparison.Ordinal))
                throw new ArgumentException($"Avatar layer image '{path}' must be an exact .png path.");
            if (!Files.Collect("avatar_layers").TryGetValue(path, out var file))
                throw new ArgumentException($"Avatar layer image '{path}' was not found in avatar_layers.");
            var bytes = ModManager.Instance.ReadFileBinary(file.ModId, file.FullPath);
            if (bytes == null) throw new ArgumentException($"Cannot read avatar layer image '{path}'.");
            using var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok)
                throw new ArgumentException($"Avatar layer image '{path}' is not a valid PNG.");
            return ImageTexture.CreateFromImage(image);
        }
    }
}
