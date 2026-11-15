using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ayarlar menüsündeki dil seçimi: "Türkçe" ve "English" butonları.
/// Seçilen dilin butonu vurgulanır. Tıklayınca dil anında değişir ve kaydedilir.
/// </summary>
public class LanguageSelector : MonoBehaviour
{
    public Button turkishButton;
    public Button englishButton;
    public Color selectedColor = new Color(1f, 0.85f, 0.45f, 1f);
    public Color normalColor = Color.white;

    TMP_Text _turkishLabel;
    TMP_Text _englishLabel;

    void Awake()
    {
        _turkishLabel = EnsureLabel(turkishButton, "lang.tr");
        _englishLabel = EnsureLabel(englishButton, "lang.en");
        if (turkishButton != null) turkishButton.onClick.AddListener(() => Localization.SetLanguage(Language.Turkish));
        if (englishButton != null) englishButton.onClick.AddListener(() => Localization.SetLanguage(Language.English));
    }

    void OnEnable()
    {
        Localization.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable() => Localization.LanguageChanged -= Refresh;

    void Refresh()
    {
        if (_turkishLabel != null) _turkishLabel.text = Localization.Get("lang.tr");
        if (_englishLabel != null) _englishLabel.text = Localization.Get("lang.en");
        Mark(turkishButton, Localization.Current == Language.Turkish);
        Mark(englishButton, Localization.Current == Language.English);
    }

    TMP_Text EnsureLabel(Button button, string languageKey)
    {
        if (button == null) return null;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        var existingText = FindFirstObjectByType<TMP_Text>();
        if (label == null)
        {
            var go = new GameObject("Label (TMP)", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            label = go.AddComponent<TextMeshProUGUI>();
            if (existingText != null) label.font = existingText.font;
            label.fontSize = 30f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        label.text = Localization.Get(languageKey);
        return label;
    }

    void Mark(Button b, bool selected)
    {
        if (b == null) return;
        var colors = b.colors;
        colors.normalColor = selected ? selectedColor : normalColor;
        colors.highlightedColor = selected ? selectedColor : new Color(0.961f, 0.961f, 0.961f, 1f);
        colors.selectedColor = colors.normalColor;
        b.colors = colors;
        if (b.targetGraphic != null) b.targetGraphic.color = selected ? selectedColor : normalColor;
    }
}
