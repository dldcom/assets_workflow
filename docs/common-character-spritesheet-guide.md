# Common Character Spritesheet Guide

This guide is project-agnostic. Its goal is to generate one clean character spritesheet image that can later be imported, sliced, or analyzed by any game project.

## Goal

Create a single image that contains:

- one character only
- consistent character design across every frame
- transparent or removable flat-color background
- clearly separated frames
- predictable row-based animation ordering
- enough padding between frames for automatic slicing

The consuming project can decide how to slice frames, assign animations, scale sprites, and map frame indexes.

## Recommended Base Format

Use this format unless a project needs something else:

- Grid: `4 columns x 4 rows`
- Total frames: `16`
- Suggested target frame size after processing: `48x64`
- Source image background: flat `#00ff00` chroma key
- No labels, numbers, borders, shadows, or scenery

The generated source image does not need to be exactly `192x256`. It can be larger, as long as the frames are cleanly separated and arranged in a predictable grid.

Core layout rule:

```text
Each row is one animation.
Each column is one timing frame inside that row's animation.
Never use columns as separate animation groups.
```

## Walking Spritesheet Layout

Recommended row order:

```text
row 1: down/front walk, 4 frames
row 2: up/back walk, 4 frames
row 3: left-facing walk, 4 frames
row 4: right-facing walk, 4 frames
```

Recommended frame order inside each row:

```text
column 1: step frame 1
column 2: step frame 2
column 3: step frame 3
column 4: step frame 4
```

If the image generator swaps left and right rows, keep the sheet if the art is good and fix the animation mapping in the consuming project.

## Action Spritesheet Layout

For non-walking animations, keep the same row-based rule. Each row is one action, and the four columns are the action's animation frames.

```text
row 1: cheering / arms raised, 4 frames
row 2: jumping, 4 frames
row 3: dancing, 4 frames
row 4: head pat reaction, 4 frames
```

## Image Generation Prompt Template

Replace the bracketed parts.

```text
Use case: stylized-concept
Asset type: game character spritesheet

Primary request:
Create one complete pixel-art spritesheet image of [CHARACTER DESCRIPTION].

Style:
High-quality retro pixel art for a 2D RPG game. Crisp pixel edges, limited color palette, readable small character silhouette, cute and expressive, consistent design in every frame.

Grid:
Create exactly 4 columns and 4 rows, 16 total frames. Use one character only. Keep the same character scale, proportions, colors, and style in every frame.

Layout:
Rows are animations in this exact order:
row 1: [ANIMATION 1], 4 frames
row 2: [ANIMATION 2], 4 frames
row 3: [ANIMATION 3], 4 frames
row 4: [ANIMATION 4], 4 frames

Columns are animation timing frames from left to right:
column 1: frame 1
column 2: frame 2
column 3: frame 3
column 4: frame 4

Important:
Do not use columns as separate actions. Every animation must occupy one full row.

Background:
Use a perfectly flat solid #00ff00 chroma-key background across the entire image. No shadows, no floor, no gradients, no texture, no scenery.

Frame separation:
Add wide empty green padding between every frame, row, and column. Keep every sprite fully separated from neighboring sprites. No overlap between frames.

Constraints:
No text, no labels, no frame numbers, no borders, no watermark. No extra characters. No props unless requested. The character must stay centered inside each frame with generous padding.

Avoid:
Realistic photo, vector icon style, 3D render, painterly illustration, blurry anti-pixel edges, background scenery, merged frames, inconsistent character size, inconsistent costume or colors.
```

## Walking Prompt Example

```text
Create one complete pixel-art spritesheet image of a cute small dog walking on two legs, clearly dog-like and not human-like. No clothes, no accessories, no shoes. The dog has floppy ears, a round head, short body, small paws, wagging tail, warm brown and tan fur, and a cheerful cute expression.

Rows are animations in this exact order:
row 1: front/down walk, 4 frames
row 2: back/up walk, 4 frames
row 3: left-facing side walk, 4 frames
row 4: right-facing side walk, 4 frames

Use a perfectly flat solid #00ff00 chroma-key background. Add wide empty green padding between every frame. No text, no labels, no borders, no watermark.
```

## Quality Checklist

Before using the spritesheet in a project, verify:

- The sheet contains exactly 16 frames.
- Every frame contains the same character.
- The character size is consistent across frames.
- Frames are clearly separated with background padding.
- No character parts cross into another frame.
- There is no text, numbering, border, shadow, or scenery.
- The background is a single removable color.
- Each row is one animation.
- Columns only represent timing frames within the row.

## Common Failure Cases

- **Rows overlap:** Regenerate with stronger spacing language.
- **Left/right directions swapped:** Fix the consuming project's frame mapping.
- **Character changes between frames:** Regenerate and emphasize same character, same colors, same proportions.
- **Background is not flat:** Regenerate with "perfectly flat solid #00ff00, no shadows, no gradients."
- **Frames are not evenly spaced:** Use connected-component detection or manually crop the frames.
- **Generated image includes labels or borders:** Regenerate with stronger "no text, no labels, no frame numbers, no borders."

## Optional Post-Processing

If a project needs a fixed-size atlas, process the generated image after validation:

1. Remove the chroma-key background.
2. Detect or manually crop each frame.
3. Validate that every crop contains exactly one complete character pose.
4. Resize or fit each frame into the target cell size.
5. Export the final atlas as transparent PNG.

Recommended final output for small RPG characters:

```text
frame size: 48x64
grid: 4x4
atlas size: 192x256
format: transparent PNG
```
