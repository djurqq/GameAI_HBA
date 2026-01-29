using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    public float Percent => Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => currentHealth <= 0f;

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - amount);
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }
}
