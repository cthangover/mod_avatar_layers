using System.Text.Json;
using Cthangover.Core.Mods.Testing;

namespace Cthangover.Mods.AvatarLayers.Tests
{
    /// <summary>Layer and canvas authoring DTOs preserve every visual property through JSON.</summary>
    public sealed class AvatarLayerDefinitionTests
    {
        [ModTest("avatar_layers/layer_and_canvas_json_roundtrip")]
        public static void RoundTrip(ModTestContext ctx)
        {
            var layer = new AvatarLayerDefinition { Image = "sample/eyes.png", Order = -2, X = 3, Y = -4,
                Width = 12, Height = 16, Visible = false, Opacity = 0.25f, FlipH = true, FlipV = true };
            var copy = JsonSerializer.Deserialize<AvatarLayerDefinition>(JsonSerializer.Serialize(layer));
            ModAssert.Equal(layer.Image, copy.Image); ModAssert.Equal(layer.Order, copy.Order);
            ModAssert.CloseTo(layer.X, copy.X); ModAssert.CloseTo(layer.Y, copy.Y);
            ModAssert.CloseTo(layer.Width, copy.Width); ModAssert.CloseTo(layer.Height, copy.Height);
            ModAssert.False(copy.Visible); ModAssert.CloseTo(layer.Opacity, copy.Opacity);
            ModAssert.True(copy.FlipH); ModAssert.True(copy.FlipV);
            var canvas = JsonSerializer.Deserialize<AvatarCanvasDefinition>(JsonSerializer.Serialize(new AvatarCanvasDefinition { Width = 640, Height = 960 }));
            ModAssert.CloseTo(640, canvas.Width); ModAssert.CloseTo(960, canvas.Height);
            ModAssert.True(new AvatarLayerDefinition().Visible);
            ModAssert.CloseTo(1, new AvatarLayerDefinition().Opacity);
        }
    }
}
