using System;
using UnityEngine;

/// <summary>
/// Oyuncunun XP'si ve seviyesi. Sahneler arasında (ve ölünce) korunur.
/// Düşmanlar ölünce XpReward üzerinden AddXp çağrılır.
/// </summary>
public static class PlayerProgress
{
    public static int Level { get; private set; } = 1;
    public static int Xp { get; private set; }

    /// <summary>Bir sonraki seviye için gereken XP ("Can Yenile" bedeli bu seviye için artırır).</summary>
    public static int XpToNext => Mathf.RoundToInt((60 + (Level - 1) * 40) * NextLevelXpMultiplier);

    /// <summary>(xp, gereken xp, seviye)</summary>
    public static event Action<int, int, int> Changed;
    /// <summary>Yeni seviye</summary>
    public static event Action<int> LeveledUp;

    // ---- seviye atlayınca seçilen özellikler (bölümler arası ve ölünce korunur)
    /// <summary>Seçilmeyi bekleyen özellik sayısı (her seviye atlayışta 1 artar)</summary>
    public static int PendingPicks;
    /// <summary>"Güçlü vuruş" ile eklenen hasar</summary>
    public static int BonusDamage;
    /// <summary>"Dayanıklılık" kaç kez alındı (her biri en fazla canı %25 artırır)</summary>
    public static int MaxHealthSteps;
    /// <summary>"Çift vuruş" alındı mı</summary>
    public static bool DoubleHit;

    // ---- özelliklerin bedelleri
    /// <summary>"Güçlü vuruş" bedeli: saldırı bekleme süresine eklenen oran (0.15 = %15 yavaş)</summary>
    public static float AttackSlowdown;
    /// <summary>"Dayanıklılık" bedeli: hareket hızı çarpanı</summary>
    public static float MoveSpeedMultiplier = 1f;
    /// <summary>"Can yenile" bedeli: yalnızca bir sonraki seviyenin XP ihtiyacı çarpanı</summary>
    public static float NextLevelXpMultiplier = 1f;

    public static void AddXp(int amount)
    {
        if (amount <= 0) return;
        Xp += amount;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            NextLevelXpMultiplier = 1f; // ceza sadece bir seviye sürer
            Level++;
            PendingPicks++;
            LeveledUp?.Invoke(Level);
        }
        Changed?.Invoke(Xp, XpToNext, Level);
    }

    /// <summary>XP ihtiyacı değişince arayüzü yeniler.</summary>
    public static void NotifyChanged() => Changed?.Invoke(Xp, XpToNext, Level);

    public static void ResetProgress()
    {
        Level = 1;
        Xp = 0;
        PendingPicks = 0; BonusDamage = 0; MaxHealthSteps = 0; DoubleHit = false;
        AttackSlowdown = 0f; MoveSpeedMultiplier = 1f; NextLevelXpMultiplier = 1f;
        Changed?.Invoke(Xp, XpToNext, Level);
    }

    // Editörde "Domain Reload" kapalıysa da her Play'de sıfırdan başlasın
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Level = 1;
        Xp = 0;
        PendingPicks = 0; BonusDamage = 0; MaxHealthSteps = 0; DoubleHit = false;
        AttackSlowdown = 0f; MoveSpeedMultiplier = 1f; NextLevelXpMultiplier = 1f;
        Changed = null;
        LeveledUp = null;
    }
}
