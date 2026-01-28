using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    public float healAmount = 40f;
    public float respawnTime = 8f;

    bool available = true;

    void OnTriggerEnter(Collider other)
    {
        if (!available) return;

        Health h = other.GetComponent<Health>();
        if (h == null) return;

        h.Heal(healAmount);
        StartCoroutine(Respawn());
    }

    System.Collections.IEnumerator Respawn()
    {
        available = false;
        gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnTime);
        gameObject.SetActive(true);
        available = true;
    }
}
