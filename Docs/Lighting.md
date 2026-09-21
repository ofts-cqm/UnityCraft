# Voxel lighting

## Design and decisions

Terrain combines scalar skylight and local light with Unity's directional sun and shadow maps.
Voxel access controls ambient and direct illumination because global sky probes and finite-distance
shadow maps cannot keep an editable, sealed cave dark. Unity still supplies the sun direction,
shadow rendering, procedural skybox, and the existing URP pipeline. `VoxelSky.shader` adapts
[Unity's MIT-licensed procedural sky sample](https://github.com/Unity-Technologies/ShaderGraph-MasterStack-Samples/blob/main/Assets/Shaders/Skybox-Procedural-Universal.shader)
with a sun-directed horizon glow. This keeps its atmospheric scattering and sun disk while
avoiding the greenish horizons produced by trying to author twilight through wavelength tint.
The original copyright and MIT license accompany the shader.

Both light channels range from 0 through 15. Clear vertical sky rays retain strength, including in
deep open shafts. Other propagation loses at least one level per block, so daylight ends within
15 steps of a sky-lit passage. Glass transmits; leaves absorb one level; water absorbs two. Slabs
and all stair states use the existing 2×2×2 occupancy and matching face openings. Empty portions
of the supported shapes are connected; new shapes with disconnected cavities will need separate
light values for each connected cavity rather than assuming a single value per voxel.

A small linear-space brightness floor (0.035), with face contrast, keeps unlit textures readable.
Smooth vertex samples exclude blocked half-cells and occluded corner samples. Direct sunlight is
gated by voxel skylight and the sun's elevation; there is no light from below the horizon. Water
reflections and the underwater overlay are also attenuated, while inventory sprite baking remains
independent of world lighting. Falling blocks sample the current field via a material override.

## Moving sun shadows

The gameplay sun inherits soft-shadow quality from the active URP asset. The PC preset requests
the high-quality 7×7 tent filter; a per-light low-quality override previously reduced this to
four comparison samples and made rotating shadow silhouettes visibly oscillate. Inheriting the
preset keeps quality ownership in one place and preserves Mobile's existing hard-shadow budget.

PC depth bias is 1 shadow-texel unit instead of 0.1; normal bias remains 0.5. The wider filter
samples neighboring depths on sloped receivers, so the old depth bias produced self-shadow
shimmer when high-quality filtering was enabled. Bias and filtering must be tuned together.
The 2048 atlas, four cascades, and 50-block distance stay unchanged. The sun and sky continue to
move every frame: pausing or stepping the daylight clock would hide aliasing by making shadows
stop or jump, and would change the intended cycle. Finite-resolution shadow maps can still show
small residual sampling variation; these settings address the pronounced edge flashing.
The terrain/water lighting helper skips shadow-map comparisons when the voxel sky channel,
surface orientation, sun elevation, or sun color prevents direct illumination. Such samples
cannot affect the result; avoiding them offsets part of the wider filter's GPU cost, especially
on sealed caves and back-facing surfaces.

## Ownership and update pipeline

`WorldLighting` owns one background worker. The main thread copies changed chunk buffers and
submits immutable snapshots. Copies are limited to two chunks per frame and share the render
budget with light uploads (0.25 ms default, within the existing 4 ms render budget); block data
is never scanned for propagation on the main thread.

The worker builds optical data, rebuilds a dirty region including its eight neighboring chunks,
then calculates vertex colors. The 16-block halo exceeds the maximum lateral propagation radius,
allowing reconstruction from sources to remove stale illumination after roofs close or sources
disappear. Only propagation-frontier cells enter the queue, and immutable optical arrays are
shared across light generations to limit allocations. Unloaded neighbors are closed boundaries.
World-top sky is the only implicit source.
Runtime sources and chunk changes are coalesced. Published buffers are immutable, and dependency
revisions reject outputs calculated against changed or unloaded chunks. Retired mesh bindings
cannot upload to replacement geometry. Initial loading waits for the first valid light upload.

Geometry rebuilds retain the last accepted smooth colors for matching position/normal pairs,
rather than clearing a whole section to black while its replacement light job runs. The worker
builds the vertex lookup once per geometry generation; the main thread only remaps those cached
colors. Newly exposed vertices use a single lookup in the last published field until their smooth
samples arrive. This intentionally allows the existing async lighting delay, but not a chunk-wide
black flash. Rapid successive edits inherit the accepted snapshot, never an unvalidated worker
result. Propagation, smoothing, and lookup construction remain on the worker.
Cache keys hash position/normal components separately to avoid grid-coordinate hash collisions;
newly streamed chunks without a published field skip provisional lookups entirely. Source revision
stamps defer to a pending block snapshot so a source update cannot validate unsupplied geometry.

Terrain geometry and light use separate vertex-buffer streams: stream 0 contains positions,
normals, UVs, and atlas/animation data; stream 1 contains four-byte colors (R=sky, G=local).
Light updates upload only stream 1 and never rebuild triangles or physics colliders. The worker
checks cancellation during solving and shuts down before world-owned data is released.

`World.Lighting.DiagnosticStatus` reports pending snapshots, first uploads, published columns,
and the last solve duration. Propagation can lag behind rapid edits while the worker catches up;
day/night transitions do not enqueue propagation or geometry work.

## Time and persistence

The default full cycle lasts 1,200 active-play seconds. Sunrise is phase 0, noon 0.25, sunset 0.5,
and midnight 0.75. New/legacy worlds start 60 seconds after sunrise. Time freezes during loading
and pause, and does not advance offline. Warm horizon transitions accompany the sun, and the
runtime skybox instance is restored on world exit so the menu material is never modified.

The descriptor's optional `daylight.elapsedSeconds` stores elapsed time through the serialized
save worker and existing atomic file replacement. Missing metadata means morning. Schema stays
at 2 because neither existing binary layout changes and legacy descriptors remain readable.
Existing schema-1 conversion stays supported. Light fields are derived and are not persisted.

## Future torches and lamps

For a block-integrated emitter, override `Block.LightEmission(ushort stateId)` with a value from
0–15. The override must be pure and thread-safe because the lighting worker evaluates it.
Opaque emitting blocks illuminate adjacent openings without transmitting light through
their solid volume. Existing block-state changes automatically invalidate lighting.

For a prefab, attach `VoxelLightSource` in the open cell containing the flame or lamp aperture.
It registers its scalar strength, updates when its cell/strength changes, and removes its source
when disabled. Code can instead use `RegisterSource`, `UpdateSource`, and `RemoveSource` on
`World.Lighting`. These facade APIs belong to the main thread. Overlapping sources take the
maximum contribution; they do not allocate Unity point lights or individual shadow maps.

## Validation

Run the Editor suite, including `VoxelLightingTests`, for propagation, shape transmission,
source removal, async unload/reload behavior, clock persistence, and independent GPU streams.

`VoxelLightingValidationBuild.Build` builds a macOS Development Player under `/private/tmp`
(override with `UNITYCRAFT_VALIDATION_BUILD`). It temporarily uses the product name
`UnityCraft Lighting Validation` so diagnostic saves do not share the normal game's save folder.
Launch it with `--voxel-lighting-validation <output-directory>` to create an isolated fixture,
capture rooms/caves/day phases, and record frame times for a camera orbit, mass roof edits,
and chunk streaming. It also exercises 81 concurrent scalar emitters.
The same harness runs against the original checkout, enabling a matched comparison.
Add `--remesh-flash-check` for a shorter regression run that alternates ten roof block edits
and checks unchanged lit vertices after every rendered frame. This catches transient cleared
light streams that would be invisible in screenshots taken only after lighting settles.

Use `--shadow-stability-check` for fixed-camera frozen-sun, low-sun, midmorning, and evening
sequences (90 frames each, at deterministic 1/60-second daylight increments). This fixture omits
the room so it cannot obscure the tower's shadow. `--shadow-baseline` restores the previous
low filter and 0.1 depth bias in the diagnostic Player, including on the normal performance route.
Compare capture directories with `python Docs/Validation/analyze_shadow_stability.py <before> <after>`
(requires Pillow). The analysis separates intended shadow motion from brightness reversals,
and the frozen control checks for unrelated temporal rendering noise. Capture PNG writes are
synchronous, so this mode is for image comparisons, not frame-time benchmarking.

Acceptance compares the PC preset at identical resolution/view distance: median frame time
within 5% and p99 within 10% of the baseline. A successful build or Editor test alone does not
establish visual or performance acceptance; keep Player measurements and screenshots with the
validation report. Hardware-specific observations and results are recorded separately.
