using UnityEngine;
using System.Collections;

public class HealthPickup : MonoBehaviour
{
    public float healAmount = 40f;
    public float respawnTime = 8f;

    bool available = true;
    Collider col;
    Renderer[] rends;

    void Awake()
    {
        col = GetComponent<Collider>();
        rends = GetComponentsInChildren<Renderer>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!available) return;

        Health h = other.GetComponentInParent<Health>();
        if (h == null) return;

        h.Heal(healAmount);
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        available = false;

        // "disable" pickup without deactivating object
        if (col != null) col.enabled = false;
        foreach (var r in rends) r.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        // re-enable
        foreach (var r in rends) r.enabled = true;
        if (col != null) col.enabled = true;

        available = true;
    }
}
