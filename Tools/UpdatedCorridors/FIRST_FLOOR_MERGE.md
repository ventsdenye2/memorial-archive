# Front hall / first-floor corridor merge (2026-09-13)

Runtime scene: `Assets/Scenes/FrontHall.unity`.
The existing front hall occupies -57.6..57.6; the corridor is appended on the right,
shifted +67.2 units to 57.6..211.2. Height and 100 PPU stay unchanged.
One player, camera, UI/event system and darkness overlay serve both sections.
The old connecting exits and inner walls are inactive. A foreground column made
from a sprite rect in the existing source covers the wall-art seam; no new collider.

The corridor retains its interaction/spawn/light identities under
`SceneLayout0909/FirstFloorCorridor`. Its original `Floor_1F.unity` is preserved as
an authoring source and excluded from the build. Both room-return configs and the
SceneFlowManager alias resolve legacy Floor_1F destinations to FrontHall.
Old Floor_1F save positions gain +67.2 X; old FrontHall and new merged saves are
unchanged. SaveManager validates the resolved scene before restoring modules.

Reauthor using **Tools > Memorial Archive > Merge Front Hall and First Floor**.
Layered corridor and narrative builders also refresh the merged scene automatically.
After using the offline all-scene layout script, run the merge menu before playing
or building. Make corridor content edits in the preserved source scene.

Validation:
- SceneLayoutValidator: all 15 authoring scenes and destination IDs passed.
- `merged_qa.py`: one active player/camera, no joining exits or seam colliders;
  real PlayerMotor traversal across the seam both ways kept scene handle/player ID;
  old/new save positions and configured stair/toilet returns passed.
- `FirstFloorSceneLayoutTests`: 4/4 passed, covering migration-once and unchanged
  other scenes. Existing SavePersistenceTests also passed.
- Captures/results: `Logs/CorridorQA/merged-seam.png`, `merged-runtime.json`.
- Windows Development build succeeded: `Builds/Regression/Memorial Archive.exe`;
  completion marker `Logs/CorridorQA/build-merged.txt` is PASS.
