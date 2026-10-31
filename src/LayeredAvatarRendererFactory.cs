using Cthangover.Mods.Avatars;

namespace Cthangover.Mods.AvatarLayers
{
    /// <summary>Discovered by avatar_base through the normal mod assembly consumer lifecycle.</summary>
    public sealed class LayeredAvatarRendererFactory : IAvatarRendererFactory
    {
        /// <inheritdoc/>
        public string Id => "layers";
        /// <inheritdoc/>
        public IAvatarRenderer Create() => new LayeredAvatarRenderer();
    }
}
