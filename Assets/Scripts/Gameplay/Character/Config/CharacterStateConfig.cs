using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Config
{
    public enum CharacterStateType
    {
        Normal,
        Injured,
        BrokenLeg,
        Dying
    }

    [CreateAssetMenu(menuName = "Memorial Archive/Config/Character State")]
    public sealed class CharacterStateConfig : ScriptableObject
    {
        [SerializeField] private int stateId;
        [SerializeField] private CharacterStateType stateType = CharacterStateType.Normal;
        [SerializeField] private string displayName;
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private bool canRun = true;
        [SerializeField] private bool canDodge = true;

        public int StateId => stateId;
        public CharacterStateType StateType => stateType;
        public string DisplayName => displayName;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public bool CanRun => canRun;
        public bool CanDodge => canDodge;
    }
}
