using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Kilise kapısı: oyuncu kapının önündeyken "[E] Gir" yazısı çıkar.
/// E (ya da gamepad Y) ile ekran kararır, oyuncu iç mekâna ışınlanır,
/// kamera iç mekânla sınırlanır, ışık kısılır, yağmur durur.
/// İçeride ölüp kayıt noktasından dönülürse iç mekân ayarları hemen uygulanır.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ChurchDoor : MonoBehaviour
{
    [Header("Referanslar")]
    public PlayerController player;
    public PlayerCombat playerCombat;
    public CameraFollow cameraFollow;
    [Tooltip("Kapının üstündeki '[E] Gir' yazısı")]
    public GameObject prompt;
    [Tooltip("Tam ekran siyah görsel (geçiş için)")]
    public Image fade;
    [Tooltip("İçeride oyuncunun belireceği nokta")]
    public Transform interiorSpawn;

    [Header("İç mekân")]
    [Tooltip("Kameranın içeride göstereceği alan")]
    public Rect interiorBounds = new Rect(499f, -2f, 43f, 15f);
    public Light2D globalLight;
    public float insideLightIntensity = 0.18f;
    public Color insideLightColor = new Color(0.6f, 0.6f, 0.75f);
    [Tooltip("İçeride kapatılacak yağmur")]
    public ParticleSystem rain;

    [Header("Geçiş")]
    public float fadeTime = 0.6f;

    bool _playerNear;
    bool _entered;
    float _outsideIntensity;
    Color _outsideColor;

    /// <summary>Kapıdan girildi, kontrol oyuncuya geri verildi (hikâye buradan devam edebilir).</summary>
    public event System.Action Entered;

    void Start()
    {
        LocalizedText.Bind(prompt, "prompt.enter");
        if (globalLight != null) { _outsideIntensity = globalLight.intensity; _outsideColor = globalLight.color; }
        if (prompt != null) prompt.SetActive(false);
        SetFade(0f);
        // Kayıt noktasından içeride başlandıysa
        if (player != null && interiorBounds.Contains(player.transform.position)) ApplyInside();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNear = true;
        if (prompt != null && !_entered) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNear = false;
        if (prompt != null) prompt.SetActive(false);
    }

    void Update()
    {
        if (_entered || !_playerNear || CutsceneState.IsActive) return;
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        if ((kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame))
            Interact();
    }

    /// <summary>Kapıdan gir (E tuşu bunu çağırır).</summary>
    public void Interact()
    {
        if (_entered) return;
        StartCoroutine(Enter());
    }

    IEnumerator Enter()
    {
        _entered = true;
        if (prompt != null) prompt.SetActive(false);
        CutsceneState.Begin();
        if (playerCombat != null) playerCombat.enabled = false;
        player.ForcedMoveInput = 0f;

        yield return Fade(0f, 1f);

        player.Teleport(interiorSpawn.position);
        player.FaceDirection(1);
        ApplyInside();
        CheckpointState.Set(interiorSpawn.position);

        yield return new WaitForSeconds(0.25f);
        if (cameraFollow != null) cameraFollow.Shake(0.25f, 0.4f); // kapı arkadan çarparak kapanır
        yield return Fade(1f, 0f);

        player.ForcedMoveInput = null;
        if (playerCombat != null) playerCombat.enabled = true;
        CutsceneState.End();
        Entered?.Invoke();
    }

    /// <summary>Dış mekân ayarlarını geri getirir (kiliseden çıkınca).</summary>
    public void ApplyOutside()
    {
        if (cameraFollow != null) cameraFollow.ClearBoundsOverride();
        if (globalLight != null)
        {
            globalLight.intensity = _outsideIntensity;
            globalLight.color = _outsideColor;
        }
        if (rain != null)
        {
            var em = rain.emission;
            em.enabled = true;
            rain.Play();
        }
    }

    void ApplyInside()
    {
        _entered = true;
        if (cameraFollow != null)
        {
            cameraFollow.SetBoundsOverride(interiorBounds);
            cameraFollow.SnapToTarget();
        }
        if (globalLight != null)
        {
            globalLight.intensity = insideLightIntensity;
            globalLight.color = insideLightColor;
        }
        if (rain != null)
        {
            var em = rain.emission;
            em.enabled = false;
            rain.Clear(true);
        }
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeTime)));
            yield return null;
        }
        SetFade(to);
    }

    void SetFade(float a)
    {
        if (fade == null) return;
        var c = fade.color; c.a = a; fade.color = c;
        fade.raycastTarget = false;
        fade.enabled = a > 0.001f;
    }
}
