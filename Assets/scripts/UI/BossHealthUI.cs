using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ekranın üstündeki boss can barı.
/// </summary>
public class BossHealthUI : MonoBehaviour
{
    public GameObject root;
    public TMP_Text nameText;
    [Tooltip("Kırmızı dolgu (Image, Filled / Horizontal)")]
    public Image fill;
    [Tooltip("Hasar alınca geriden yavaşça inen açık renk dolgu")]
    public Image trail;
    public float trailSpeed = 0.6f;

    Health _health;
    string _nameKey;

    void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    void OnEnable() => Localization.LanguageChanged += RefreshName;
    void OnDisable() => Localization.LanguageChanged -= RefreshName;

    void RefreshName()
    {
        if (nameText != null && _nameKey != null) nameText.text = Localization.Has(_nameKey) ? Localization.Get(_nameKey) : _nameKey;
    }

    /// <summary>nameOrKey: Localization kimliği ("boss.rhino") ya da düz isim.</summary>
    public void Show(Health health, string nameOrKey)
    {
        _health = health;
        _nameKey = nameOrKey;
        RefreshName();
        root.SetActive(true);
        _health.Changed += OnChanged;
        float f = (float)health.Current / health.maxHealth;
        fill.fillAmount = f;
        if (trail != null) trail.fillAmount = f;
    }

    public void Hide()
    {
        if (_health != null) _health.Changed -= OnChanged;
        _health = null;
        root.SetActive(false);
    }

    void OnChanged(int current, int max)
    {
        fill.fillAmount = max > 0 ? (float)current / max : 0f;
    }

    void Update()
    {
        if (trail == null || !root.activeSelf) return;
        if (trail.fillAmount > fill.fillAmount)
            trail.fillAmount = Mathf.MoveTowards(trail.fillAmount, fill.fillAmount, trailSpeed * Time.deltaTime);
        else trail.fillAmount = fill.fillAmount;
    }
}
