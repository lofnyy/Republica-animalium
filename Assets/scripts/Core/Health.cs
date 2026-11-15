using System;
using UnityEngine;

/// <summary>
/// Can sistemi. Hem oyuncu hem düşmanlar kullanır.
/// Hasar alınca kısa süre dokunulmaz olabilir (invulnerableTime).
/// </summary>
public class Health : MonoBehaviour
{
    public int maxHealth = 5;
    [Tooltip("Hasar aldıktan sonra tekrar hasar alamayacağı süre (saniye)")]
    public float invulnerableTime = 0f;
    [Tooltip("Gelen hasar bununla çarpılır (oyuncunun canı daha ince bölünebilsin diye)")]
    public int damageMultiplier = 1;

    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    /// <summary>Canın yüzdesi (0..1)</summary>
    public float Percent => maxHealth > 0 ? Mathf.Clamp01((float)Current / maxHealth) : 0f;
    public bool IsInvulnerable => _invulTimer > 0f;
    /// <summary>Dışarıdan açılan dokunulmazlık (ör. dash sırasında). Yanıp sönme yok.</summary>
    public bool Immune { get; set; }

    /// <summary>(şu anki can, en yüksek can)</summary>
    public event Action<int, int> Changed;
    /// <summary>Hasar veren şeyin pozisyonu (geri savrulma yönü için)</summary>
    public event Action<Vector2> Damaged;
    public event Action Died;

    float _invulTimer;

    void Awake()
    {
        Current = maxHealth;
    }

    void Update()
    {
        if (_invulTimer > 0f) _invulTimer -= Time.deltaTime;
    }

    /// <summary>Hasar verir. Hasar işlendiyse true döner.</summary>
    public bool TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (IsDead || IsInvulnerable || Immune || amount <= 0) return false;

        amount *= Mathf.Max(1, damageMultiplier);
        Current = Mathf.Max(0, Current - amount);
        _invulTimer = invulnerableTime;

        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke(sourcePosition);
        if (Current == 0) Died?.Invoke();
        return true;
    }

    /// <summary>En fazla canı değiştirir; heal kadar can da ekler (Current yeni sınırı aşmaz).</summary>
    public void SetMax(int newMax, int heal)
    {
        maxHealth = Mathf.Max(1, newMax);
        Current = Mathf.Clamp(Current + Mathf.Max(0, heal), 0, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }
}
