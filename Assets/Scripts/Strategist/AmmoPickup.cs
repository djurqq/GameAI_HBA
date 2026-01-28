using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
    public int ammoAmount = 12;
    public float respawnTime = 8f;

    bool available = true;

    void OnTriggerEnter(Collider other)
    {
        if (!available) return;

        Ammo a = other.GetComponent<Ammo>();
        if (a == null) return;

        a.Add(ammoAmount);
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
