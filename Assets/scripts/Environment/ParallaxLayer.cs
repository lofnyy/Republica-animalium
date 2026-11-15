using UnityEngine;

/// <summary>
/// Uzaktaki şeyler (ay, uzak silüetler) kamerayla birlikte yavaşça kayar.
/// factor = 1: kamerayla tam birlikte hareket eder (sonsuz uzak), 0: hiç kaymaz.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Range(0f, 1f)] public float factorX = 0.8f;
    [Range(0f, 1f)] public float factorY = 0.8f;

    Transform _cam;
    Vector3 _start;
    Vector3 _camStart;

    void Start()
    {
        if (Camera.main != null) _cam = Camera.main.transform;
        _start = transform.position;
        if (_cam != null) _camStart = _cam.position;
    }

    void LateUpdate()
    {
        if (_cam == null) return;
        var d = _cam.position - _camStart;
        transform.position = _start + new Vector3(d.x * factorX, d.y * factorY, 0f);
    }
}
