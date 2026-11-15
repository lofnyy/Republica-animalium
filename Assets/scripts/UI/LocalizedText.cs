using TMPro;
using UnityEngine;

/// <summary>
/// Sahnede duran sabit bir yazıyı (buton, başlık, ipucu) seçili dile göre gösterir.
/// Dil değişince kendiliğinden yenilenir. key = strings.json'daki kimlik.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    public string key;
    [Tooltip("Metnin başına eklenecek sabit önek (ör. \"» \")")]
    public string prefix = "";

    TMP_Text _text;

    void Awake() => _text = GetComponent<TMP_Text>();

    public static LocalizedText Bind(TMP_Text text, string localizationKey)
    {
        if (text == null) return null;
        var localized = text.GetComponent<LocalizedText>();
        if (localized == null) localized = text.gameObject.AddComponent<LocalizedText>();
        localized.SetKey(localizationKey);
        return localized;
    }

    public static LocalizedText Bind(GameObject root, string localizationKey)
    {
        return root == null ? null : Bind(root.GetComponentInChildren<TMP_Text>(true), localizationKey);
    }

    void OnEnable()
    {
        Localization.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable() => Localization.LanguageChanged -= Refresh;

    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }

    public void Refresh()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
        if (_text != null && !string.IsNullOrEmpty(key)) _text.text = prefix + Localization.Get(key);
    }
}
