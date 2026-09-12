# Updated corridor authoring

The first-floor corridor now runs inside FrontHall; see
`FIRST_FLOOR_MERGE.md` for the merged scene, authoring source and old-save mapping.

The reviewed `layout.json` describes 1F–3F at 15360×1080, 100 pixels/unit,
left -9.6, right 144. Source coordinates are native top-left pixel offsets.
Background images are reference-only; runtime art uses separate Source sprites.
4F has no supplied replacement pack and is excluded.

Columns (`组 6`) use sorting order 40, above character renderers (0) and
below the darkness overlay (50). 3F includes its supplied independent column
layer even though its flattened reference omits it. They have no colliders.
4F's existing art has no corresponding independent column layer.

Unity menu: **Tools > Memorial Archive > Rebuild Layered Corridors**.
The old **Import Updated Corridor Assets** entry delegates to this builder.
Save any unrelated scene edits before running scene validation.

The builder updates existing transition/spawn identities, the continuous geometry,
camera bounds and light registrations, and preserves the far-left terrace route.
It is designed to be reapplied without accumulating generated layers or lights.

**Validate Layered Corridors** checks source references, native scale, anchors,
camera references and the walking lane, and captures full-width Unity renders to
`Logs/CorridorQA`. Also run **Validate Scene Layout** for cross-scene routes and
configuration coverage. Finally test movement and room returns in Play Mode.

`analyze.py`, `match_layers.py`, `build_plan.py` produce analysis/previews.
`sync_legacy_plan.py` synchronizes the older all-scene manifest so its offline
authoring tool cannot restore the old corridor backgrounds. The offline tool is
not safe to run while Unity has those scenes open.

Validation on 2026-09-13: Unity compilation and scene application succeeded.
All three source-layer/bounds/walking-lane audits passed; the 15-scene route/config
audit returned no errors. Actual Unity panoramas are `Floor_1F-unity.png` through
`Floor_3F-unity.png` under `Logs/CorridorQA`.

`runtime_qa.py` requires Play Mode and runs actual SceneFlowManager transitions,
checks room-return/terrace spawn positions, and checks the far-right camera on
each floor. It temporarily enables background game updates for unattended QA;
it does not save player progress or change the project's background-run setting.
Results are written to `Logs/CorridorQA/runtime.json`.

`occlusion_qa.py` renders the player at a column on each floor, once with the
column enabled and once disabled. It restores the original scene without saving
the temporary player/camera changes. All three floors passed the order inspection;
the paired captures show the character hidden by the opaque column pixels.

Windows Development build including the foreground columns succeeded on
2026-09-13: `Builds/Regression/Memorial Archive.exe`.
Completion marker: `Logs/CorridorQA/build-columns.txt` (`PASS`).
