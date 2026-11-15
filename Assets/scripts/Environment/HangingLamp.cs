using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tavandan sarkan maden lambası: ışık hafifçe titrer, lamba zincirinden hafifçe sallanır.
/// Bu script lambanın en üst noktasında (zincirin asıldığı yerde) durur; sallanma bu noktanın etrafında olur.
/// </summary>
public class HangingLamp : MonoBehaviour
{
    public Light2D lampLight;

    [Header("Titreme")]
    public float baseIntensity = 1.2f;
    [Tooltip("Işık şiddeti ne kadar oynasın")]
    public float flickerAmount = 0.12f;
    public float flickerSpeed = 4f;

    [Header("Sallanma")]
    [Tooltip("Derece cinsinden en fazla sallanma açısı")]
    public float swingAngle = 2.5f;
    public float swingSpeed = 0.8f;

    /// <summary>Sallanmayı geçici olarak büyütmek için (ör. deprem). 1 = normal.</summary>
    public float SwingMultiplier { get; set; } = 1f;

    float _seed;
    float _phase;

    void Awake()
    {
        _seed = Random.value * 100f; // her lamba farklı titresin
        _phase = _seed;
    }

    void Update()
    {
        float t = Time.time;

        if (lampLight != null)
        {
            float noise = Mathf.PerlinNoise(_seed, t * flickerSpeed) * 2f - 1f;
            lampLight.intensity = Mathf.Max(0f, baseIntensity + noise * flickerAmount);
        }

        if (swingAngle > 0f)
        {
            // Sarsıntıda daha hızlı ve geniş sallanır
            float speed = swingSpeed * Mathf.Lerp(1f, 3f, Mathf.Clamp01((SwingMultiplier - 1f) / 5f));
            _phase += Time.deltaTime * speed;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase) * swingAngle * SwingMultiplier);
        }
    }
}
