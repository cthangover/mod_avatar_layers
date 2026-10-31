# Layered avatar renderer

`avatar_layers` implements `IAvatarRenderer` from `avatar_base`, registered as `layers` through
`LayeredAvatarRendererFactory`. Content mods depend on `avatar_layers>=1`; no dependency on
`characters_base`, `dialog_base` or Live2D is required.

This is a working discrete renderer, not a character-art generator. Authors supply aligned PNG
layers and a `Kind: avatar` definition. For preset semantics and save state see
[avatar_base](../../avatar_base/docs/en.md).

## Files and bindings

```text
my_content/
  manifest.json                       depends: ["avatar_layers>=1"]
  avatars/marao.json                   Kind: avatar, Renderer: layers
  avatar_layers/marao/body.png
  avatar_layers/marao/eyes_opened.png
  avatar_layers/marao/eyes_closed.png
```

Image paths include `.png` and are relative to `avatar_layers/`. They are resolved through mod
providers, so ZIP mods and dependent content overrides work without direct filesystem loading.
Use a content-specific prefix such as `marao/` to avoid collisions with unrelated content.

A complete resource:

```json
{
  "Kind": "avatar",
  "Items": [{
    "Id": "my_content/marao",
    "Renderer": "layers",
    "BaseValues": {
      "canvas": {"Width": 512, "Height": 1024},
      "layers/body": {"Image": "marao/body.png", "Order": 0}
    },
    "Groups": {
      "eyes_state": {
        "Default": "opened",
        "Presets": {
          "opened": {"Values": {
            "layers/eyes": {"Image": "marao/eyes_opened.png", "Order": 10}
          }},
          "closed": {"Values": {
            "layers/eyes": {"Image": "marao/eyes_closed.png", "Order": 10}
          }}
        }
      }
    }
  }]
}
```

Draw eyes onto transparent images aligned to the same logical canvas as the body. To use cropped
images instead, set `X`, `Y`, `Width`, `Height` on the layer. Coordinates are logical canvas pixels,
not normalized viewport coordinates. The host may scale the visual root to fit a portrait slot.
Root layout belongs to `scenes/avatar_layers/view.tscn`; layer children are data-driven.

Accepted bindings:

| Binding | Meaning |
| --- | --- |
| `canvas` | Object with positive finite Width and Height; defaults are 512 and 1024 |
| `layers/<id>` as string | Exact PNG path, full-canvas rectangle, Order 0 |
| `layers/<id>` as object | AvatarLayerDefinition settings below |
| `layers/<id>` as null | No layer |

Layer properties:

| Property | Default | Meaning |
| --- | --- | --- |
| Image | required when visible | PNG path including extension |
| Order | 0 | Ascending drawing order; ties use ordinal layer binding ID |
| X, Y | 0 | Top-left position in logical pixels |
| Width, Height | 0 | Rectangle dimensions; zero uses the corresponding canvas dimension |
| Visible | true | False omits the layer, without requiring/loading an image |
| Opacity | 1 | Alpha in [0,1] |
| FlipH, FlipV | false | Image mirroring inside its rectangle |

Unknown bindings and unknown object properties are rejected to expose typos. Coordinates must be
finite, dimensions nonnegative. A preset replaces a binding object whole: when changing an image,
repeat any non-default bounds/order needed by that layer. Logical binding order and compositing
layer order are separate: group Priority resolves competing targets; layer Order controls drawing.

## Hosting and changing a preset

```csharp
IAvatar avatar = AvatarCatalog.Create("my_content/marao");
IAvatarRenderer renderer = AvatarRenderers.Create(avatar);
host.AddChild(renderer.View);
avatar.ApplyPresets(new Dictionary<string, string> { ["eyes_state"] = "closed" });
renderer.Apply(avatar.Resolve());
// Before freeing host or unloading the renderer mod:
renderer.Dispose();
```

Run visual operations on the Godot runtime main thread. Positive transition times are accepted
but images switch immediately: no interpolated anatomy or crossfade is implied. Rendering or image
validation errors leave the previous valid visual target intact. Logical preset state is separate:
if an owner changed it before a rendering error, it must handle that error and choose another target.

Each renderer owns its node tree and image cache. Unchanged images are reused across Apply calls;
unused textures are disposed after old nodes release them. No shared engine texture cache is
mutated. Dispose is idempotent, detaches the root and frees nodes before textures. Missing bindings
remove previous layers; hidden layers do not load assets. Applying after disposal is an error.

Not implemented: per-layer motion/tweens, clipping masks, skeletal deformation, automatic blinking,
scenario commands, editor UI, and automatic dialog integration.

## Tests

The opt-in `contracts` suite contains synthetic red/green/blue PNG fixtures, a real avatar entity,
and integration tests for image switching, draw order, positioning, invalid targets, resource
ownership and rendered pixel colors. These fixtures do not mount in normal games.

```text
godot --headless --path . -- --frontend --with-mods=avatar_layers --run-tests --mod=avatar_layers
godot --path . --rendering-method gl_compatibility -- --frontend --with-mods=avatar_layers --itest=avatar_layers_contracts --run-tests --mod=avatar_layers__contracts
```

The second command needs an actual rendering window; the headless dummy renderer cannot verify
pixels. In this workspace execute these commands through the shared Windows watcher job bridge.
