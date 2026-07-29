using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Monster.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.View
{
    /// <summary>
    /// 无 AI 的可受击怪物目标。近战和投掷物只从 Collider2D 找到本组件，
    /// 再把 TargetId 随 CombatHitReportedEvent 上报；不允许直接修改生命。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class MonsterTargetView : MonoBehaviour
    {
        [SerializeField] private int monsterId = 2001;
        [SerializeField] private int initialHealth = 300;

        private string targetId;

        public string TargetId => targetId;
        public int MonsterId => monsterId;

        private void Awake()
        {
            // 在美术敌人资源到位前，使用醒目的运行时占位形象，确保碰撞和受击流程可测试。
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null && GetComponentInChildren<Renderer>(true) != null)
            {
                return;
            }

            spriteRenderer = spriteRenderer != null ? spriteRenderer : gameObject.AddComponent<SpriteRenderer>();
            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
                spriteRenderer.color = new Color(0.7f, 0.12f, 0.16f, 1f);
                transform.localScale = new Vector3(1.2f, 1.8f, 1f);
            }
        }

        private void OnEnable()
        {
            GameRoot.Instance?.Context?.Events.Subscribe<MonsterDiedEvent>(HandleMonsterDied);
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<MonsterDiedEvent>(HandleMonsterDied);
        }

        private void Start()
        {
            // 允许把静态目标直接拖入测试场景；由刷新点生成时会在 Start 前设置正式实例 Id。
            if (string.IsNullOrEmpty(targetId))
            {
                SetRuntimeIdentity($"static_{GetInstanceID()}", monsterId, initialHealth);
            }
        }

        /// <summary>只由 MonsterSpawnPointView 在生成实例后调用。</summary>
        public void SetRuntimeIdentity(string instanceId, int configuredMonsterId, int configuredInitialHealth)
        {
            targetId = instanceId;
            monsterId = configuredMonsterId;
            initialHealth = Mathf.Max(1, configuredInitialHealth);
            foreach (var targetCollider in GetComponents<Collider2D>())
            {
                targetCollider.enabled = true;
            }

            GameRoot.Instance?.GetSystem<MonsterSystem>()?.RegisterSpawnedMonster(
                targetId, monsterId, initialHealth, transform.position);
            GetComponentInParent<MonsterAIView>()?.InitializeRuntime(this);
        }

        private void HandleMonsterDied(MonsterDiedEvent evt)
        {
            if (!string.IsNullOrEmpty(targetId) && evt.MonsterInstanceId == targetId)
            {
                foreach (var targetCollider in GetComponents<Collider2D>())
                {
                    targetCollider.enabled = false;
                }

                if (GetComponentInParent<MonsterAIView>() == null)
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
