using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Level 1 girişindeki deprem cutscene'i.
///
/// Akış:
///   1) Oyuncu kilitlenir, siyah sinema şeritleri gelir
///   2) Hafif titreme → güçlü deprem (kamera sarsılır, lambalar sert sallanır, tavandan taş düşer)
///   3) Sarsıntı yavaşça durur, şeritler çekilir, kontrol oyuncuya geçer
///
/// Esc ile atlanabilir. Oyuncu ölüp bölüm yeniden başlarsa tekrar oynamaz.
/// </summary>
public class EarthquakeIntro : MonoBehaviour
{
    [Header("Referanslar")]
    public PlayerController player;
    public PlayerCombat playerCombat;
    public CameraFollow cameraFollow;
    [Tooltip("Cutscene sırasında gizlenecek arayüz (ör. can yazısı)")]
    public GameObject hudToHide;
    public RectTransform topBar;
    public RectTransform bottomBar;

    [Header("Süreler (saniye)")]
    public float barsInTime = 0.5f;
    public float rumbleTime = 0.8f;
    public float quakeTime = 2.4f;
    public float settleTime = 1.2f;
    public float barsOutTime = 0.5f;

    [Header("Sarsıntı")]
    public float rumbleAmplitude = 0.08f;
    public float quakeAmplitude = 0.45f;
    [Tooltip("Depremde lambalar normalden kaç kat geniş sallansın")]
    public float lampSwingMultiplier = 7f;
    public float barHeight = 110f;

    [Header("Düşen taşlar")]
    public Sprite debrisSprite;
    public Color debrisColor = new Color(0.32f, 0.28f, 0.24f);
    [Tooltip("Taşların düştüğü yükseklik (maden tavanı)")]
    public float ceilingY = 13.8f;
    [Tooltip("Kameranın sağına/soluna ne kadar uzağa düşebilir")]
    public float debrisSpreadX = 10f;
    public float debrisPerSecond = 14f;
    public Vector2 debrisSizeRange = new Vector2(0.15f, 0.45f);
    public float debrisLifetime = 4f;
    public string debrisLayer = "Debris";

    HangingLamp[] _lamps;
    bool _skip;

    void Start()
    {
        if (CutsceneState.SkipIntroOnce)
        {
            CutsceneState.SkipIntroOnce = false;
            SetBars(0f);
            return;
        }
        _lamps = FindObjectsByType<HangingLamp>(FindObjectsSortMode.None);
        StartCoroutine(Play());
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) _skip = true;
    }

    IEnumerator Play()
    {
        CutsceneState.Begin();
        LockPlayer(true);
        if (hudToHide != null) hudToHide.SetActive(false);

        // 1) Şeritler gelir
        yield return Animate(barsInTime, k => SetBars(Smooth(k)));

        // 2a) Hafif titreme
        yield return Animate(rumbleTime, k =>
        {
            cameraFollow.ShakeAmplitude = rumbleAmplitude * k;
            SetLampSwing(Mathf.Lerp(1f, 2f, k));
        });

        // 2b) Güçlü deprem + taşlar
        float debrisTimer = 0f;
        yield return Animate(quakeTime, k =>
        {
            // başta sert vurur, sonra biraz dalgalanır
            float wave = 0.8f + 0.2f * Mathf.Sin(k * Mathf.PI * 6f);
            cameraFollow.ShakeAmplitude = Mathf.Lerp(rumbleAmplitude, quakeAmplitude, Mathf.Clamp01(k * 4f)) * wave;
            SetLampSwing(Mathf.Lerp(2f, lampSwingMultiplier, Mathf.Clamp01(k * 3f)));

            debrisTimer += Time.deltaTime * debrisPerSecond;
            while (debrisTimer >= 1f) { debrisTimer -= 1f; SpawnDebris(); }
        });

        // 3) Sakinleşme
        float startAmp = cameraFollow.ShakeAmplitude;
        yield return Animate(settleTime, k =>
        {
            cameraFollow.ShakeAmplitude = Mathf.Lerp(startAmp, 0f, Smooth(k));
            SetLampSwing(Mathf.Lerp(lampSwingMultiplier, 1f, Smooth(k)));
            if (Random.value < Time.deltaTime * debrisPerSecond * (1f - k) * 0.4f) SpawnDebris();
        });

        Finish(); // şeritleri çeker, kontrolü geri verir
    }

    void Finish()
    {
        StopAllCoroutines();
        CutsceneState.End();
        cameraFollow.ShakeAmplitude = 0f;
        SetLampSwing(1f);
        LockPlayer(false);
        if (hudToHide != null) hudToHide.SetActive(true);
        if (_skip) SetBars(0f);
        else StartCoroutine(Animate(barsOutTime, k => SetBars(1f - Smooth(k))));
        enabled = false;
    }

    /// <summary>k: 0→1 arası ilerleme. Esc basılırsa cutscene hemen biter.</summary>
    IEnumerator Animate(float duration, System.Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            if (_skip) { Finish(); yield break; }
            t += Time.deltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }

    static float Smooth(float k) => k * k * (3f - 2f * k);

    void SetBars(float k)
    {
        float h = barHeight * k;
        if (topBar != null) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, h);
        if (bottomBar != null) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, h);
    }

    void SetLampSwing(float m)
    {
        if (_lamps == null) return;
        foreach (var l in _lamps) if (l != null) l.SwingMultiplier = m;
    }

    void LockPlayer(bool locked)
    {
        if (player != null)
        {
            player.enabled = !locked;
            var rb = player.GetComponent<Rigidbody2D>();
            if (locked && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 3f;
            }
        }
        if (playerCombat != null) playerCombat.enabled = !locked;
    }

    void SpawnDebris()
    {
        var cam = cameraFollow.transform.position;
        float size = Random.Range(debrisSizeRange.x, debrisSizeRange.y);
        var go = new GameObject("Debris");
        int layer = LayerMask.NameToLayer(debrisLayer);
        if (layer >= 0) go.layer = layer;
        go.transform.position = new Vector3(cam.x + Random.Range(-debrisSpreadX, debrisSpreadX), ceilingY - size, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        go.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1.2f), 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = debrisSprite;
        sr.color = debrisColor * Random.Range(0.8f, 1.2f);
        sr.sortingOrder = 6;

        go.AddComponent<BoxCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2f;
        rb.angularVelocity = Random.Range(-360f, 360f);

        Destroy(go, debrisLifetime + Random.Range(0f, 1f));
    }
}
