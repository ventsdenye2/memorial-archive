using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Monster.View;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>Runtime projectile presentation for grenades and molotovs.</summary>
    public sealed class ThrowableProjectileView : MonoBehaviour
    {
        private const int GrenadeItemId = 1010;
        private const int MolotovItemId = 1011;
        private const float GrenadeFuseSeconds = 3f;
        private const float GrenadeRadius = 3f;
        private const float MolotovRadius = 2.5f;
        private const float MolotovBurnSeconds = 5f;
        private const float MolotovMaxFlightSeconds = 6f;

        private AttackContext attack;
        private Rigidbody2D body;
        private Collider2D projectileCollider;
        private PhysicsMaterial2D runtimeMaterial;
        private float elapsed;
        private bool resolved;

        public void Initialize(
            AttackContext attackContext,
            Rigidbody2D projectileBody,
            Collider2D ownCollider,
            Collider2D[] ownerColliders,
            PhysicsMaterial2D material)
        {
            attack = attackContext;
            body = projectileBody;
            projectileCollider = ownCollider;
            runtimeMaterial = material;
            if (projectileCollider != null && ownerColliders != null)
            {
                foreach (var ownerCollider in ownerColliders)
                {
                    if (ownerCollider != null && ownerCollider != projectileCollider)
                    {
                        Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
                    }
                }
            }
        }

        private void Update()
        {
            if (resolved || attack == null)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (attack.WeaponItemId == GrenadeItemId && elapsed >= GrenadeFuseSeconds)
            {
                ExplodeGrenade();
            }
            else if (attack.WeaponItemId == MolotovItemId && elapsed >= MolotovMaxFlightSeconds)
            {
                ShatterMolotov(transform.position);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (resolved || attack == null || attack.WeaponItemId != MolotovItemId || elapsed < 0.08f)
            {
                return;
            }

            ShatterMolotov(collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position);
        }

        private void ExplodeGrenade()
        {
            resolved = true;
            var center = (Vector2)transform.position;
            GameRoot.Instance?.Context?.Events.Publish(new GrenadeExplodedEvent(center));
            ReportGrenadeTargets(center);
            if (!SpineEffectPlayer.TryPlayAt(
                    SpineEffectPlayer.GrenadeExplosionResource,
                    "idle",
                    center,
                    0f,
                    1.4f,
                    55))
            {
                CombatPlaceholderEffectView.CreatePulse(
                    center,
                    GrenadeRadius,
                    new Color(1f, 0.58f, 0.12f, 0.7f),
                    0.45f);
            }
            Destroy(gameObject);
        }

        private void ReportGrenadeTargets(Vector2 center)
        {
            var reportedTargets = new HashSet<string>();
            var colliders = Physics2D.OverlapCircleAll(center, GrenadeRadius);
            foreach (var areaCollider in colliders)
            {
                if (areaCollider == null)
                {
                    continue;
                }

                var monsterTarget = areaCollider.GetComponentInParent<MonsterTargetView>();
                if (monsterTarget != null && !string.IsNullOrEmpty(monsterTarget.TargetId) &&
                    reportedTargets.Add(monsterTarget.TargetId))
                {
                    PublishConfiguredHit(monsterTarget.TargetId, areaCollider.ClosestPoint(center), 0);
                }

                if (IsPlayerCollider(areaCollider) && reportedTargets.Add(CombatTargetIds.Player))
                {
                    PublishConfiguredHit(CombatTargetIds.Player, areaCollider.ClosestPoint(center), 0);
                }
            }
        }

        private void ShatterMolotov(Vector2 impactPoint)
        {
            resolved = true;
            var fireObject = new GameObject($"BurningArea_{attack.AttackInstanceId}");
            fireObject.transform.position = impactPoint;
            fireObject.AddComponent<BurningAreaView>().Initialize(
                attack,
                MolotovRadius,
                MolotovBurnSeconds,
                1f);
            CombatPlaceholderEffectView.CreatePulse(
                impactPoint,
                MolotovRadius * 0.65f,
                new Color(1f, 0.22f, 0.04f, 0.65f),
                0.3f);
            Destroy(gameObject);
        }

        private void PublishConfiguredHit(string targetId, Vector2 hitPoint, int sequence)
        {
            GameRoot.Instance?.Context?.Events.Publish(new CombatHitReportedEvent(
                new CombatHitReport(attack.AttackInstanceId, targetId, hitPoint, sequence)));
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

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
