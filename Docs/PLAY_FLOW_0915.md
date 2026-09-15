# Demo Play Flow

Confirmed against the user's flowchart and subsequent answers on 2026-09-15.

1. Complete the opening, movement, lighting and UI tutorials, then enter Floor_2F.
2. Visit Room_ArchiveA, Room_ArchiveB and Room_Reception, complete Cecil's conversation, and attempt to enter the locked Room_Office without key 1024.
3. Returning to Floor_2F after these objectives queues the upstairs suggestion once. Before completion every upstairs attempt displays the blocking narrative again. Going downstairs remains available.
4. Explore Floor_3F. Archive C has axe 1003 and no monster. Treatment B, the director's office and Floor_4F are unavailable in this demo.
5. Take key 1024 from Treatment A. Leaving without the key does not spawn the encounter. Leaving with the key spawns the standard melee monster and runs the dodge/equipment/combat tutorial. Travel from Floor_3F is blocked during this encounter.
6. Defeat the encounter and return to Floor_2F. Key 1024 opens Room_Office.
7. Inside Room_Office there is one standard melee and one standard ranged monster. Read both office documents and inspect the existing locked cabinet as the safe. The safe displays an unable-to-open message; no puzzle or reward is implemented.
8. After all preceding objectives and both office kills, leaving Room_Office for Floor_2F displays "目前流程已结束，感谢您的游玩" once.

The guide_flow save module persists the locked-door check, safe inspection, encounter phase and office monster health. Narrative saves persist visits, document reading and one-shot dialogue completion. Blocking messages and safe inspection are repeatable. Save restoration bypasses scene travel gates to preserve load compatibility; use a new game for acceptance of the complete route.

GuideFlowSystem owns gates and final objectives; SceneFlowManager enforces gates at travel requests. GuideWorldView spawns office monsters from the standard prefabs and restores their remaining health. NarrativeSystem only presents and saves narrative state.

Darkness-only death has been disabled in both lighting and opening-guide paths. Combat death remains enabled.

Validation: Unity Editor tests completed on 2026-09-15 at 21:56:59 (local time), 18 passed and 0 failed. Includes PlayMode checks of the upstairs hint, Treatment A key/encounter gate, both office monster spawns, office completion and no replay after save restoration. See Logs/narrative-tests.txt. No player build was produced.
