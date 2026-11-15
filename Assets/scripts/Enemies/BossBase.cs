using System;
using UnityEngine;

/// <summary>
/// Bütün boss'ların ortak tabanı. BossArena bununla çalışır.
/// </summary>
public abstract class BossBase : MonoBehaviour
{
    [Header("Boss")]
    public string bossName = "???";

    /// <summary>Boss öldüğünde (ölüm animasyonu bitince) çağrılır.</summary>
    public event Action Defeated;

    /// <summary>Dövüşü başlat.</summary>
    public abstract void Activate();

    protected void RaiseDefeated() => Defeated?.Invoke();
}
