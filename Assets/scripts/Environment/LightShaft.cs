using UnityEngine;

/// <summary>
/// Bulutların arasından sızan ışık huzmesi: saydamlığı yavaşça artıp azalır,
/// böylece bulutlar açılıp kapanıyormuş gibi görünür.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LightShaft : MonoBehaviour
{
    [Tooltip("En parlak anındaki saydamlık")]
    public float maxAlpha = 0.12f;
    [Tooltip("En sönük anındaki saydamlık")]
    public float minAlpha = 0.02f;
    [Tooltip("Bir tam belirip kaybolma süresi (saniye)")]
    public float period = 12f;

    SpriteRenderer _sr;
    float _offset;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _offset = Random.value * period;
    }

    void Update()
    {
        float k = 0.5f + 0.5f * Mathf.Sin((Time.time + _offset) / period * Mathf.PI * 2f);
        k = k * k; // çoğu zaman sönük, ara sıra belirgin
        var c = _sr.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, k);
        _sr.color = c;
    }
}
