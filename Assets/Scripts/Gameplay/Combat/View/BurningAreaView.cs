using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Monster.View;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>
    /// Five-second molotov area. Monsters use configured hit reports; the
    /// player uses the fixed 0.5 fire-damage request required by the design.
    /// </summary>
    public sealed class BurningAreaView : MonoBehaviour
    {
        private const float PlayerFireDamagePerTick = 0.5f;

        private AttackContext attack;
        private float radius;
        private float duration;
        private float tickInterval;
        private float elapsed;
        private float nextTickAt;
        private int hitSequence;
        private SpriteRenderer areaRenderer;

        public void Initialize(AttackContext attackContext, float areaRadius, float durationSeconds, float intervalSeconds)
        {
            attack = attackContext;
            radius = Mathf.Max(0.1f, areaRadius);
            duration = Mathf.Max(0.1f, durationSeconds);
            tickInterval = Mathf.Max(0.1f, intervalSeconds);
            nextTickAt = 0f;
            areaRenderer = CombatPlaceholderEffectView.CreateAreaRenderer(
                gameObject,
                radius,
                new Color(1f, 0.2f, 0.02f, 0.22f),
                10);
        }

        private void Update()
        {
            if (attack == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            while (nextTickAt < duration && elapsed >= nextTickAt)
            {
                ApplyDamageTick();
                hitSequence++;
                nextTickAt += tickInterval;
            }

            if (areaRenderer != null)
            {
                var color = areaRenderer.color;
                color.a = 0.16f + Mathf.PingPong(Time.time * 0.18f, 0.12f);
                areaRenderer.color = color;
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyDamageTick()
        {
            var center = (Vector2)transform.position;
            var reportedMonsters = new HashSet<string>();
            var playerInArea = false;
            var playerHitPoint = center;
            var colliders = Physics2D.OverlapCircleAll(center, radius);
            foreach (var areaCollider in colliders)
            {
                if (areaCollider == null)
                {
                    continue;
                }

                var monsterTarget = areaCollider.GetComponentInParent<MonsterTargetView>();
                if (monsterTarget != null && !string.IsNullOrEmpty(monsterTarget.TargetId) &&
                    reportedMonsters.Add(monsterTarget.TargetId))
                {
                    GameRoot.Instance?.Context?.Events.Publish(new CombatHitReportedEvent(
                        new CombatHitReport(
                            attack.AttackInstanceId,
                            monsterTarget.TargetId,
                            areaCollider.ClosestPoint(center),
                            hitSequence)));
                }

                if (!playerInArea && IsPlayerCollider(areaCollider))
                {
                    playerInArea = true;
                    playerHitPoint = areaCollider.ClosestPoint(center);
                }
            }

            if (playerInArea)
            {
                GameRoot.Instance?.Context?.Events.Publish(new DamageRequestedEvent(new DamageRequest(
                    attack.AttackInstanceId,
                    attack.AttackerId,
                    CombatTargetIds.Player,
                    attack.WeaponItemId,
                    DamageType.Fire,
                    PlayerFireDamagePerTick,
                    playerHitPoint)));
            }
        }

        private static bool IsPlayerCollider(Collider2D candidate)
        {
            var current = candidate != null ? candidate.transform : null;
            while (current != null)
            {
                if (current.CompareTag("Player"))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
