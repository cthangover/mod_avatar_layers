using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Cthangover.Core.Mods.Testing;
using Cthangover.Mods.Avatars;
using Godot;

namespace Cthangover.Mods.AvatarLayers.Contracts
{
    /// <summary>Real mod image loading and node composition, including a rendered pixel comparison.</summary>
    public sealed class LayeredAvatarRendererTests
    {
        [ModTest("avatar_layers/discovery_presets_order_and_removal")]
        public static void Composition(ModTestContext ctx)
        {
            var avatar = AvatarCatalog.Create("avatar_layers_contracts/sample");
            using var renderer = AvatarRenderers.Create(avatar);
            var view = renderer.View;
            ModAssert.CloseTo(4, view.Size.X); ModAssert.CloseTo(4, view.Size.Y);
            ModAssert.Equal(2, view.GetChildCount());
            var body = view.GetChild<TextureRect>(0); var eyes = view.GetChild<TextureRect>(1);
            ModAssert.Equal("body", body.GetMeta("avatar_layer").AsString());
            ModAssert.Equal("eyes", eyes.GetMeta("avatar_layer").AsString());
            ModAssert.CloseTo(1, eyes.Position.X); ModAssert.CloseTo(1, eyes.Position.Y);
            ModAssert.CloseTo(2, eyes.Size.X);
            var bodyTexture = body.Texture;
            avatar.ApplyPresets(new Dictionary<string, string> { ["eyes_state"] = "closed" });
            renderer.Apply(avatar.Resolve(), 0.15);
            ModAssert.True(ReferenceEquals(bodyTexture, view.GetChild<TextureRect>(0).Texture), "unchanged images reused");
            using (var image = view.GetChild<TextureRect>(1).Texture.GetImage())
                ModAssert.CloseTo(1, image.GetPixel(0, 0).B, what: "closed preset selects blue PNG");
            avatar.ApplyPresets(new Dictionary<string, string> { ["eyes_state"] = "absent" });
            renderer.Apply(avatar.Resolve());
            ModAssert.Equal(1, view.GetChildCount(), "null binding removes prior layer");
            avatar.ApplyPresets(new Dictionary<string, string> { ["eyes_state"] = "hidden" });
            renderer.Apply(avatar.Resolve());
            ModAssert.Equal(1, view.GetChildCount(), "invisible layer needs no image");
        }

        [ModTest("avatar_layers/rejects_invalid_targets_without_changing_view")]
        public static void InvalidTargets(ModTestContext ctx)
        {
            using var renderer = AvatarRenderers.Create(AvatarCatalog.Create("avatar_layers_contracts/sample"));
            var texture = renderer.View.GetChild<TextureRect>(0).Texture;
            Reject(renderer, "{\"canvas\":{\"Width\":0,\"Height\":4}}");
            Reject(renderer, "{\"layers/eyes\":{\"Image\":\"test/opened.png\",\"Opacity\":2}}");
            Reject(renderer, "{\"layers/eyes\":{\"Image\":\"test/opened.png\",\"Width\":-1}}");
            Reject(renderer, "{\"layers/eyes\":{\"Image\":\"test/opened.png\",\"Visble\":false}}");
            Reject(renderer, "{\"layers/eyes\":{}}");
            Reject(renderer, "{\"layer/eyes\":\"test/opened.png\"}");
            Reject(renderer, "{\"layers/eyes\":\"test/missing.png\"}");
            Reject(renderer, "{\"layers/body\":\"test/body.png\",\"layers/eyes\":\"test/missing.png\"}");
            var invalidTime = false;
            try { renderer.Apply(AvatarCatalog.Create("avatar_layers_contracts/sample").Resolve(), double.NaN); }
            catch (ArgumentOutOfRangeException) { invalidTime = true; }
            ModAssert.True(invalidTime);
            ModAssert.Equal(2, renderer.View.GetChildCount());
            ModAssert.True(ReferenceEquals(texture, renderer.View.GetChild<TextureRect>(0).Texture));
        }

        [ModTest("avatar_layers/canvas_can_shrink_after_a_larger_target")]
        public static void Resize(ModTestContext ctx)
        {
            using var renderer = AvatarRenderers.Create(AvatarCatalog.Create("avatar_layers_contracts/sample"));
            var definition = new AvatarDefinition { ID = "test/resize", Renderer = "layers",
                BaseValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    "{\"canvas\":{\"Width\":2,\"Height\":3},\"layers/body\":\"test/body.png\"}") };
            renderer.Apply(new AvatarInstance(definition).Resolve());
            ModAssert.CloseTo(2, renderer.View.Size.X);
            ModAssert.CloseTo(3, renderer.View.Size.Y);
            ModAssert.CloseTo(2, renderer.View.CustomMinimumSize.X);
            ModAssert.CloseTo(2, renderer.View.GetChild<TextureRect>(0).Size.X);
            ModAssert.CloseTo(3, renderer.View.GetChild<TextureRect>(0).Size.Y);
        }

        [ModTest("avatar_layers/dispose_detaches_root_and_releases_owned_resources")]
        public static void Ownership(ModTestContext ctx)
        {
            var host = new Control();
            var renderer = AvatarRenderers.Create(AvatarCatalog.Create("avatar_layers_contracts/sample"));
            var view = renderer.View;
            try
            {
                host.AddChild(view);
                var texture = view.GetChild<TextureRect>(0).Texture;
                renderer.Dispose(); renderer.Dispose();
                ModAssert.Equal(0, host.GetChildCount());
                ModAssert.False(GodotObject.IsInstanceValid(view));
                ModAssert.False(GodotObject.IsInstanceValid(texture));
                var rejected = false;
                try { renderer.Apply(AvatarCatalog.Create("avatar_layers_contracts/sample").Resolve()); }
                catch (ObjectDisposedException) { rejected = true; }
                ModAssert.True(rejected);
            }
            finally { renderer.Dispose(); host.Free(); }
        }

        [ModTest("avatar_layers/rendered_pixels_follow_preset", TimeoutSeconds = 15)]
        public static async Task Pixels(ModTestContext ctx)
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var viewport = new SubViewport { Size = new Vector2I(4, 4), TransparentBg = true,
                Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            var avatar = AvatarCatalog.Create("avatar_layers_contracts/sample");
            using var renderer = AvatarRenderers.Create(avatar);
            try
            {
                tree.Root.AddChild(viewport);
                viewport.AddChild(renderer.View);
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using (var image = viewport.GetTexture().GetImage())
                {
                    ModAssert.CloseTo(1, image.GetPixel(0, 0).R, epsilon: 0.01, what: "red base remains visible");
                    ModAssert.CloseTo(1, image.GetPixel(1, 1).G, epsilon: 0.01, what: "green eyes draw above base");
                }
                avatar.ApplyPresets(new Dictionary<string, string> { ["eyes_state"] = "closed" });
                renderer.Apply(avatar.Resolve());
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var changed = viewport.GetTexture().GetImage();
                ModAssert.CloseTo(1, changed.GetPixel(1, 1).B, epsilon: 0.01, what: "blue eyes replace green eyes");
            }
            finally
            {
                renderer.Dispose();
                viewport.GetParent()?.RemoveChild(viewport); viewport.Free();
            }
        }

        private static void Reject(IAvatarRenderer renderer, string values)
        {
            var definition = new AvatarDefinition { ID = "test/bad", Renderer = "layers",
                BaseValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(values) };
            var rejected = false;
            try { renderer.Apply(new AvatarInstance(definition).Resolve()); }
            catch (ArgumentException) { rejected = true; }
            catch (JsonException) { rejected = true; }
            ModAssert.True(rejected, "invalid layered target must fail");
        }
    }
}
