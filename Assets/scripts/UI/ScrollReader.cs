using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ekranın ortasında açılan büyük parşömen.
/// Open() ile açılır; açıkken oyun durur (cutscene gibi), E / Boşluk / Esc / Enter ile kapanır.
/// </summary>
public class ScrollReader : MonoBehaviour
{
    [Header("Görseller")]
    [Tooltip("Açıkken görünen kök obje")]
    public GameObject root;
    [Tooltip("Arkadaki karartma")]
    public Image dim;
    [Tooltip("Açılırken aşağı doğru açılan kâğıt")]
    public RectTransform paper;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text closeHint;

    [Header("Oyuncu")]
    public PlayerController player;
    public PlayerCombat playerCombat;

    [Header("Animasyon")]
    public float openTime = 0.35f;
    [Range(0f, 1f)] public float dimAlpha = 0.7f;

    public bool IsOpen { get; private set; }
    /// <summary>Kapandığı kare (aynı tuşla hemen tekrar açılmasın diye).</summary>
    public int LastClosedFrame { get; private set; } = -1;

    Action _onClosed;
    int _openFrame;
    float _t;
    string _title;
    string _body;
    string _titleKey;
    string _bodyKey;

    void Awake()
    {
        LocalizedText.Bind(closeHint, "ui.close");
        if (root != null) root.SetActive(false);
    }

    void OnEnable() => Localization.LanguageChanged += RefreshTexts;

    void OnDisable() => Localization.LanguageChanged -= RefreshTexts;

    public void Open(string title, string body, Action onClosed = null)
    {
        if (IsOpen) return;
        IsOpen = true;
        _onClosed = onClosed;
        _openFrame = Time.frameCount;
        _t = 0f;

        _titleKey = Localization.Has(title) ? title : null;
        _bodyKey = Localization.Has(body) ? body : null;
        _title = title;
        _body = body;
        EnsureTextFields();
        RefreshTexts();

        CutsceneState.Begin();
        if (playerCombat != null) playerCombat.enabled = false;
        if (player != null) player.ForcedMoveInput = 0f;

        if (root != null) root.SetActive(true);
        Apply(0f);
    }

    void RefreshTexts()
    {
        if (!IsOpen && _titleKey == null && _bodyKey == null) return;
        if (titleText != null) titleText.text = _titleKey != null ? Localization.Get(_titleKey) : _title;
        if (bodyText != null) bodyText.text = _bodyKey != null ? Localization.Get(_bodyKey) : _body;
    }

    void EnsureTextFields()
    {
        if (paper == null || (titleText != null && bodyText != null)) return;

        var referenceText = FindFirstObjectByType<TMP_Text>();
        var font = referenceText != null ? referenceText.font : null;
        if (titleText == null)
            titleText = CreateText("ScrollTitle", font, 38f, new Vector2(0f, -70f), new Vector2(900f, 70f));
        if (bodyText == null)
            bodyText = CreateText("ScrollBody", font, 27f, new Vector2(0f, -160f), new Vector2(900f, 720f));
    }

    TMP_Text CreateText(string objectName, TMP_FontAsset font, float size, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(paper, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.18f, 0.14f, 0.1f, 1f);
        text.alignment = TextAlignmentOptions.Top;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    void Update()
    {
        if (!IsOpen) return;
        _t += Time.unscaledDeltaTime;
        Apply(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / openTime)));

        if (Time.frameCount > _openFrame && _t > openTime * 0.8f && ClosePressed())
            Close();
    }

    bool ClosePressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                           kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            return true;
        var gp = Gamepad.current;
        return gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame);
    }

    void Apply(float k)
    {
        if (dim != null) { var c = dim.color; c.a = dimAlpha * k; dim.color = c; }
        if (paper != null) paper.localScale = new Vector3(Mathf.Lerp(0.85f, 1f, k), Mathf.Max(0.02f, k), 1f);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        LastClosedFrame = Time.frameCount;
        if (root != null) root.SetActive(false);

        if (player != null) player.ForcedMoveInput = null;
        if (playerCombat != null) playerCombat.enabled = true;
        CutsceneState.End();

        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }
}
