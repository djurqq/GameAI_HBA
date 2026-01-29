using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class GladiatorAgentMelee : Agent
{
    [Header("Movement")]
    public float moveSpeed = 4.0f;
    public float turnSpeed = 180f;

    [Header("Melee")]
    public float attackRange = 1.6f;
    public float attackDamage = 12f;
    public float attackCooldown = 0.6f;

    [Header("Sensing / LOS")]
    public float maxSenseDistance = 14f;
    public LayerMask obstacleMask;

    [HideInInspector] public GladiatorAgentMelee opponent; // set by ArenaManager

    Rigidbody rb;
    Health health;

    float nextAttackTime;
    float lastHealth;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        lastHealth = health.currentHealth;

        // Useful rigidbody settings for stable training
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin()
    {
        nextAttackTime = 0f;
        lastHealth = health.currentHealth;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Self
        sensor.AddObservation(health.Percent);                 // 0..1
        sensor.AddObservation(rb.linearVelocity.x / moveSpeed);
        sensor.AddObservation(rb.linearVelocity.z / moveSpeed);

        if (opponent == null)
        {
            // pad observations if opponent missing
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        Vector3 toOpp = opponent.transform.position - transform.position;
        float dist = toOpp.magnitude;

        // Direction to opponent in LOCAL space (better learning)
        Vector3 localDir = transform.InverseTransformDirection(toOpp.normalized);
        sensor.AddObservation(new Vector3(localDir.x, 0f, localDir.z)); // 3 values

        // Normalized distance
        sensor.AddObservation(Mathf.Clamp01(dist / maxSenseDistance));

        // Can see opponent (LOS)
        sensor.AddObservation(HasLineOfSight(opponent.transform) ? 1f : 0f);

        // Opponent health (gives “finish them” behaviour)
        sensor.AddObservation(opponent.health.Percent);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Actions (continuous):
        // 0 = moveX, 1 = moveZ, 2 = turn, 3 = attack
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        float atk = Mathf.Clamp(actions.ContinuousActions[3], 0f, 1f);

        // Move (world relative based on agent forward/right)
        Vector3 move = (transform.right * moveX + transform.forward * moveZ) * moveSpeed;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

        // Turn
        transform.Rotate(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);

        // Small living reward (survival)
        AddReward(0.0005f);

        // Small time penalty (prevents camping)
        AddReward(-0.0002f);

        // Damage taken penalty (computed by health drop)
        float hpNow = health.currentHealth;
        float taken = Mathf.Max(0f, lastHealth - hpNow);
        if (taken > 0f) AddReward(-0.01f * taken);
        lastHealth = hpNow;

        // Attack
        if (atk > 0.5f)
            TryMeleeAttack();

        // Lose condition
        if (health.IsDead)
        {
            AddReward(-1.0f);
            EndEpisode();
        }
    }

    void TryMeleeAttack()
    {
        if (Time.time < nextAttackTime) return;
        if (opponent == null) return;

        float dist = Vector3.Distance(transform.position, opponent.transform.position);
        if (dist > attackRange) return;

        // Optional LOS requirement to stop “through wall” hits
        if (!HasLineOfSight(opponent.transform)) return;

        nextAttackTime = Time.time + attackCooldown;

        // Deal damage (we control this so we can reward “damage dealt”)
        opponent.health.TakeDamage(attackDamage);
        AddReward(0.01f * attackDamage);

        // If opponent died, win
        if (opponent.health.IsDead)
        {
            AddReward(+1.0f);
            EndEpisode();
        }
    }

    bool HasLineOfSight(Transform t)
    {
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 target = t.position + Vector3.up * 0.8f;
        Vector3 dir = (target - origin).normalized;
        float dist = Vector3.Distance(origin, target);

        // If a wall is between us, no LOS
        if (Physics.Raycast(origin, dir, dist, obstacleMask))
            return false;

        return true;
    }

    // Optional: manual control for testing
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        a[0] = Input.GetAxis("Horizontal");
        a[1] = Input.GetAxis("Vertical");
        a[2] = Input.GetKey(KeyCode.Q) ? -1f : Input.GetKey(KeyCode.E) ? 1f : 0f;
        a[3] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }
}
