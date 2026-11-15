using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Oyuncuyu yumuşak şekilde takip eden 2D kamera.
/// boundsTilemap atanırsa o tilemap'te çizili alanın dışını göstermez.
/// Sarsıntı: ShakeAmplitude'u sürekli ayarla ya da Shake(şiddet, süre) çağır.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("İsteğe bağlı: kamera bu tilemap'in sınırları dışına çıkmaz")]
    public Tilemap boundsTilemap;
    [Tooltip("Sınırların üstüne eklenecek boşluk (ör. açık havada gökyüzünü göstermek için)")]
    public float extraTop = 0f;     

    [Tooltip("Ekranda görünen yüksekliğin yarısı (birim)")]
    public float orthographicSize = 7f;
    [Tooltip("Kamera hedefin ne kadar üstüne baksın")]
    public Vector2 offset = new Vector2(0f, 1.5f);
    public float smoothTime = 0.12f;

    [Header("Sarsıntı")]
    [Tooltip("Sarsıntının hızı (yüksek = daha titrek)")]
    public float shakeFrequency = 22f;

    /// <summary>Sürekli sarsıntı şiddeti (birim). Cutscene gibi şeyler her kare ayarlayabilir.</summary>
    public float ShakeAmplitude { get; set; }

    Camera _cam;
    Vector3 _velocity;
    Vector3 _basePos;
    Bounds _bounds;
    bool _hasBounds;
    Bounds _override;
    bool _hasOverride;
    float _impulseAmp, _impulseTime, _impulseDuration;

    /// <summary>Kamerayı geçici olarak başka bir alana sınırla (ör. bina içi).</summary>
    public void SetBoundsOverride(Rect area)
    {
        _override.SetMinMax(new Vector3(area.xMin, area.yMin, 0f), new Vector3(area.xMax, area.yMax, 0f));
        _hasOverride = true;
    }

    public void ClearBoundsOverride() => _hasOverride = false;

    /// <summary>Kamerayı yumuşatmadan hemen hedefe ışınla.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        _velocity = Vector3.zero;
        _basePos = Clamp(Desired());
        transform.position = _basePos;
    }

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = orthographicSize;
        _basePos = transform.position;
    }

    void Start()
    {
        if (boundsTilemap != null)
        {
            boundsTilemap.CompressBounds();
            var cb = boundsTilemap.cellBounds;
            if (cb.size.x > 0 && cb.size.y > 0)
            {
                _bounds.SetMinMax(boundsTilemap.CellToWorld(cb.min), boundsTilemap.CellToWorld(cb.max) + Vector3.up * extraTop);
                _hasBounds = true;
            }
        }
        if (target != null) _basePos = Clamp(Desired());
        transform.position = _basePos;
    }

    /// <summary>Kısa bir sarsıntı: şiddet zamanla sıfıra iner.</summary>
    public void Shake(float amplitude, float duration)
    {
        _impulseAmp = Mathf.Max(_impulseAmp * (_impulseTime / Mathf.Max(0.0001f, _impulseDuration)), amplitude);
        _impulseDuration = Mathf.Max(0.0001f, duration);
        _impulseTime = _impulseDuration;
    }

    void LateUpdate()
    {
        if (target != null)
            _basePos = Clamp(Vector3.SmoothDamp(_basePos, Clamp(Desired()), ref _velocity, smoothTime));

        transform.position = _basePos + ShakeOffset();
    }

    Vector3 ShakeOffset()
    {
        float amp = ShakeAmplitude;
        if (_impulseTime > 0f)
        {
            _impulseTime -= Time.deltaTime;
            amp = Mathf.Max(amp, _impulseAmp * Mathf.Clamp01(_impulseTime / _impulseDuration));
        }
        if (amp <= 0f) return Vector3.zero;

        float t = Time.time * shakeFrequency;
        float x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;
        return new Vector3(x, y, 0f) * amp;
    }

    Vector3 Desired() =>
        new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);

    Vector3 Clamp(Vector3 p)
    {
        if (!_hasBounds && !_hasOverride) return p;
        var b = _hasOverride ? _override : _bounds;
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        p.x = b.size.x > halfW * 2f ? Mathf.Clamp(p.x, b.min.x + halfW, b.max.x - halfW) : b.center.x;
        p.y = b.size.y > halfH * 2f ? Mathf.Clamp(p.y, b.min.y + halfH, b.max.y - halfH) : b.center.y;
        return p;
    }
}
