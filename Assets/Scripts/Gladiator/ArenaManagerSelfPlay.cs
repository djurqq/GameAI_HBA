using UnityEngine;

public class ArenaManagerSelfPlay : MonoBehaviour
{
    public GladiatorAgentMelee gladiatorA;
    public GladiatorAgentMelee gladiatorB;

    public Transform[] spawnPoints; // size 2+ recommended

    void Start()
    {
        if (gladiatorA != null && gladiatorB != null)
        {
            gladiatorA.opponent = gladiatorB;
            gladiatorB.opponent = gladiatorA;
        }

        ResetBoth();
    }

    void Update()
    {
        // If either died, reset both so training stays stable
        if (gladiatorA != null && gladiatorB != null)
        {
            if (gladiatorA.GetComponent<Health>().IsDead || gladiatorB.GetComponent<Health>().IsDead)
                ResetBoth();
        }
    }

    public void ResetBoth()
    {
        ResetAgent(gladiatorA, 0);
        ResetAgent(gladiatorB, 1);
    }

    void ResetAgent(GladiatorAgentMelee agent, int spawnIndex)
    {
        if (agent == null) return;

        var rb = agent.GetComponent<Rigidbody>();
        var hp = agent.GetComponent<Health>();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform sp = spawnPoints[Mathf.Clamp(spawnIndex, 0, spawnPoints.Length - 1)];
            agent.transform.position = sp.position;
            agent.transform.rotation = sp.rotation;
        }
        else
        {
            // fallback
            agent.transform.position = new Vector3(spawnIndex * 4f, 0.5f, 0f);
            agent.transform.rotation = Quaternion.identity;
        }

        rb.linearVelocity = Vector3.zero;  // or rb.velocity
        rb.angularVelocity = Vector3.zero;

        hp.ResetHealth();

        // Important: start a fresh episode cleanly
        agent.EndEpisode();
    }
}
