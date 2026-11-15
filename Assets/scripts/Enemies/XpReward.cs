using UnityEngine;

/// <summary>Bu düşman ölünce oyuncuya XP verir.</summary>
[RequireComponent(typeof(Health))]
public class XpReward : MonoBehaviour
{
    public int xp = 20;
    [Tooltip("'+XP' yazısının ayaklara göre yüksekliği")]
    public float textHeight = 1.8f;

    Health _health;

    void Awake()
    {
        _health = GetComponent<Health>();
        _health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (_health != null) _health.Died -= OnDied;
    }

    void OnDied()
    {
        PlayerProgress.AddXp(xp);
        FloatingText.Spawn("+" + xp + " XP", transform.position + Vector3.up * textHeight,
                           new Color(1f, 0.85f, 0.3f), 4.5f, 1.3f);
    }
}
