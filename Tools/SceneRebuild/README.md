# Gameplay scene layout

`layout.json` is the reviewed placement manifest. Pixel coordinates use each scene's top-left origin, 100 pixels per Unity unit. Backgrounds retain their native width: 1920 assets stay 1920; native 3840 rooms stay 3840; FrontHall is 11520. Corridors concatenate original 1920 segments. Camera view is 1920×1080.

To apply a deliberate manifest revision, save and close affected scenes and exit Play Mode. Run from the project root with Python, PyYAML and Pillow:

```powershell
python -X utf8 Tools/SceneRebuild/apply_layout.py
python -X utf8 Tools/SceneRebuild/finalize_layout.py
```

Wait for the first command to finish before running the second. Refresh Unity, then run **Tools → Memorial Archive → Validate Scene Layout**. The second command registers access rules and the terrace map marker and replaces superseded config registrations by domain ID.

The generated hierarchy is `SceneLayout0909`. Reapplying replaces this hierarchy, so record placement edits in the manifest first. Previous scene art/interaction/lighting roots are retained inactive for comparison. Existing player/UI prefabs and established transition IDs remain in use.

`referencePlate` records the artist's positioning image; `backgrounds` contains clean layers where available to avoid duplicate notes/phones. `points`, `lights`, `monsters`, and `moves` describe interactive props, lighting, spawn points, and existing transition positions. Office puzzle contents are intentionally empty at the author's request. Old Archive and Confinement are outside this revision.

The editor validator writes `Temp/SceneRebuildAudit/unity_scene_audit.json`. It checks missing scripts and assets, unique interaction IDs, registered configs, camera bounds, point/spawn bounds, monster prefab references, native background scale, triggers covering walking height, and every door/stair destination spawn ID. It does not replace Play Mode or visual acceptance.

## Interaction spacing

`interaction_layout.json` is the final interaction placement pass, including narrative points, the merged first-floor copy, and the two runtime guide props. It uses **world coordinates and full trigger widths**, not lighting radii. Base prop positions are also synchronized into `layout.json`; this final pass supplies the ranges and points authored by other builders.

After rebuilding scene or narrative content, or merging the first floor, run **Tools → Memorial Archive → Apply Interaction Layout** last. It updates the existing objects without rebuilding scene hierarchies, and synchronizes the guide's obstacle, lamp and diary anchors. Save any open scene edits before applying.

Run **Validate Interaction Layout** to check every active interaction point against the manifest, actual trigger bounds, walking-height reachability, camera bounds, and at least 0.25 units between neighboring interaction ranges. The report is `Temp/InteractionAudit/after.json`. Ordinary lights use a 0.5-unit horizontal half-width, props generally 0.6, doors 0.7–0.8 and stairs 0.9; box triggers span -5.8 to -3.4; existing circle triggers are centered at walking height (-5.2), so decorative prop height does not affect reachability. Inactive historical roots are excluded. The unused SampleScene is included as a regression precaution.
