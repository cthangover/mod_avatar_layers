using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Cthangover.Mods.Avatars;
using Godot;

namespace Cthangover.Mods.AvatarLayers
{
    /// <summary>
    /// Renders complete preset targets as ordered TextureRects. Assets switch discretely, including
    /// for positive transition times. Each instance owns its root and texture cache; unchanged images
    /// are reused, and obsolete images are released only after their nodes have been detached.
    /// </summary>
    public sealed class LayeredAvatarRenderer : IAvatarRenderer
    {
        private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        private readonly Control root;
        private Dictionary<string, Texture2D> textures = new(StringComparer.Ordinal);
        private bool disposed;

        /// <summary>Creates the visual root from the mod prefab. Requires the Godot main thread.</summary>
        public LayeredAvatarRenderer()
        {
            RequireRuntime();
            root = new ModScenePrefab("scenes/avatar_layers/view.tscn").Instantiate<Control>()
                ?? throw new InvalidOperationException("Layered avatar view prefab is unavailable.");
        }

        /// <inheritdoc/>
        public Control View => root;

        /// <summary>
        /// Validates and prepares an entire target before replacing visible children. Malformed
        /// targets or missing images leave the last valid display intact. Only layers/* and canvas
        /// bindings are accepted, making content typos actionable instead of silently ignored.
        /// </summary>
        public void Apply(AvatarFrame target, double transitionSeconds = 0)
        {
            RequireRuntime();
            if (disposed) throw new ObjectDisposedException(nameof(LayeredAvatarRenderer));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target.Renderer != "layers") throw new ArgumentException("Layered renderer requires Renderer=layers.");
            if (!double.IsFinite(transitionSeconds) || transitionSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
            var canvas = new AvatarCanvasDefinition();
            var layers = new Dictionary<string, AvatarLayerDefinition>(StringComparer.Ordinal);
            foreach (var binding in target.Values)
            {
                if (binding.Key == "canvas")
                    canvas = binding.Value.Deserialize<AvatarCanvasDefinition>(Options)
                        ?? throw new ArgumentException("Avatar canvas cannot be null.");
                else if (binding.Key.StartsWith("layers/", StringComparison.Ordinal) && binding.Key.Length > 7)
                {
                    if (binding.Value.ValueKind == JsonValueKind.Null) continue;
                    var layer = binding.Value.ValueKind == JsonValueKind.String
                        ? new AvatarLayerDefinition { Image = binding.Value.GetString() }
                        : binding.Value.Deserialize<AvatarLayerDefinition>(Options);
                    if (layer == null) throw new ArgumentException($"Invalid avatar layer '{binding.Key}'.");
                    ValidateLayer(binding.Key, layer);
                    if (layer.Visible) layers.Add(binding.Key, layer);
                }
                else throw new ArgumentException($"Unknown layered avatar binding '{binding.Key}'.");
            }
            if (!float.IsFinite(canvas.Width) || !float.IsFinite(canvas.Height) || canvas.Width <= 0 || canvas.Height <= 0)
                throw new ArgumentException("Avatar canvas dimensions must be positive and finite.");

            var pending = new List<TextureRect>();
            var nextTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            var committed = false;
            try
            {
                foreach (var entry in layers.OrderBy(Order).ThenBy(Name, StringComparer.Ordinal))
                {
                    var layer = entry.Value;
                    if (!nextTextures.TryGetValue(layer.Image, out var texture))
                    {
                        if (!textures.TryGetValue(layer.Image, out texture)) texture = AvatarLayerImages.Load(layer.Image);
                        nextTextures.Add(layer.Image, texture);
                    }
                    var node = new TextureRect();
                    pending.Add(node);
                    node.Name = "Layer" + (pending.Count - 1);
                    node.MouseFilter = Control.MouseFilterEnum.Ignore;
                    node.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                    node.StretchMode = TextureRect.StretchModeEnum.Scale;
                    node.Texture = texture;
                    node.Position = new Vector2(layer.X, layer.Y);
                    node.Size = new Vector2(layer.Width == 0 ? canvas.Width : layer.Width,
                        layer.Height == 0 ? canvas.Height : layer.Height);
                    node.FlipH = layer.FlipH;
                    node.FlipV = layer.FlipV;
                    node.Modulate = new Color(1, 1, 1, layer.Opacity);
                    node.SetMeta("avatar_layer", entry.Key.Substring(7));
                }
                ClearChildren();
                var canvasSize = new Vector2(canvas.Width, canvas.Height);
                root.CustomMinimumSize = canvasSize;
                root.Size = canvasSize;
                foreach (var node in pending) root.AddChild(node);
                foreach (var old in textures)
                    if (!nextTextures.ContainsKey(old.Key)) old.Value.Dispose();
                textures = nextTextures;
                committed = true;
            }
            finally
            {
                if (!committed)
                {
                    foreach (var node in pending)
                    {
                        node.GetParent()?.RemoveChild(node);
                        node.Texture = null;
                        node.Free();
                    }
                    foreach (var texture in nextTextures)
                        if (!textures.ContainsKey(texture.Key)) texture.Value.Dispose();
                }
            }
        }

        /// <summary>Detaches and frees the owned visual tree before releasing owned textures. Idempotent.</summary>
        public void Dispose()
        {
            RequireRuntime();
            if (disposed) return;
            disposed = true;
            if (GodotObject.IsInstanceValid(root))
            {
                ClearChildren();
                root.GetParent()?.RemoveChild(root);
                root.Free();
            }
            foreach (var texture in textures.Values) texture.Dispose();
            textures.Clear();
        }

        private void ClearChildren()
        {
            foreach (var child in root.GetChildren())
            {
                root.RemoveChild(child);
                if (child is TextureRect image) image.Texture = null;
                child.Free();
            }
        }

        private static void RequireRuntime()
        {
            if (!GameEnvironment.IsRuntime || !GameEnvironment.IsMainThread)
                throw new InvalidOperationException("Avatar visuals require the Godot runtime main thread.");
        }

        private static void ValidateLayer(string id, AvatarLayerDefinition layer)
        {
            if (layer.Visible && string.IsNullOrWhiteSpace(layer.Image))
                throw new ArgumentException($"Avatar layer '{id}' requires Image.");
            if (!float.IsFinite(layer.X) || !float.IsFinite(layer.Y) ||
                !float.IsFinite(layer.Width) || !float.IsFinite(layer.Height) || layer.Width < 0 || layer.Height < 0 ||
                !float.IsFinite(layer.Opacity) || layer.Opacity < 0 || layer.Opacity > 1)
                throw new ArgumentException($"Avatar layer '{id}' has invalid bounds or opacity.");
        }

        private static int Order(KeyValuePair<string, AvatarLayerDefinition> layer) => layer.Value.Order;
        private static string Name(KeyValuePair<string, AvatarLayerDefinition> layer) => layer.Key;
    }
}
