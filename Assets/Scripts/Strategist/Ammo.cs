using UnityEngine;

public class Ammo : MonoBehaviour
{
    public int maxAmmo = 30;
    public int currentAmmo = 15;

    public float Percent => maxAmmo <= 0 ? 0f : Mathf.Clamp01((float)currentAmmo / maxAmmo);

    public bool Use(int amount)
    {
        if (currentAmmo < amount) return false;
        currentAmmo -= amount;
        return true;
    }

    public void Add(int amount)
    {
        currentAmmo = Mathf.Min(maxAmmo, currentAmmo + amount);
    }
}
