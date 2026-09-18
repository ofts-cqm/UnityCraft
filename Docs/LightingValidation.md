# Lighting validation — 2026-09-18

## Environment and comparison

Validated with Unity 6000.5.4f1 / URP 17.5 on this Mac's Apple M4 GPU, using macOS Development
Players, the PC preset, 1280×720, view distance 4, VSync disabled, and uncapped frame rate.
The baseline is commit `e552592` with only the same opt-in diagnostic harness added. Both Players
use isolated diagnostic saves and the same seed, fixture, camera route, and edit sequence.
Performance runs were sequential, with no concurrent Unity builds or tests.

The fixtures were chosen to expose light leakage and update cost directly: an opaque enclosed
room containing water, a deep cave connected to a sky-open shaft, a tower for sun shadows,
1,024-block roof insertion/removal, and travel across eight chunk boundaries into generated terrain.

## Frame-time results

Times are milliseconds; lower is better. Acceptance is median within 5% and p99 within 10% of
baseline. These are measured comparisons, not a guarantee for every world or device.

| Scenario | Baseline median | New median | Baseline p99 | New p99 | p99 change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Camera orbit, 15 seconds | 1.114 | 1.097 | 2.159 | 2.314 | +7.2% |
| Bulk roof edits, 16 seconds | 1.111 | 1.097 | 2.154 | 2.216 | +2.9% |
| Chunk streaming, 20 seconds | 1.204 | 1.174 | 5.393 | 5.694 | +5.6% |

All three scenarios meet the chosen median/p99 limits. Streaming still has occasional long
frames in both versions (baseline maximum 35.910 ms; new maximum 33.197 ms). The orbit also
contained a new-run outlier of 16.916 ms versus baseline 3.237 ms, despite meeting the p99 limit;
this run does not establish the cause of that isolated outlier. Bulk-edit maxima were 4.203 ms
baseline and 5.285 ms new. The design moves light solving/sampling off the main thread and
budgets uploads; it does not claim to eliminate existing geometry, physics, OS, or GC stalls.

## Visual and functional results

- Final Editor regression suite: **92 passed, 0 failed**.
- Inspected built-Player screenshots of the sealed room and deep cave: textures remain readable
  at the darkness floor, without daytime sky illumination leaking inside.
- The enclosed room's water also remains dark. Open vertical shafts retain skylight.
- Morning and evening screenshots show opposite, correct sun-shadow directions; the nighttime
  scene is dark with no below-world sun illumination.
- Sunrise produces an east-facing gold glow; sunset produces a west-facing orange glow.
- A scalar cave source brightens the passage; removing it restores darkness. The runtime harness
  also registers/removes 81 simultaneous sources after chunk streaming without worker errors.
- Editor tests cover source overlap/removal, partial-block transmission, unloaded boundaries,
  cancellation, worker source updates and unload/reload, clock metadata, and independent vertex
  streams including their geometry, bounds, and atlas data.

The Player checks caught and corrected zero mesh bounds in the explicit stream layout. The
bulk-update benchmark motivated frontier-only propagation queues, shared immutable optical
buffers, and a 0.25 ms upload/copy allowance within the existing render budget. Material regression
checks preserve glass blending and keep its low-alpha pixels from being cut out.

## Reproduction and artifacts

See [Lighting.md](Lighting.md) for build/run instructions and architectural decisions.
The checked-in harness is disabled unless explicitly invoked, and is excluded from non-development
Players. It creates diagnostic worlds under the validation product's separate save directory.

Raw screenshots and frame samples from this run are in `/private/tmp/unitycraft-lighting-visual/`;
baseline artifacts are in `/private/tmp/unitycraft-lighting-baseline-results/`. Editor test results
are in `/private/tmp/unitycraft-lighting-final-tests.xml`, with build and Player logs alongside them.
These temporary artifacts may be cleared by the operating system; this report retains the measured
results. Actual mobile hardware, other resolutions/view distances, and long-duration soak testing
were not covered by this validation.

## Block-edit dark-flash regression

Investigation found that `WorldLighting.Bind` filled the new mesh's light stream with zeroes
on every rebuild, leaving the entire section dark until its background job completed. Rebuilds
now remap the last accepted colors by vertex position and normal. New vertices sample the last
published field provisionally, while the worker still performs propagation and smooth sampling.
This preserves immediate geometry edits and async work without blanking unchanged faces.

Validation used an isolated project copy because the working project was open in Unity:

- **94 Editor tests passed, 0 failed**, including both vertex-normal upload modes, reordered and
  removed/inserted faces with changing vertex counts, two edits without an intervening light upload,
  and eventual source removal. A forced source-before-snapshot ordering also verifies that an
  incomplete worker result cannot be accepted as the first valid upload.
- A macOS Development Player alternated **10 roof block edits** and checked **289 rendered frames**
  against nine unchanged lit vertex probes. Every probe retained its expected color. The harness
  verified that all ten edits actually rebuilt the mesh; break/place screenshots were also inspected.
- Test results: `/private/tmp/unitycraft-lighting-remesh-final-tests.xml`.
- Frame-check log: `/private/tmp/unitycraft-lighting-remesh-final-player.log`; screenshots:
  `/private/tmp/unitycraft-lighting-remesh-final-visual/`.

Use `--voxel-lighting-validation <directory> --remesh-flash-check` to repeat this targeted check.

The first cache implementation exceeded the streaming target: its repeat run measured a
1.429 ms median / 7.002 ms p99 (the first trial was slower still, at 1.593 / 8.118 ms).
This motivated component-wise vertex-key hashing and skipping guaranteed-missing provisional
lookups for newly streamed chunks. The final build and a freshly rerun original baseline measured:

| Scenario | Baseline median | Fixed median | Baseline p99 | Fixed p99 |
| --- | ---: | ---: | ---: | ---: |
| Camera orbit | 1.110 | 1.135 | 2.146 | 2.315 |
| Bulk roof edits | 1.120 | 1.120 | 2.156 | 2.270 |
| Chunk streaming | 1.212 | 1.193 | 5.602 | 5.905 |

All final scenarios meet the same 5% median / 10% p99 limits. Streaming maxima remain occasional
outliers: 29.562 ms baseline and 32.102 ms fixed. These are uncapped single-machine measurements,
not a guarantee against OS/GC/geometry stalls. Both final comparison runs left the user's editor
open, with no concurrent validation build or test process. Final raw frame data is under
`/private/tmp/unitycraft-lighting-remesh-final-performance/`; the fresh baseline is under
`/private/tmp/unitycraft-lighting-remesh-baseline/`.
