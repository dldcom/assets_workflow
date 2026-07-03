# Consumption Item Grid Workflow

This workflow creates polished single-frame item icons for consumption cards and effects.
It is separate from the character spritesheet workflow because this grid contains different
items, not animation frames.

## Output Layout

Recommended source image:

```text
1024x1024 source image
4 columns x 4 rows
256x256 source cell per item
16 different items total
flat #00ff00 chroma-key background
```

Recommended processed output:

```text
assets/consumptions/<item-name>.png
128x128 transparent PNG per item

assets/consumptions/consumption-items-4x4.png
512x512 transparent atlas
4 columns x 4 rows
128x128 frame per item
```

## Prompt Template

Polished 3D icon version:

```text
Use case: stylized-concept
Asset type: game consumable item icon grid source for later sprite extraction
Primary request: Create one square 4x4 grid of sixteen cute polished game item icons for a children's economy game. Each grid cell contains exactly one standalone object, centered with generous padding, on a perfectly flat solid #00ff00 chroma-key background.
Items in reading order, left to right, top to bottom: 1 pizza slice, 2 ice cream cone, 3 star cape, 4 performance ticket, 5 hamburger, 6 juice cup, 7 toy robot, 8 colored pencil set, 9 story book, 10 soccer ball, 11 flower bouquet, 12 cookie, 13 movie ticket, 14 headphones, 15 board game box, 16 small gift box.
Style/medium: high-quality cute 3D-rendered game icons, soft rounded shapes, clean edges, subtle highlights, polished and charming, not pixel art.
Composition/framing: exact 4 columns by 4 rows, each item isolated in its own equal square cell, no overlap, no borders, no labels, no numbers, no text.
Lighting/mood: bright soft studio lighting on the objects only, no cast shadows on the background.
Color palette: colorful but balanced, suitable for children, avoid neon green in any item.
Constraints: The background must be one uniform #00ff00 color with no shadows, gradients, texture, reflections, floor plane, or lighting variation. Keep each object fully separated from the background with crisp edges and generous padding. Do not use #00ff00 anywhere in the items. No cast shadow, no contact shadow, no reflection, no watermark, no text, no frame numbers, no decorative dividers.
```

Pixel-art version:

```text
Use case: stylized-concept
Asset type: game consumable item icon grid source for later sprite extraction
Primary request: Create one square 4x4 grid of sixteen cute pixel-art game item icons for a children's economy game. Each grid cell contains exactly one standalone object, centered with generous padding, on a perfectly flat solid #00ff00 chroma-key background.
Items in reading order, left to right, top to bottom: 1 pizza slice, 2 ice cream cone, 3 star cape, 4 performance ticket, 5 hamburger, 6 juice cup, 7 toy robot, 8 colored pencil set, 9 story book, 10 soccer ball, 11 flower bouquet, 12 cookie, 13 movie ticket, 14 headphones, 15 board game box, 16 small gift box.
Style/medium: cute polished pixel art game icons, crisp blocky pixel edges, charming toy-like proportions, clear readable silhouettes, colorful but clean, no realistic rendering, no anti-aliased 3D look.
Composition/framing: exact 4 columns by 4 rows, each item isolated in its own equal square cell, wide empty chroma-key space between items, no overlap, no item touching another item, no borders, no labels, no numbers, no text.
Lighting/mood: bright cheerful icon style, simple pixel-art highlights and shadows only.
Color palette: colorful but balanced, suitable for children, avoid neon green in any item.
Constraints: The background must be one uniform #00ff00 color with no shadows, gradients, texture, reflections, floor plane, or lighting variation. Keep every item fully inside the image with generous outer padding. Each item must be separated by wide empty chroma-key space. No item may touch, overlap, or visually connect with another item. Do not use #00ff00 anywhere in the items. No cast shadow, no contact shadow, no reflection, no watermark, no text, no frame numbers, no decorative dividers.
```

## Processing Command

Run from the `assets_workflow` root:

```powershell
Add-Type -Path tools\process-consumption-item-grid.cs -ReferencedAssemblies System.Drawing
[ProcessConsumptionItemGrid]::Main(@(
  "--input", "assets\raw\consumptions\consumption-items-4x4-source.png",
  "--output-dir", "assets\consumptions",
  "--atlas", "assets\consumptions\consumption-items-4x4.png",
  "--transparent", "assets\consumptions\consumption-items-4x4-transparent-source.png",
  "--validation", "assets\consumptions\consumption-items-4x4-validation.png"
))
```

For a separate style test, use a separate raw file and output directory:

```powershell
Add-Type -Path tools\process-consumption-item-grid.cs -ReferencedAssemblies System.Drawing
[ProcessConsumptionItemGrid]::Main(@(
  "--input", "assets\raw\consumptions\consumption-items-pixel-4x4-source.png",
  "--output-dir", "assets\consumptions-pixel",
  "--atlas", "assets\consumptions-pixel\consumption-items-pixel-4x4.png",
  "--transparent", "assets\consumptions-pixel\consumption-items-pixel-4x4-transparent-source.png",
  "--validation", "assets\consumptions-pixel\consumption-items-pixel-4x4-validation.png"
))
```

## Tool Behavior

`tools/process-consumption-item-grid.cs`:

- removes the flat chroma-key background with a soft alpha matte
- despills green edges
- uses the fixed 4x4 grid to name and order items
- finds connected alpha components across the whole image
- assigns each component to the grid cell containing its center
- preserves a component even when it slightly crosses a cell boundary
- crops and centers each item into a consistent transparent frame
- writes individual transparent PNG files
- writes one transparent atlas
- writes a validation image with cell and item bounds

## Validation Checklist

Before importing into a game project, check:

- `assets/consumptions/consumption-items-4x4-validation.png`
  - blue grid lines match the intended 4x4 source cells
  - red boxes surround only the intended item
  - no neighboring item fragment is inside the red box
- `assets/consumptions/consumption-items-4x4.png`
  - all 16 icons are visible
  - transparent background is clean
  - item scale feels consistent
- individual item files
  - each file is `128x128`
  - all four corners are transparent
  - no obvious green fringe remains

If a neighboring item leaks into a cell, check whether both objects became one connected component.
If that happens, regenerate the source with more spacing between items.
If an item edge is clipped at the outside edge of the entire image, decrease `--cell-inset` or regenerate the source with more outer padding.

## Current Item Order

```text
row 1: pizza, icecream, star-cape, performance-ticket
row 2: hamburger, juice, toy-robot, colored-pencils
row 3: story-book, soccer-ball, flower-bouquet, cookie
row 4: movie-ticket, headphones, board-game, gift-box
```
