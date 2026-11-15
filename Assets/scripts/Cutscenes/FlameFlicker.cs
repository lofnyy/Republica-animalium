using UnityEngine;

/// <summary>Basit alev titremesi (çocuk sprite'ların boyunu oynatır).</summary>
public class FlameFlicker : MonoBehaviour
{
    Vector3[] _base;
    float _seed;

    void Start()
    {
        _seed = Random.value * 10f;
        _base = new Vector3[transform.childCount];
        for (int i = 0; i < transform.childCount; i++) _base[i] = transform.GetChild(i).localScale;
    }

    void Update()
    {
        for (int i = 0; i < _base.Length; i++)
        {
            var c = transform.GetChild(i);
            if (c.GetComponent<SpriteRenderer>() == null) continue;
            float n = Mathf.PerlinNoise(_seed + i, Time.time * 6f);
            c.localScale = new Vector3(_base[i].x * (0.9f + n * 0.2f), _base[i].y * (0.8f + n * 0.4f), 1f);
        }
    }
}
