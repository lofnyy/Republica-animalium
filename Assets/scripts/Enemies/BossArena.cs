using UnityEngine;

/// <summary>
/// Oyuncu arenaya girince duvarlar kapanır, boss uyanır ve üstte can barı çıkar.
/// Boss ölünce duvarlar açılır.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossArena : MonoBehaviour
{
    public BossBase boss;
    [Tooltip("Dövüş sırasında kapanan duvarlar (collider + görsel)")]
    public GameObject[] walls;
    public BossHealthUI bossUI;
    [Tooltip("Boss ölünce açılacak objeler (ör. çıkış kapısı)")]
    public GameObject[] activateOnDefeat;
    [Tooltip("Boss ölünce onun olduğu yere bırakılıp açılacak obje (ör. parşömen)")]
    public GameObject dropOnDefeat;
    public Vector2 dropOffset = new Vector2(0f, 3f);

    bool _started;

    void Awake() => SetWalls(false);

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_started || boss == null || !other.CompareTag("Player")) return;
        _started = true;
        SetWalls(true);
        boss.Defeated += OnBossDefeated;
        boss.Activate();
        if (bossUI != null) bossUI.Show(boss.GetComponent<Health>(), boss.bossName);
        var cam = FindFirstObjectByType<CameraFollow>();
        if (cam != null) cam.Shake(0.3f, 0.6f);
    }

    void OnBossDefeated()
    {
        if (dropOnDefeat != null && boss != null)
        {
            var p = boss.transform.position;
            dropOnDefeat.transform.position = new Vector3(p.x + dropOffset.x, Mathf.Max(p.y, 0f) + dropOffset.y, 0f);
            dropOnDefeat.SetActive(true);
        }
        SetWalls(false);
        if (bossUI != null) bossUI.Hide();
        if (activateOnDefeat != null)
            foreach (var go in activateOnDefeat) if (go != null) go.SetActive(true);
    }

    void SetWalls(bool on)
    {
        foreach (var w in walls) if (w != null) w.SetActive(on);
    }
}
