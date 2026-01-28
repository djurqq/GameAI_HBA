using UnityEngine;
using System.Collections;

public class AmmoPickup : MonoBehaviour
{
    public int ammoAmount = 12;
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

        Ammo a = other.GetComponentInParent<Ammo>();
        if (a == null) return;

        a.Add(ammoAmount);
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        available = false;

        if (col != null) col.enabled = false;
        foreach (var r in rends) r.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        foreach (var r in rends) r.enabled = true;
        if (col != null) col.enabled = true;

        available = true;
    }
}
