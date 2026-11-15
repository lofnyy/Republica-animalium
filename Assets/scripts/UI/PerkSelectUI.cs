using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Seviye atlayınca açılan özellik seçme ekranı.
/// Oyun durur, 3 kart çıkar: 1/2/3, ←/→ + E/Enter, fare ya da gamepad ile seçilir.
/// Özellikler: Güçlü Vuruş (+hasar), Can Yenile (can dolar), Çift Vuruş (her saldırı 2 kez vurur),
/// Dayanıklılık (en fazla can %25 artar). Ara sahne ya da konuşma sırasında açılmaz, bitince açılır.
/// PlayerHealthUI tarafından HUD'a eklenir.
/// </summary>
public class PerkSelectUI : MonoBehaviour
{
    public TMP_FontAsset font;

    struct PerkInfo
    {
        public PlayerCombat.Perk perk;
        public Color color;
    }

    static readonly PerkInfo[] All =
    {
        new PerkInfo { perk = PlayerCombat.Perk.Damage,    color = new Color(0.9f, 0.35f, 0.25f) },
        new PerkInfo { perk = PlayerCombat.Perk.Heal,      color = new Color(0.35f, 0.8f, 0.4f) },
        new PerkInfo { perk = PlayerCombat.Perk.DoubleHit, color = new Color(1f, 0.7f, 0.25f) },
        new PerkInfo { perk = PlayerCombat.Perk.MaxHealth, color = new Color(0.4f, 0.6f, 1f) },
    };

    PlayerCombat _combat;
    DialogueBox _dialogue;
    Canvas _canvas;
    RectTransform _root;
    Image _dim;
    TMP_Text _title, _subtitle, _hint;
    readonly List<RectTransform> _cards = new List<RectTransform>();
    readonly List<Image> _cardFrames = new List<Image>();
    readonly List<PerkInfo> _choices = new List<PerkInfo>();
    int _selected;
    bool _open;
    float _openTime;
    Vector2 _lastMouse;
    bool _stickHeld;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) _combat = p.GetComponent<PlayerCombat>();
        _dialogue = FindFirstObjectByType<DialogueBox>();
        _canvas = GetComponentInParent<Canvas>();
    }

    void OnEnable() => Localization.LanguageChanged += OnLanguageChanged;
    void OnDisable() => Localization.LanguageChanged -= OnLanguageChanged;

    void OnLanguageChanged()
    {
        if (_open) ApplyTexts();
    }

    /// <summary>Açık olan seçim ekranının tüm yazılarını seçili dilde yazar.</summary>
    void ApplyTexts()
    {
        for (int i = 0; i < _cards.Count && i < _choices.Count; i++)
        {
            var c = _choices[i];
            string id = PlayerCombat.PerkId(c.perk);
            var texts = _cards[i].GetComponentsInChildren<TMP_Text>(true);
            texts[0].text = Localization.Get(id + ".title");
            texts[1].text = Localization.Get(id + ".desc");
            texts[2].text = "+ " + _combat.PerkPreview(c.perk);
            texts[3].text = "− " + _combat.PerkDrawback(c.perk);
            texts[4].text = "[" + (i + 1) + "]";
        }
        _title.text = Localization.Get("perk.title");
        _subtitle.text = Localization.Get("perk.subtitle", PlayerProgress.Level);
        if (_hint != null) _hint.text = Localization.Get("perk.hint");
    }

    void Update()
    {
        if (_open) { HandleInput(); Animate(); return; }

        if (PlayerProgress.PendingPicks <= 0 || _combat == null) return;
        if (CutsceneState.IsActive || (_dialogue != null && _dialogue.IsOpen)) return;
        if (_combat.Health == null || _combat.Health.IsDead) return;
        Open();
    }

    // ---------------------------------------------------------------- aç / kapa

    void Open()
    {
        Build();
        _open = true;
        _openTime = 0f;

        // 3 seçenek: alınmış "Çift Vuruş" ve can doluyken "Can Yenile" çıkmaz
        var pool = new List<PerkInfo>();
        foreach (var p in All)
        {
            if (p.perk == PlayerCombat.Perk.DoubleHit && PlayerProgress.DoubleHit) continue;
            if (p.perk == PlayerCombat.Perk.Heal && _combat.Health.Current >= _combat.Health.maxHealth) continue;
            pool.Add(p);
        }
        _choices.Clear();
        while (_choices.Count < 3 && pool.Count > 0)
        {
            int i = Random.Range(0, pool.Count);
            _choices.Add(pool[i]);
            pool.RemoveAt(i);
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            bool on = i < _choices.Count;
            _cards[i].gameObject.SetActive(on);
            if (!on) continue;
            var c = _choices[i];
            _cards[i].Find("Band").GetComponent<Image>().color = c.color;
        }
        // kartları ortala
        float w = 380f, gap = 40f;
        float total = _choices.Count * w + (_choices.Count - 1) * gap;
        for (int i = 0; i < _choices.Count; i++)
            _cards[i].anchoredPosition = new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), -40f);

        ApplyTexts();
        _selected = 0;
        _lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        _root.gameObject.SetActive(true);
        Time.timeScale = 0f;
        CutsceneState.Begin();
        _combat.enabled = false;
        var pc = _combat.GetComponent<PlayerController>();
        if (pc != null) pc.ForcedMoveInput = 0f;
    }

    void Pick(int index)
    {
        if (index < 0 || index >= _choices.Count) return;
        _combat.ApplyPerk(_choices[index].perk);
        PlayerProgress.PendingPicks = Mathf.Max(0, PlayerProgress.PendingPicks - 1);
        FloatingText.Spawn(Localization.Get(PlayerCombat.PerkId(_choices[index].perk) + ".title"), _combat.transform.position + Vector3.up * 2.2f, _choices[index].color, 4f, 1.4f);
        StartCoroutine(Close());
    }

    IEnumerator Close()
    {
        _open = false;
        _root.gameObject.SetActive(false);
        Time.timeScale = 1f;
        // seçme tuşu/tıklaması aynı karede saldırı ya da zıplama olmasın
        yield return null;
        yield return null;
        var pc = _combat.GetComponent<PlayerController>();
        if (pc != null) pc.ForcedMoveInput = null;
        _combat.enabled = true;
        CutsceneState.End();
    }

    // ---------------------------------------------------------------- girdi

    void HandleInput()
    {
        _openTime += Time.unscaledDeltaTime;
        if (_openTime < 0.35f) return; // açılırken yanlışlıkla seçilmesin

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) { Pick(0); return; }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) { Pick(1); return; }
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) { Pick(2); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Move(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Move(1);
            if (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { Pick(_selected); return; }
        }
        if (gp != null)
        {
            float x = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(x) > 0.6f) { if (!_stickHeld) Move(x > 0 ? 1 : -1); _stickHeld = true; }
            else _stickHeld = false;
            if (gp.dpad.left.wasPressedThisFrame) Move(-1);
            if (gp.dpad.right.wasPressedThisFrame) Move(1);
            if (gp.buttonSouth.wasPressedThisFrame) { Pick(_selected); return; }
        }
        if (mouse != null)
        {
            var mp = mouse.position.ReadValue();
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            int over = -1;
            for (int i = 0; i < _choices.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_cards[i], mp, cam)) over = i;
            if (over >= 0 && (mp - _lastMouse).sqrMagnitude > 1f) _selected = over;
            _lastMouse = mp;
            if (over >= 0 && mouse.leftButton.wasPressedThisFrame) { Pick(over); return; }
        }
        // Mobil: dokunmatik ekranda karta dokunma
        if (MobileInput.Available && Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
                for (int i = 0; i < _choices.Count; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_cards[i], touch.position, cam)) { Pick(i); return; }
            }
        }
    }

    void Move(int d)
    {
        if (_choices.Count == 0) return;
        _selected = (_selected + d + _choices.Count) % _choices.Count;
    }

    void Animate()
    {
        float k = Mathf.Clamp01(_openTime / 0.3f);
        float e = k * k * (3f - 2f * k);
        _dim.color = new Color(0f, 0f, 0f, 0.7f * e);
        for (int i = 0; i < _choices.Count; i++)
        {
            bool sel = i == _selected;
            float target = sel ? 1.07f : 1f;
            float s = Mathf.Lerp(_cards[i].localScale.x, target, 12f * Time.unscaledDeltaTime);
            float pop = Mathf.Lerp(0.6f, 1f, e);
            _cards[i].localScale = Vector3.one * (k < 1f ? pop : s);
            _cardFrames[i].color = sel ? new Color(1f, 0.9f, 0.55f, 1f) : new Color(0.35f, 0.3f, 0.25f, 1f);
        }
    }

    // ---------------------------------------------------------------- kurulum

    void Build()
    {
        if (_root != null) return;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        var parent = (RectTransform)(_canvas != null ? _canvas.transform : transform);

        _root = NewRect("PerkSelect", parent);
        _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
        _root.offsetMin = _root.offsetMax = Vector2.zero;
        _root.SetAsLastSibling();

        _dim = NewRect("Dim", _root).gameObject.AddComponent<Image>();
        var dr = (RectTransform)_dim.transform;
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
        _dim.color = new Color(0f, 0f, 0f, 0.7f);
        _dim.raycastTarget = false;

        _title = NewText("Title", _root, 64f, new Color(1f, 0.8f, 0.3f));
        Place((RectTransform)_title.transform, new Vector2(0f, 330f), new Vector2(1400f, 90f));
        _subtitle = NewText("Subtitle", _root, 30f, Color.white);
        Place((RectTransform)_subtitle.transform, new Vector2(0f, 262f), new Vector2(1400f, 50f));

        _hint = NewText("Hint", _root, 22f, new Color(1f, 1f, 1f, 0.7f));
        Place((RectTransform)_hint.transform, new Vector2(0f, -340f), new Vector2(1400f, 40f));
        _hint.text = Localization.Get("perk.hint");

        for (int i = 0; i < 3; i++)
        {
            var card = NewRect("Card" + i, _root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(380f, 440f);
            var frame = card.gameObject.AddComponent<Image>();
            frame.raycastTarget = false;
            _cardFrames.Add(frame);

            var inner = NewRect("Inner", card).gameObject.AddComponent<Image>();
            var ir = (RectTransform)inner.transform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(5f, 5f); ir.offsetMax = new Vector2(-5f, -5f);
            inner.color = new Color(0.13f, 0.11f, 0.1f, 0.97f);
            inner.raycastTarget = false;

            var band = NewRect("Band", card).gameObject.AddComponent<Image>();
            var br = (RectTransform)band.transform;
            br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(5f, -95f); br.offsetMax = new Vector2(-5f, -5f);
            band.raycastTarget = false;

            var t = NewText("Title", card, 36f, Color.white);
            Place((RectTransform)t.transform, new Vector2(0f, 170f), new Vector2(360f, 80f));
            var d = NewText("Desc", card, 26f, new Color(0.9f, 0.88f, 0.82f));
            d.enableWordWrapping = true;
            Place((RectTransform)d.transform, new Vector2(0f, 55f), new Vector2(320f, 110f));
            var v = NewText("Value", card, 26f, new Color(0.55f, 0.95f, 0.5f));
            v.enableWordWrapping = true;
            Place((RectTransform)v.transform, new Vector2(0f, -55f), new Vector2(340f, 70f));
            var dbf = NewText("Drawback", card, 24f, new Color(1f, 0.45f, 0.4f));
            dbf.enableWordWrapping = true;
            Place((RectTransform)dbf.transform, new Vector2(0f, -125f), new Vector2(340f, 70f));
            var k = NewText("Key", card, 26f, new Color(1f, 1f, 1f, 0.6f));
            Place((RectTransform)k.transform, new Vector2(0f, -185f), new Vector2(200f, 40f));

            _cards.Add(card);
        }
        _root.gameObject.SetActive(false);
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    TMP_Text NewText(string name, RectTransform parent, float size, Color color)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }
}
