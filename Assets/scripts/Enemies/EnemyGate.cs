using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Barikat: listedeki düşmanların hepsi ölünce yıkılır ve yol açılır.
/// breakApart açıksa barikatın parçaları savrulup düşer,
/// kapalıysa 'raise' objesi yukarı kalkar (ör. demir parmaklık).
/// </summary>
public class EnemyGate : MonoBehaviour
{
    [Tooltip("Hepsi ölünce barikat açılır")]
    public List<Health> enemies = new List<Health>();
    [Tooltip("Yolu kapatan collider")]
    public Collider2D blocker;
    public string debrisLayer = "Debris";

    [Header("Açılış şekli")]
    [Tooltip("Açık: parçalar savrulur. Kapalı: 'raise' yukarı kalkar")]
    public bool breakApart = true;
    public Transform raise;
    public float raiseHeight = 6f;
    public float raiseTime = 1.2f;
    [Tooltip("Açılınca etkinleşecek bileşenler (ör. kapı tetikleyicisi)")]
    public Behaviour[] enableOnOpen;

    bool _open;

    void Update()
    {
        if (_open) return;
        foreach (var e in enemies)
            if (e != null && !e.IsDead) return;
        Open();
    }

    public void Open()
    {
        _open = true;
        if (blocker != null) blocker.enabled = false;

        var cam = FindFirstObjectByType<CameraFollow>();
        if (cam != null) cam.Shake(0.2f, 0.3f);
        if (enableOnOpen != null)
            foreach (var b in enableOnOpen) if (b != null) b.enabled = true;

        if (!breakApart)
        {
            if (raise != null) StartCoroutine(Raise());
            return;
        }

        int layer = LayerMask.NameToLayer(debrisLayer);
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            var go = sr.gameObject;
            if (layer >= 0) go.layer = layer;
            if (go.GetComponent<Collider2D>() == null) go.AddComponent<BoxCollider2D>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.5f;
            rb.linearVelocity = new Vector2(Random.Range(1f, 5f), Random.Range(2f, 6f));
            rb.angularVelocity = Random.Range(-400f, 400f);
            Destroy(go, Random.Range(2.5f, 3.5f));
        }
        enabled = false;
    }

    IEnumerator Raise()
    {
        var start = raise.localPosition;
        var end = start + Vector3.up * raiseHeight;
        float t = 0f;
        while (t < raiseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / raiseTime);
            raise.localPosition = Vector3.Lerp(start, end, k) + new Vector3(Mathf.Sin(t * 70f) * 0.02f, 0f, 0f);
            yield return null;
        }
        raise.localPosition = end;
        enabled = false;
    }
}
