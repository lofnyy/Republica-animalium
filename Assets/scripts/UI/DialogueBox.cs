using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ekranın üstündeki konuşma kutusu. Metni harf harf yazar.
/// Devam: E / Space / Enter / sol tık / gamepad A.
/// Yazı bitmeden basılırsa satırın tamamı hemen görünür.
///
/// PlayKeys(konuşmacıKimliği, replikKimlikleri...) metni Localization'dan çeker;
/// ekranda açıkken dil değişirse satır anında yeni dilde yeniden yazılır.
/// Play(konuşmacı, satırlar) düz metin içindir (eski kullanım).
/// </summary>
public class DialogueBox : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public TMP_Text continueHint;
    [Tooltip("Saniyede kaç harf yazılsın")]
    public float charsPerSecond = 40f;

    public bool IsOpen => panel != null && panel.activeSelf;

    bool _advanceRequested;
    bool _keyed;          // şu anki satır kimlik mi (true) düz metin mi (false)
    string _speaker;      // kimlik ya da düz metin
    string _line;
    bool _fullyShown;

    /// <summary>Konuşmayı ilerletir (ekran butonu, dokunmatik ekran vb. için).</summary>
    public void Advance() => _advanceRequested = true;

    void Awake()
    {
        LocalizedText.Bind(continueHint, "ui.continue");
        if (panel != null) panel.SetActive(false);
    }

    void OnEnable() => Localization.LanguageChanged += OnLanguageChanged;
    void OnDisable() => Localization.LanguageChanged -= OnLanguageChanged;

    /// <summary>Düz metinle konuşma (çevrilmez).</summary>
    public IEnumerator Play(string speaker, string[] lines) => Run(speaker, lines, false);

    /// <summary>Kimlikle konuşma: metinler seçili dilde gösterilir.</summary>
    public IEnumerator PlayKeys(string speakerKey, params string[] lineKeys) => Run(speakerKey, lineKeys, true);

    IEnumerator Run(string speaker, string[] lines, bool keyed)
    {
        panel.SetActive(true);

        foreach (var line in lines)
        {
            _keyed = keyed;
            _speaker = speaker;
            _line = line;
            _fullyShown = false;
            ApplyTexts();
            bodyText.maxVisibleCharacters = 0;
            if (continueHint != null) continueHint.gameObject.SetActive(false);

            yield return null; // açan tuş basışı hemen atlatmasın
            float shown = 0f;
            while (bodyText.maxVisibleCharacters < bodyText.textInfo.characterCount)
            {
                int total = bodyText.textInfo.characterCount;
                if (ContinuePressed()) { bodyText.maxVisibleCharacters = total; break; }
                shown += Time.deltaTime * charsPerSecond;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            _fullyShown = true;
            bodyText.maxVisibleCharacters = int.MaxValue;

            if (continueHint != null) continueHint.gameObject.SetActive(true);
            yield return null;
            while (!ContinuePressed()) yield return null;
        }

        panel.SetActive(false);
        _keyed = false;
    }

    string Resolve(string s) => _keyed ? Localization.Get(s) : s;

    void ApplyTexts()
    {
        string name = Resolve(_speaker);
        speakerText.text = name;
        speakerText.gameObject.SetActive(!string.IsNullOrEmpty(name));
        bodyText.text = Resolve(_line);
        bodyText.ForceMeshUpdate();
    }

    void OnLanguageChanged()
    {
        if (!IsOpen || !_keyed) return;
        int visible = bodyText.maxVisibleCharacters;
        ApplyTexts();
        bodyText.maxVisibleCharacters = _fullyShown ? int.MaxValue : visible;
    }

    bool ContinuePressed()
    {
        if (_advanceRequested) { _advanceRequested = false; return true; }
        if (MobileInput.TapAdvance) return true;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var gp = Gamepad.current;
        return (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (gp != null && gp.buttonSouth.wasPressedThisFrame);
    }
}
