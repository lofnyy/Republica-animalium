using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ateş ışığı: şiddeti ve menzili hızlı ve düzensiz titrer.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class FireLight : MonoBehaviour
{
    public float baseIntensity = 1.1f;
    public float intensityFlicker = 0.35f;
    public float baseRadius = 4.5f;
    public float radiusFlicker = 0.4f;
    public float speed = 9f;

    Light2D _light;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light2D>();
        _seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed;
        float a = Mathf.PerlinNoise(_seed, t) * 2f - 1f;
        float b = Mathf.PerlinNoise(t * 0.7f, _seed) * 2f - 1f;
        _light.intensity = Mathf.Max(0f, baseIntensity + a * intensityFlicker);
        _light.pointLightOuterRadius = Mathf.Max(0.5f, baseRadius + b * radiusFlicker);
    }
}
