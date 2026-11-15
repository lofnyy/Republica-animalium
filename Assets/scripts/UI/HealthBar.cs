using TMPro;
using UnityEngine;

/// <summary>
/// Karakterin üstünde duran dünya içi can barı.
/// "fill" objesi soldan sağa canla orantılı küçülür, üstünde "%67" gibi yüzde yazar.
/// </summary>
public class HealthBar : MonoBehaviour
{
    public Health health;
    [Tooltip("Kırmızı dolgu objesi (genişliği 'width', ortası barın ortası)")]
    public Transform fill;
    public float width = 1f;

    [Header("Yüzde yazısı")]
    public bool showPercent = true;
    [Tooltip("Boşsa otomatik oluşturulur")]
    public TextMeshPro percentText;
    public float textSize = 2.2f;
    public float textOffsetY = 0.22f;

    [Header("Renk")]
    public bool colorByPercent = true;
    public Color highColor = new Color(0.35f, 0.85f, 0.35f);
    public Color midColor = new Color(0.95f, 0.8f, 0.2f);
    public Color lowColor = new Color(0.9f, 0.2f, 0.2f);

    SpriteRenderer _fillRenderer;

    void Start()
    {
        if (health == null) health = GetComponentInParent<Health>();
        if (fill != null) _fillRenderer = fill.GetComponent<SpriteRenderer>();
        if (showPercent && percentText == null) CreateText();
        health.Changed += OnChanged;
        OnChanged(health.Current, health.maxHealth);
    }

    void OnEnable() => Localization.LanguageChanged += RefreshPercent;

    void OnDisable() => Localization.LanguageChanged -= RefreshPercent;

    void CreateText()
    {
        var go = new GameObject("Percent");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, textOffsetY, 0f);
        percentText = go.AddComponent<TextMeshPro>();
        percentText.fontSize = textSize;
        percentText.alignment = TextAlignmentOptions.Center;
        percentText.fontStyle = FontStyles.Bold;
        percentText.outlineWidth = 0.3f;
        percentText.outlineColor = new Color32(0, 0, 0, 230);
        percentText.rectTransform.sizeDelta = new Vector2(3f, 1f);
        percentText.sortingOrder = 200;
    }

    void OnDestroy()
    {
        if (health != null) health.Changed -= OnChanged;
    }

    void OnChanged(int current, int max)
    {
        float f = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (fill != null)
        {
            var s = fill.localScale;
            fill.localScale = new Vector3(width * f, s.y, 1f);
            fill.localPosition = new Vector3(-width * 0.5f + width * f * 0.5f, fill.localPosition.y, 0f);
        }
        if (colorByPercent && _fillRenderer != null)
            _fillRenderer.color = f > 0.5f ? Color.Lerp(midColor, highColor, (f - 0.5f) * 2f)
                                           : Color.Lerp(lowColor, midColor, f * 2f);
        if (percentText != null)
        {
            RefreshPercent();
            percentText.gameObject.SetActive(current > 0);
        }
    }

    void RefreshPercent()
    {
        if (percentText == null || health == null) return;
        float percent = health.maxHealth > 0 ? (float)health.Current / health.maxHealth : 0f;
        percentText.text = Localization.Get("hud.percent", Mathf.RoundToInt(percent * 100f));
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.identity; // karakter dönse de bar düz kalsın
    }
}
