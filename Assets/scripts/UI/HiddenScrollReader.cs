using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gizli yazılı parşömen ekranı (Level 2, Sahne 6).
///   1) Boş parşömen açılır, Kirpi "Bu şey de ne?" der
///   2) Köşede gözlük ikonu: G (gamepad Y) ile gözlük merceğe dönüşür
///   3) Mercek fare / yön tuşları / sol çubukla gezdirilir; yazı yalnızca merceğin altında görünür
///   4) Yazının çoğu tarandığında (ya da süre dolunca) yazı tamamen belirir; E / Boşluk ile kapanır
/// Arayüz ilk kullanımda koddan kurulur. Ayrıca ekranın altında kısa "eşya alındı" bildirimi gösterebilir.
/// </summary>
public class HiddenScrollReader : MonoBehaviour
{
    [Header("Görseller")]
    public Sprite blankScroll;
    [Tooltip("Türkçe yazılı parşömen (çizim)")]
    public Sprite writtenScroll;
    [Tooltip("İngilizce yazılı parşömen (strings.json'daki metinden üretilen çizim)")]
    public Sprite writtenScrollEn;
    public Sprite monocleIcon;
    public TMP_FontAsset font;

    [Header("Boyut (1920x1080 referans)")]
    public float paperHeight = 820f;
    public float paperOffsetY = 90f;
    public float lensDiameter = 270f;
    public float lensSpeed = 750f;

    [Header("Yazı alanı (parşömen görselinde, sol üstten 0..1)")]
    public Rect textArea = new Rect(0.29f, 0.23f, 0.5f, 0.59f);
    [Range(0f, 1f)] public float revealAtCoverage = 0.7f;
    public float revealAfterSeconds = 12f;

    public bool IsOpen { get; private set; }

    bool _simPress;
    /// <summary>Test için: bir sonraki "G" / "kapat" tuşu basılmış sayılır.</summary>
    public void SimulatePress() => _simPress = true;

    Canvas _canvas;
    RectTransform _root, _paper, _lens, _lensWritten, _icon, _toastRoot;
    Image _dim, _full, _lensTint, _toastIcon, _lensInner;
    CanvasGroup _iconGroup, _hintGroup, _lensGroup, _toastGroup;
    TMP_Text _iconText, _hint, _toastText;
    Coroutine _toast;

    // ---------------------------------------------------------------- oynatma

    /// <summary>Sahne 6'nın tamamı. dialogue: Kirpi'nin repliği için.</summary>
    public IEnumerator Play(DialogueBox dialogue)
    {
        Build();
        IsOpen = true;
        _root.gameObject.SetActive(true);
        ApplyLanguage();
        _full.color = new Color(1f, 1f, 1f, 0f);
        _lens.gameObject.SetActive(false);
        _icon.gameObject.SetActive(false);
        _hintGroup.alpha = 0f;

        // açılış
        yield return Animate(0.35f, k =>
        {
            _dim.color = new Color(0f, 0f, 0f, 0.75f * k);
            _paper.localScale = new Vector3(Mathf.Lerp(0.85f, 1f, k), Mathf.Max(0.02f, k), 1f);
        });

        yield return Wait(0.4f);
        if (dialogue != null) yield return dialogue.PlayKeys("speaker.hedgehog", "act2.scroll.hedgehog_01");

        // gözlük ikonu
        _icon.gameObject.SetActive(true);
        float t = 0f;
        while (!LensPressed())
        {
            t += Time.unscaledDeltaTime;
            _iconGroup.alpha = 0.65f + 0.35f * Mathf.Sin(t * 5f);
            yield return null;
        }
        _iconGroup.alpha = 1f;

        // ikon merceğe dönüşüp ortaya uçar
        Vector2 from = _paper.InverseTransformPoint(_icon.position);
        _lens.gameObject.SetActive(true);
        Vector2 lensPos = Vector2.zero;
        yield return Animate(0.4f, k =>
        {
            float e = k * k * (3f - 2f * k);
            SetLens(Vector2.Lerp(from, Vector2.zero, e));
            _lens.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, e);
            _iconGroup.alpha = 1f - k;
        });
        _icon.gameObject.SetActive(false);
        _lensGroup.alpha = 1f;

        // merceği gezdir
        var cells = BuildCells(out int total);
        int covered = 0;
        float used = 0f;
        Vector2 lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        while (covered < total * revealAtCoverage && used < revealAfterSeconds)
        {
            float dt = Time.unscaledDeltaTime;
            used += dt;
            _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, 1f, dt * 2f);

            Vector2 move = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            }
            var gp = Gamepad.current;
            if (gp != null) move += gp.leftStick.ReadValue();
            if (move.sqrMagnitude > 1f) move.Normalize();
            lensPos += move * lensSpeed * dt;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var mp = mouse.position.ReadValue();
                if ((mp - lastMouse).sqrMagnitude > 1f &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_paper, mp, EventCamera(), out var local))
                    lensPos = local;
                lastMouse = mp;
            }

            var half = _paper.rect.size * 0.5f;
            lensPos = new Vector2(Mathf.Clamp(lensPos.x, -half.x, half.x), Mathf.Clamp(lensPos.y, -half.y, half.y));
            SetLens(lensPos);

            float r = lensDiameter * 0.38f;
            for (int i = 0; i < cells.Length; i++)
                if (!float.IsNaN(cells[i].x) && (cells[i] - lensPos).sqrMagnitude < r * r)
                {
                    cells[i] = new Vector2(float.NaN, float.NaN);
                    covered++;
                }
            yield return null;
        }

        // yazı tamamen belirir
        _hintGroup.alpha = 0f;
        yield return Animate(1.4f, k =>
        {
            _full.color = new Color(1f, 1f, 1f, k);
            _lens.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, k);
            _lensGroup.alpha = 1f - k;
        });
        _lens.gameObject.SetActive(false);

        yield return Wait(0.6f);
        while (!ClosePressed()) yield return null;

        yield return Animate(0.3f, k =>
        {
            _dim.color = new Color(0f, 0f, 0f, 0.75f * (1f - k));
            _paper.localScale = new Vector3(1f, Mathf.Max(0.02f, 1f - k), 1f);
        });
        _root.gameObject.SetActive(false);
        IsOpen = false;
    }

    /// <summary>Ekranın altında kısa "eşya alındı" bildirimi.</summary>
    /// <summary>textOrKey: Localization kimliği ("item.monocle") ya da düz metin.</summary>
    public void ShowItem(Sprite icon, string textOrKey)
    {
        Build();
        string text = Localization.Has(textOrKey) ? Localization.Get(textOrKey) : textOrKey;
        if (_toast != null) StopCoroutine(_toast);
        _toast = StartCoroutine(ToastRoutine(icon, text));
    }

    IEnumerator ToastRoutine(Sprite icon, string text)
    {
        _toastRoot.gameObject.SetActive(true);
        _toastIcon.sprite = icon;
        _toastIcon.enabled = icon != null;
        _toastText.text = text;
        var basePos = new Vector2(0f, 150f);
        yield return Animate(0.25f, k =>
        {
            _toastGroup.alpha = k;
            _toastRoot.anchoredPosition = basePos + new Vector2(0f, -30f * (1f - k));
        });
        yield return Wait(2f);
        yield return Animate(0.4f, k => _toastGroup.alpha = 1f - k);
        _toastRoot.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- dil

    void OnEnable() => Localization.LanguageChanged += ApplyLanguage;
    void OnDisable() => Localization.LanguageChanged -= ApplyLanguage;

    /// <summary>Seçili dile göre yazılı parşömen görselini ve yazıları ayarlar.</summary>
    void ApplyLanguage()
    {
        if (_root == null) return;
        var sprite = Localization.Current == Language.English && writtenScrollEn != null ? writtenScrollEn : writtenScroll;
        if (_full != null) _full.sprite = sprite;
        if (_lensInner != null) _lensInner.sprite = sprite;
        if (_iconText != null) _iconText.text = Localization.Get("ui.lens_key");
        if (_hint != null) _hint.text = Localization.Get("ui.lens_hint");
    }

    // ---------------------------------------------------------------- yardımcılar

    void SetLens(Vector2 pos)
    {
        _lens.localPosition = pos;
        _lensWritten.localPosition = -pos; // yazı parşömenle hizalı kalsın
    }

    Vector2[] BuildCells(out int total)
    {
        const int cols = 5, rows = 9;
        var size = _paper.rect.size;
        var cells = new Vector2[cols * rows];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                float u = textArea.x + textArea.width * (x + 0.5f) / cols;
                float v = textArea.y + textArea.height * (y + 0.5f) / rows;
                cells[y * cols + x] = new Vector2((u - 0.5f) * size.x, (0.5f - v) * size.y);
            }
        total = cells.Length;
        return cells;
    }

    Camera EventCamera() =>
        _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    bool LensPressed()
    {
        if (_simPress) { _simPress = false; return true; }
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        return (kb != null && kb.gKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
    }

    bool ClosePressed()
    {
        if (_simPress) { _simPress = false; return true; }
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        return (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                               kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame));
    }

    static IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
    }

    static IEnumerator Animate(float time, System.Action<float> step)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(t / time));
            yield return null;
        }
        step(1f);
    }

    // ---------------------------------------------------------------- kurulum

    void Build()
    {
        if (_root != null) return;
        _canvas = GetComponentInParent<Canvas>();
        var parent = (RectTransform)(_canvas != null ? _canvas.transform : transform);

        _root = NewRect("HiddenScroll", parent);
        Stretch(_root);
        // Konuşma kutusu bunun üstünde kalsın
        var dlg = FindFirstObjectByType<DialogueBox>();
        if (dlg != null && dlg.panel != null && dlg.panel.transform.parent == parent)
            _root.SetSiblingIndex(dlg.panel.transform.GetSiblingIndex());

        _dim = NewImage("Dim", _root, null, Color.black);
        Stretch((RectTransform)_dim.transform);

        float aspect = blankScroll != null ? blankScroll.rect.width / blankScroll.rect.height : 1f;
        var paperImg = NewImage("Paper", _root, blankScroll, Color.white);
        _paper = (RectTransform)paperImg.transform;
        _paper.anchorMin = _paper.anchorMax = _paper.pivot = new Vector2(0.5f, 0.5f);
        _paper.sizeDelta = new Vector2(paperHeight * aspect, paperHeight);
        _paper.anchoredPosition = new Vector2(0f, paperOffsetY);

        // Tam yazı (sonunda belirir)
        _full = NewImage("Written", _paper, writtenScroll, new Color(1f, 1f, 1f, 0f));
        Stretch((RectTransform)_full.transform);

        // Mercek: daire maske + içinde yazılı parşömen + çerçeve
        var circle = MakeCircleSprite(256, false);
        var lensImg = NewImage("Lens", _paper, circle, Color.white);
        _lens = (RectTransform)lensImg.transform;
        _lens.sizeDelta = Vector2.one * lensDiameter;
        var mask = lensImg.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        _lensGroup = lensImg.gameObject.AddComponent<CanvasGroup>();
        var inner = NewImage("WrittenInLens", _lens, writtenScroll, Color.white);
        _lensInner = inner;
        _lensWritten = (RectTransform)inner.transform;
        _lensWritten.sizeDelta = _paper.sizeDelta;
        _lensTint = NewImage("Glass", _lens, circle, new Color(0.75f, 0.85f, 0.95f, 0.12f));
        Stretch((RectTransform)_lensTint.transform);
        var ring = NewImage("Ring", _lens, MakeCircleSprite(256, true), new Color(0.11f, 0.09f, 0.09f, 1f));
        Stretch((RectTransform)ring.transform);
        ring.maskable = false;
        var ringRt = (RectTransform)ring.transform;
        ringRt.offsetMin = Vector2.one * -14f;
        ringRt.offsetMax = Vector2.one * 14f;

        // Gözlük ikonu (sağ alt)
        var iconImg = NewImage("MonocleIcon", _paper, monocleIcon, Color.white);
        _icon = (RectTransform)iconImg.transform;
        _icon.anchorMin = _icon.anchorMax = new Vector2(1f, 0f);
        _icon.pivot = new Vector2(0.5f, 0.5f);
        _icon.sizeDelta = new Vector2(110f, 138f);
        _icon.anchoredPosition = new Vector2(40f, 120f);
        _iconGroup = iconImg.gameObject.AddComponent<CanvasGroup>();
        _iconText = NewText("Key", _icon, 30f);
        var kt = (RectTransform)_iconText.transform;
        kt.anchorMin = kt.anchorMax = new Vector2(0.5f, 0f);
        kt.pivot = new Vector2(0.5f, 1f);
        kt.sizeDelta = new Vector2(260f, 44f);
        kt.anchoredPosition = new Vector2(0f, -6f);
        _iconText.text = Localization.Get("ui.lens_key");

        // Kullanım ipucu (parşömenin altı)
        _hint = NewText("Hint", _root, 26f);
        var hr = (RectTransform)_hint.transform;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0.5f);
        hr.sizeDelta = new Vector2(1200f, 40f);
        hr.anchoredPosition = new Vector2(0f, paperOffsetY - paperHeight * 0.5f - 18f);
        _hint.text = Localization.Get("ui.lens_hint");
        _hint.color = new Color(1f, 1f, 1f, 0.8f);
        _hintGroup = _hint.gameObject.AddComponent<CanvasGroup>();

        _root.gameObject.SetActive(false);

        // Eşya bildirimi
        _toastRoot = NewRect("ItemToast", parent);
        _toastRoot.anchorMin = _toastRoot.anchorMax = _toastRoot.pivot = new Vector2(0.5f, 0f);
        _toastRoot.sizeDelta = new Vector2(520f, 90f);
        var bg = _toastRoot.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        bg.raycastTarget = false;
        _toastGroup = _toastRoot.gameObject.AddComponent<CanvasGroup>();
        _toastIcon = NewImage("Icon", _toastRoot, null, Color.white);
        var ti = (RectTransform)_toastIcon.transform;
        ti.anchorMin = ti.anchorMax = new Vector2(0f, 0.5f);
        ti.pivot = new Vector2(0f, 0.5f);
        ti.sizeDelta = new Vector2(58f, 72f);
        ti.anchoredPosition = new Vector2(22f, 0f);
        _toastIcon.preserveAspect = true;
        _toastText = NewText("Text", _toastRoot, 32f);
        var tt = (RectTransform)_toastText.transform;
        tt.anchorMin = new Vector2(0f, 0f); tt.anchorMax = new Vector2(1f, 1f);
        tt.offsetMin = new Vector2(96f, 0f); tt.offsetMax = new Vector2(-20f, 0f);
        _toastText.alignment = TextAlignmentOptions.MidlineLeft;
        _toastRoot.gameObject.SetActive(false);
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text NewText(string name, RectTransform parent, float size)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    /// <summary>Dolu daire ya da halka sprite'ı (kenarları yumuşak).</summary>
    static Sprite MakeCircleSprite(int size, bool ring)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f, r = size * 0.5f - 1f, inner = r * 0.86f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d);
                if (ring) a *= Mathf.Clamp01(d - inner);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
