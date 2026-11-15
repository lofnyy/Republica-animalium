using TMPro;
using UnityEngine;

/// <summary>
/// Dünyada yukarı süzülüp kaybolan kısa yazı (hasar sayısı, "+20 XP" gibi).
/// FloatingText.Spawn(...) ile oluşturulur.
/// </summary>
public class FloatingText : MonoBehaviour
{
    TextMeshPro _text;
    Color _color;
    float _life;
    float _t;
    Vector3 _velocity;

    public static void Spawn(string text, Vector3 position, Color color, float size = 4f, float life = 0.9f)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = position;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);
        tmp.rectTransform.sizeDelta = new Vector2(6f, 2f);
        tmp.sortingOrder = 500;

        var ft = go.AddComponent<FloatingText>();
        ft._text = tmp;
        ft._color = color;
        ft._life = life;
        ft._velocity = new Vector3(Random.Range(-0.4f, 0.4f), 2.2f, 0f);
    }

    void Update()
    {
        _t += Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;
        _velocity.y = Mathf.Max(0.3f, _velocity.y - 3f * Time.deltaTime);

        float k = _t / _life;
        var c = _color;
        c.a = k < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.6f) / 0.4f);
        _text.color = c;
        float pop = _t < 0.1f ? Mathf.Lerp(1.4f, 1f, _t / 0.1f) : 1f;
        transform.localScale = Vector3.one * pop;

        if (_t >= _life) Destroy(gameObject);
    }
}
