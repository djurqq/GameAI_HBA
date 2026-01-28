using UnityEngine;

public class GuardMeleeAttack : MonoBehaviour
{
    [Header("Melee Settings")]
    public float attackRange = 1.6f;
    public float damage = 10f;
    public float attackCooldown = 1f;

    [Header("Who can be hit")]
    public LayerMask hitMask;

    float nextAttackTime;

    void Update()
    {
        if (Time.time < nextAttackTime) return;

        // Find any enemy collider in range
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, hitMask);

        if (hits.Length == 0) return;

        // Deal damage to the first valid target with Health
        foreach (var h in hits)
        {
            Health hp = h.GetComponentInParent<Health>();
            if (hp == null) continue;

            hp.TakeDamage(damage);
            nextAttackTime = Time.time + attackCooldown;
            break;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
