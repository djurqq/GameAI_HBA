using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class StrategistAI : MonoBehaviour
{
    public enum Goal { Normal, Survival }
    public enum ActionType { Heal, GetAmmo, Hide, Fight }

    [Header("Decision Timing")]
    public float decisionInterval = 0.7f;

    [Header("Critical Survival Override")]
    public float criticalHealthThreshold = 0.30f;      // if health% <= this -> Survival goal overrides
    public float criticalThreatDistance = 4f;          // if enemy close + lowish health -> survival
    public float threatenedHealthThreshold = 0.55f;

    [Header("Vision / Threat")]
    public float viewRadius = 14f;
    [Range(0, 180)] public float viewAngle = 120f;
    public LayerMask enemyMask;
    public LayerMask obstacleMask;

    [Header("Targets in Scene")]
    public Transform[] healthPacks;
    public Transform[] ammoCrates;
    public Transform[] coverPoints;

    [Header("Movement Speeds")]
    public float normalSpeed = 3.5f;
    public float panicSpeed = 5.5f;

    [Header("Combat (simple)")]
    public float attackRange = 10f;
    public float attackCooldown = 0.5f;
    public float attackDamage = 10f;

    NavMeshAgent agent;
    Health health;
    Ammo ammo;

    Goal currentGoal = Goal.Normal;
    ActionType currentAction = ActionType.GetAmmo;

    Transform currentEnemy;
    float nextDecisionTime;
    float nextAttackTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        ammo = GetComponent<Ammo>();
    }

    void Start()
    {
        agent.speed = normalSpeed;
        nextDecisionTime = Time.time + 0.2f;
    }

    void Update()
    {
        UpdateThreat();

        // 2.2 Hierarchical Goal Management (Survival override)
        currentGoal = EvaluateGoal();

        if (Time.time >= nextDecisionTime)
        {
            nextDecisionTime = Time.time + decisionInterval;

            // 2.1 Utility AI scoring happens here
            currentAction = ChooseActionByUtility(currentGoal);

            ExecuteAction(currentAction);
        }

        // If fighting, do simple “damage tick” when in range and LOS
        if (currentAction == ActionType.Fight)
            TryAttack();
    }

    // -------------------- GOAL OVERRIDE (2.2) --------------------
    Goal EvaluateGoal()
    {
        float hp = health != null ? health.Percent : 1f;

        bool enemyClose = currentEnemy != null &&
                          Vector3.Distance(transform.position, currentEnemy.position) <= criticalThreatDistance;

        if (hp <= criticalHealthThreshold) return Goal.Survival;
        if (enemyClose && hp <= threatenedHealthThreshold) return Goal.Survival;

        return Goal.Normal;
    }

    // -------------------- UTILITY SCORING (2.1) --------------------
    ActionType ChooseActionByUtility(Goal goal)
    {
        // Compute base scores
        float healScore = ScoreHeal();
        float ammoScore = ScoreAmmo();
        float hideScore = ScoreHide();
        float fightScore = ScoreFight();

        // In Survival goal, “Survival actions” dominate and fight is heavily discouraged
        if (goal == Goal.Survival)
        {
            fightScore *= 0.15f; // survival overrides normal aggression
            healScore *= 1.35f;
            hideScore *= 1.25f;
        }

        // Small randomness so it doesn’t do the exact same sequence every time
        healScore += Random.Range(-0.05f, 0.05f);
        ammoScore += Random.Range(-0.05f, 0.05f);
        hideScore += Random.Range(-0.05f, 0.05f);
        fightScore += Random.Range(-0.05f, 0.05f);

        // Weighted random pick among top actions (prevents identical behaviour loops)
        var list = new List<(ActionType act, float score)>
        {
            (ActionType.Heal, Mathf.Max(0f, healScore)),
            (ActionType.GetAmmo, Mathf.Max(0f, ammoScore)),
            (ActionType.Hide, Mathf.Max(0f, hideScore)),
            (ActionType.Fight, Mathf.Max(0f, fightScore))
        };

        // Keep top 3
        list.Sort((a, b) => b.score.CompareTo(a.score));
        int keep = Mathf.Min(3, list.Count);
        float total = 0f;
        for (int i = 0; i < keep; i++) total += list[i].score;

        if (total <= 0.001f)
            return ActionType.Hide; // safe fallback

        float r = Random.Range(0f, total);
        float accum = 0f;
        for (int i = 0; i < keep; i++)
        {
            accum += list[i].score;
            if (r <= accum) return list[i].act;
        }

        return list[0].act;
    }

    float ScoreHeal()
    {
        if (healthPacks == null || healthPacks.Length == 0 || health == null) return 0f;

        float need = 1f - health.Percent; // low health => high need
        float dist = DistanceToClosest(healthPacks);
        float distFactor = 1f / (1f + dist);

        // If enemy very close, healing is risky unless critically low -> handled by Survival goal
        float threatFactor = currentEnemy == null ? 1f : 0.75f;

        return (need * need) * distFactor * threatFactor;
    }

    float ScoreAmmo()
    {
        if (ammoCrates == null || ammoCrates.Length == 0 || ammo == null) return 0f;

        float need = 1f - ammo.Percent; // low ammo => high need
        float dist = DistanceToClosest(ammoCrates);
        float distFactor = 1f / (1f + dist);

        float threatFactor = currentEnemy == null ? 1f : 0.9f;

        return need * distFactor * threatFactor;
    }

    float ScoreHide()
    {
        if (coverPoints == null || coverPoints.Length == 0) return 0.2f;

        float threat = 0f;
        if (currentEnemy != null)
        {
            float d = Vector3.Distance(transform.position, currentEnemy.position);
            threat = Mathf.Clamp01(1f - (d / viewRadius)); // closer enemy => more hide
        }

        float hpLow = health != null ? (1f - health.Percent) : 0f;

        return 0.2f + threat * 0.5f + hpLow * 0.7f;
    }

    float ScoreFight()
    {
        if (currentEnemy == null || ammo == null || health == null) return 0f;

        float d = Vector3.Distance(transform.position, currentEnemy.position);
        float distFactor = Mathf.Clamp01(1f - (d / viewRadius)); // closer => better

        float confidence = health.Percent * 0.7f + ammo.Percent * 0.6f;

        // If no LOS, fighting is bad
        bool los = HasLineOfSight(currentEnemy);
        float losFactor = los ? 1f : 0.2f;

        return confidence * distFactor * losFactor;
    }

    // -------------------- EXECUTION --------------------
    void ExecuteAction(ActionType act)
    {
        if (act == ActionType.Heal)
        {
            agent.speed = normalSpeed;
            Transform t = ClosestTransform(healthPacks);
            if (t != null) agent.SetDestination(t.position);
        }
        else if (act == ActionType.GetAmmo)
        {
            agent.speed = normalSpeed;
            Transform t = ClosestTransform(ammoCrates);
            if (t != null) agent.SetDestination(t.position);
        }
        else if (act == ActionType.Hide)
        {
            agent.speed = (currentGoal == Goal.Survival) ? panicSpeed : normalSpeed;
            Transform c = BestCoverPoint();
            if (c != null) agent.SetDestination(c.position);
        }
        else if (act == ActionType.Fight)
        {
            agent.speed = normalSpeed;

            if (currentEnemy != null)
            {
                // Move toward enemy but don’t stand on top of them
                Vector3 dir = (transform.position - currentEnemy.position).normalized;
                Vector3 standOff = currentEnemy.position + dir * 3f;
                agent.SetDestination(standOff);
            }
        }
    }

    void TryAttack()
    {
        if (currentEnemy == null) return;
        if (Time.time < nextAttackTime) return;

        float d = Vector3.Distance(transform.position, currentEnemy.position);
        if (d > attackRange) return;

        if (!HasLineOfSight(currentEnemy)) return;
        if (!ammo.Use(1)) return;

        nextAttackTime = Time.time + attackCooldown;

        Health enemyHealth = currentEnemy.GetComponent<Health>();
        if (enemyHealth != null)
            enemyHealth.TakeDamage(attackDamage);
    }

    // -------------------- THREAT / LOS --------------------
    void UpdateThreat()
    {
        // Find nearest visible enemy
        currentEnemy = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, enemyMask);
        float bestDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            Transform t = hit.transform;
            if (!CanSee(t)) continue;

            float d = Vector3.Distance(transform.position, t.position);
            if (d < bestDist)
            {
                bestDist = d;
                currentEnemy = t;
            }
        }
    }

    bool CanSee(Transform t)
    {
        Vector3 toTarget = t.position - transform.position;
        float dist = toTarget.magnitude;
        if (dist > viewRadius) return false;

        Vector3 dir = toTarget.normalized;
        if (Vector3.Angle(transform.forward, dir) > viewAngle * 0.5f) return false;

        return HasLineOfSight(t);
    }

    bool HasLineOfSight(Transform t)
    {
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 target = t.position + Vector3.up * 0.8f;
        Vector3 dir = (target - origin).normalized;
        float dist = Vector3.Distance(origin, target);

        if (Physics.Raycast(origin, dir, dist, obstacleMask)) return false;
        return true;
    }

    // -------------------- HELPERS --------------------
    float DistanceToClosest(Transform[] arr)
    {
        Transform t = ClosestTransform(arr);
        if (t == null) return 999f;
        return Vector3.Distance(transform.position, t.position);
    }

    Transform ClosestTransform(Transform[] arr)
    {
        if (arr == null) return null;

        Transform best = null;
        float bestDist = Mathf.Infinity;

        foreach (var t in arr)
        {
            if (t == null) continue;
            float d = Vector3.Distance(transform.position, t.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }
        return best;
    }

    Transform BestCoverPoint()
    {
        if (coverPoints == null || coverPoints.Length == 0) return null;

        Transform best = null;
        float bestScore = -999f;

        foreach (var c in coverPoints)
        {
            if (c == null) continue;

            float dist = Vector3.Distance(transform.position, c.position);
            float distScore = 1f / (1f + dist);

            // Cover is better if enemy cannot see this point
            float coverScore = 0.5f;
            if (currentEnemy != null)
            {
                // If ray from enemy to cover hits an obstacle, it is “good cover”
                Vector3 origin = currentEnemy.position + Vector3.up * 0.8f;
                Vector3 target = c.position + Vector3.up * 0.8f;
                Vector3 dir = (target - origin).normalized;
                float d = Vector3.Distance(origin, target);

                bool blocked = Physics.Raycast(origin, dir, d, obstacleMask);
                coverScore = blocked ? 1.2f : 0.2f;
            }

            float score = distScore + coverScore;

            // random tiny variation so it doesn’t pick the exact same cover every time
            score += Random.Range(-0.03f, 0.03f);

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    // --------- VISUALISE VIEW CONE (Scene view only) ---------
    void OnDrawGizmosSelected()
    {
        // Only draw if we're in editor and have sensible values
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // Draw the FOV cone edges
        Vector3 leftDir = DirFromAngle(-viewAngle * 0.5f);
        Vector3 rightDir = DirFromAngle(viewAngle * 0.5f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * viewRadius);

        // Optional: draw "cone" lines (makes it look like a real cone)
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);

        int steps = 20;
        float stepAngle = viewAngle / steps;
        for (int i = 0; i <= steps; i++)
        {
            float a = -viewAngle * 0.5f + stepAngle * i;
            Vector3 dir = DirFromAngle(a);
            Gizmos.DrawLine(transform.position, transform.position + dir * viewRadius);
        }
    }

    Vector3 DirFromAngle(float angle)
    {
        float rad = (transform.eulerAngles.y + angle) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

}
