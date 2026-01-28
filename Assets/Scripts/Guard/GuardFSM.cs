using UnityEngine;
using UnityEngine.AI;

public class GuardFSM : MonoBehaviour
{
    public enum State { Patrol, Chase, Search }

    [Header("Patrol")]
    public Transform[] waypoints;
    public float patrolSpeed = 2.5f;
    public float waypointReachDist = 1.0f;

    [Header("Chase")]
    public float chaseSpeed = 5.0f;

    [Header("Search")]
    public float searchSpeed = 3.0f;
    public float searchWaitTime = 3.0f;

    [Header("Vision")]
    public float viewRadius = 12f;
    [Range(0, 180)] public float viewAngle = 90f;
    public LayerMask enemyMask;
    public LayerMask obstacleMask;

    NavMeshAgent agent;
    State state;

    int wpIndex;
    Transform target;
    Vector3 lastKnownPos;
    float searchTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        EnterPatrol();
    }

    void Update()
    {
        FindTarget();

        if (state == State.Patrol) PatrolUpdate();
        if (state == State.Chase) ChaseUpdate();
        if (state == State.Search) SearchUpdate();
    }

    void FindTarget()
    {
        if (target != null && CanSee(target)) return;

        target = null;

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
                target = t;
            }
        }

        if (target != null && state != State.Chase)
            EnterChase();
    }

    bool CanSee(Transform t)
    {
        Vector3 toTarget = (t.position - transform.position);
        float dist = toTarget.magnitude;
        Vector3 dir = toTarget.normalized;

        if (dist > viewRadius) return false;

        if (Vector3.Angle(transform.forward, dir) > viewAngle * 0.5f)
            return false;

        // Raycast to check if a wall is blocking
        if (Physics.Raycast(transform.position + Vector3.up * 0.8f, dir, dist, obstacleMask))
            return false;

        return true;
    }

    // ---------------- STATES ----------------

    void EnterPatrol()
    {
        state = State.Patrol;
        agent.speed = patrolSpeed;

        if (waypoints.Length > 0)
            agent.SetDestination(waypoints[wpIndex].position);
    }

    void PatrolUpdate()
    {
        if (waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= waypointReachDist)
        {
            wpIndex = (wpIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[wpIndex].position);
        }
    }

    void EnterChase()
    {
        state = State.Chase;
        agent.speed = chaseSpeed;
    }

    void ChaseUpdate()
    {
        if (target == null)
        {
            EnterSearch();
            return;
        }

        agent.SetDestination(target.position);
        lastKnownPos = target.position;

        if (!CanSee(target))
        {
            target = null;
            EnterSearch();
        }
    }

    void EnterSearch()
    {
        state = State.Search;
        agent.speed = searchSpeed;
        searchTimer = searchWaitTime;
        agent.SetDestination(lastKnownPos);
    }

    void SearchUpdate()
    {
        if (target != null)
        {
            EnterChase();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= waypointReachDist)
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
                EnterPatrol();
        }
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

        // Draw last known position (helpful for Search state)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lastKnownPos, 0.2f);
    }

    Vector3 DirFromAngle(float angle)
    {
        float rad = (transform.eulerAngles.y + angle) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

}
