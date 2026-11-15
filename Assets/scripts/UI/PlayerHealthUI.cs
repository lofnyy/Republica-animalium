using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ekranın köşesinde oyuncunun can barı (yüzdelik), seviyesi ve XP barı.
/// Barlar oyun başında koddan kurulur; eski "label" yazısının yerine geçer.
/// Seviye atlayınca ekranın ortasında "SEVİYE ATLADIN!" yazısı çıkar.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    public Health health;
    [Tooltip("Eski can yazısı: barlar bunun yerine konur, kendisi gizlenir")]
    public TMP_Text label;

    [Header("Boyut")]
    public Vector2 barSize = new Vector2(420f, 34f);
    public float xpBarHeight = 18f;

    [Header("Renkler")]
    public Color healthColor = new Color(0.85f, 0.2f, 0.22f);
    public Color healthLowColor = new Color(1f, 0.45f, 0.2f);
    public Color trailColor = new Color(0.55f, 0.12f, 0.12f, 1f);
    public Color xpColor = new Color(1f, 0.78f, 0.25f);
    public Color backColor = new Color(0f, 0f, 0f, 0.65f);
    [Range(0f, 1f)] public float lowPercent = 0.3f;

    RectTransform _hpFill, _hpTrail, _xpFill, _hpBack;
    Image _hpFillImage;
    TMP_Text _hpText, _xpText, _levelText, _toast;
    float _hpTarget = 1f, _hpTrailValue = 1f, _xpTarget, _xpShown;
    float _toastTimer;
    PlayerCombat _combat;

    const float ToastTime = 2.2f;

    void Start()
    {
        if (health == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) health = p.GetComponent<Health>();
        }
        if (health != null) _combat = health.GetComponent<PlayerCombat>();

        Build();

        // Seviye atlayınca açılan özellik seçme ekranı
        var perks = GetComponent<PerkSelectUI>();
        if (perks == null) perks = gameObject.AddComponent<PerkSelectUI>();
        if (perks.font == null && label != null) perks.font = label.font;

        if (health != null)
        {
            health.Changed += Refresh;
            Refresh(health.Current, health.maxHealth);
            _hpTrailValue = _hpTarget;
        }
        PlayerProgress.Changed += RefreshXp;
        PlayerProgress.LeveledUp += OnLeveledUp;
        Localization.LanguageChanged += OnLanguageChanged;
        RefreshXp(PlayerProgress.Xp, PlayerProgress.XpToNext, PlayerProgress.Level);
        _xpShown = _xpTarget;
    }

    void OnDestroy()
    {
        if (health != null) health.Changed -= Refresh;
        PlayerProgress.Changed -= RefreshXp;
        PlayerProgress.LeveledUp -= OnLeveledUp;
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged()
    {
        if (health != null) Refresh(health.Current, health.maxHealth);
        RefreshXp(PlayerProgress.Xp, PlayerProgress.XpToNext, PlayerProgress.Level);
    }

    // ---------------------------------------------------------------- kurulum

    void Build()
    {
        // Barlar eski yazının içine konur: cutscene'ler o objeyi gizleyip açınca barlar da onunla gizlenir.
        // Yazının kendisi kapatılır (objesi değil).
        RectTransform parent = label != null ? (RectTransform)label.transform : (RectTransform)transform;
        TMP_FontAsset font = label != null ? label.font : null;

        var root = NewRect("PlayerStats", parent);
        if (label != null)
        {
            label.text = string.Empty;   // eski "CAN 5 / 5" yazısı (ve alt mesh'leri) tamamen gitsin
            label.enabled = false;
            foreach (var sub in label.GetComponentsInChildren<TMP_SubMeshUI>(true)) sub.gameObject.SetActive(false);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero; // yazının sol altı
            root.anchoredPosition = Vector2.zero;
        }
        else
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(30f, -30f);
        }
        float levelW = 84f;
        root.sizeDelta = new Vector2(levelW + 8f + barSize.x, barSize.y + 6f + xpBarHeight);

        // Seviye rozeti (solda)
        var badge = NewRect("Level", root);
        Pin(badge, 0f, 0f, levelW, root.sizeDelta.y);
        badge.gameObject.AddComponent<Image>().color = backColor;
        _levelText = NewText("Text", badge, font, 26f, TextAlignmentOptions.Center);
        Stretch((RectTransform)_levelText.transform);

        float x = levelW + 8f;

        // Can barı
        var hpBack = NewRect("HealthBack", root);
        _hpBack = hpBack;
        Pin(hpBack, x, 0f, barSize.x, barSize.y);
        hpBack.gameObject.AddComponent<Image>().color = backColor;
        _hpTrail = NewFill("Trail", hpBack, trailColor, 3f);
        _hpFill = NewFill("Fill", hpBack, healthColor, 3f);
        _hpFillImage = _hpFill.GetComponent<Image>();
        _hpText = NewText("Text", hpBack, font, 22f, TextAlignmentOptions.Center);
        Stretch((RectTransform)_hpText.transform);

        // XP barı
        var xpBack = NewRect("XpBack", root);
        Pin(xpBack, x, barSize.y + 6f, barSize.x, xpBarHeight);
        xpBack.gameObject.AddComponent<Image>().color = backColor;
        _xpFill = NewFill("Fill", xpBack, xpColor, 2f);
        _xpText = NewText("Text", xpBack, font, 15f, TextAlignmentOptions.Center);
        Stretch((RectTransform)_xpText.transform);

        // Seviye atlama yazısı (ekran ortası)
        var canvas = GetComponentInParent<Canvas>();
        var toastParent = canvas != null ? (RectTransform)canvas.transform : parent;
        _toast = NewText("LevelUpToast", toastParent, font, 56f, TextAlignmentOptions.Center);
        var tr = (RectTransform)_toast.transform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0f, 140f);
        tr.sizeDelta = new Vector2(1200f, 160f);
        _toast.color = xpColor;
        _toast.gameObject.SetActive(false);
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Sol üst köşeye göre piksel konum/boyut.</summary>
    static void Pin(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform NewFill(string name, RectTransform parent, Color color, float pad)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        rt.gameObject.AddComponent<Image>().color = color;
        return rt;
    }

    static TMP_Text NewText(string name, RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    // ---------------------------------------------------------------- güncelleme

    void Refresh(int current, int max)
    {
        _hpTarget = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (_combat != null && _combat.BaseMaxHealth > 0)
        {
            // Özelliksiz en fazla can = 100; "Dayanıklılık" alındıkça bar uzar (100 → 125 …)
            _hpText.text = Localization.Get("hud.health_pct", Mathf.RoundToInt(_hpTarget * 100f));
            float grow = Mathf.Clamp((float)max / _combat.BaseMaxHealth, 1f, 1.75f);
            if (_hpBack != null) _hpBack.sizeDelta = new Vector2(barSize.x * grow, barSize.y);
        }
        else _hpText.text = Localization.Get("hud.health_pct", Mathf.RoundToInt(_hpTarget * 100f));
        _hpFillImage.color = _hpTarget <= lowPercent ? healthLowColor : healthColor;
        SetFill(_hpFill, _hpTarget);
        if (_hpTarget > _hpTrailValue) _hpTrailValue = _hpTarget;
    }

    void RefreshXp(int xp, int toNext, int level)
    {
        _xpTarget = toNext > 0 ? Mathf.Clamp01((float)xp / toNext) : 0f;
        _xpText.text = xp + " / " + toNext + " XP";
        _levelText.text = "<size=50%>" + Localization.Get("hud.level") + "</size>\n" + level;
    }

    void OnLeveledUp(int level)
    {
        _xpShown = 0f; // bar yeniden dolmaya başlasın
        // "Seviye atladın" yazısını artık özellik seçme ekranı (PerkSelectUI) gösteriyor
    }

    static void SetFill(RectTransform fill, float f)
    {
        fill.anchorMax = new Vector2(Mathf.Max(0f, f), 1f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Hasar alınca açık renkli iz yavaşça iner
        if (_hpTrail != null)
        {
            _hpTrailValue = Mathf.MoveTowards(_hpTrailValue, _hpTarget, 0.5f * dt);
            SetFill(_hpTrail, _hpTrailValue);
        }

        if (_xpFill != null)
        {
            _xpShown = Mathf.MoveTowards(_xpShown, _xpTarget, 1.2f * dt);
            SetFill(_xpFill, _xpShown);
        }

        if (_toast != null && _toastTimer > 0f)
        {
            _toastTimer -= dt;
            float k = 1f - _toastTimer / ToastTime;
            var c = _toast.color;
            c.a = k < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.75f) / 0.25f);
            _toast.color = c;
            _toast.transform.localScale = Vector3.one * (k < 0.08f ? Mathf.Lerp(1.5f, 1f, k / 0.08f) : 1f);
            if (_toastTimer <= 0f) _toast.gameObject.SetActive(false);
        }
    }
}
