using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC (gamepad: Start) ile açılan duraklatma menüsü: Devam Et / Bölümü Baştan Başlat / Ana Menü.
/// Oyun başında kendiliğinden oluşur, bölümlerde çalışır (ana menüde çalışmaz).
/// Ara sahne, parşömen ya da özellik seçme ekranı açıkken açılmaz (ESC orada atlama/kapatma işini yapar).
/// </summary>
public class PauseMenu : MonoBehaviour
{
    const string MainMenuScene = "mainMenu";

    static PauseMenu _instance;
    public static bool IsPaused { get; private set; }

    Canvas _canvas;
    RectTransform _root;
    Image _dim;
    const int ButtonCount = 4;
    const int LanguageIndex = 2;
    readonly RectTransform[] _buttons = new RectTransform[ButtonCount];
    readonly Image[] _buttonBg = new Image[ButtonCount];
    readonly TMP_Text[] _labelTexts = new TMP_Text[ButtonCount];
    // 0 devam · 1 baştan başlat · 2 dil (aç/kapa) · 3 ana menü
    readonly string[] _labelKeys = { "pause.resume", "pause.restart", "pause.language", "pause.menu" };
    TMP_Text _title, _hint;
    int _selected;
    float _openedAt;
    Vector2 _lastMouse;
    bool _stickHeld;
    PlayerCombat _combat;
    PlayerController _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (_instance != null) return;
        var go = new GameObject("PauseMenu");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PauseMenu>();
        IsPaused = false;
    }

    void OnEnable() => Localization.LanguageChanged += RefreshTexts;
    void OnDisable() => Localization.LanguageChanged -= RefreshTexts;

    void RefreshTexts()
    {
        if (_root == null) return;
        _title.text = Localization.Get("pause.title");
        for (int i = 0; i < ButtonCount; i++)
            _labelTexts[i].text = i == LanguageIndex
                ? Localization.Get(_labelKeys[i], Localization.NameOf(Localization.Current))
                : Localization.Get(_labelKeys[i]);
        _hint.text = Localization.Get("pause.hint");
    }

    void Awake()
    {
        SceneManager.sceneLoaded += (s, m) => { if (IsPaused) Resume(); };
    }

    void Update()
    {
        if (IsPaused) { HandleInput(); Animate(); return; }

        if (!PausePressed()) return;
        if (SceneManager.GetActiveScene().name == MainMenuScene) return;
        // ara sahnede ESC atlama/kapatma işini yapar; aynı karede biten ara sahne de menüyü açmasın
        if (CutsceneState.IsActive || _cutsceneLastFrame) return;
        if (FindFirstObjectByType<PlayerController>() == null) return;
        Pause();
    }

    bool _cutsceneLastFrame;

    void LateUpdate()
    {
        if (!IsPaused) _cutsceneLastFrame = CutsceneState.IsActive;
    }

    static bool PausePressed()
    {
        if (MobileInput.PausePressed) return true;
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        return (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
    }

    // ---------------------------------------------------------------- aç / kapa

    void Pause()
    {
        Build();
        IsPaused = true;
        _openedAt = Time.unscaledTime;
        _selected = 0;
        _lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        _root.gameObject.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        CutsceneState.Begin();
        _combat = FindFirstObjectByType<PlayerCombat>();
        _player = FindFirstObjectByType<PlayerController>();
        if (_combat != null) _combat.enabled = false;
        if (_player != null) _player.ForcedMoveInput = 0f;
    }

    void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        if (_root != null) _root.gameObject.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        CutsceneState.End();
        // menüdeki tıklama/tuş aynı karede saldırı ya da zıplama olmasın
        StartCoroutine(GiveBackControl(_combat, _player));
    }

    System.Collections.IEnumerator GiveBackControl(PlayerCombat combat, PlayerController player)
    {
        yield return null;
        yield return null;
        if (player != null) player.ForcedMoveInput = null;
        if (combat != null) combat.enabled = true;
    }

    void Choose(int index)
    {
        switch (index)
        {
            case 0:
                Resume();
                break;
            case 1:
                Resume();
                CheckpointState.Clear();            // bölümün en başından
                CutsceneState.SkipIntroOnce = true; // açılış ara sahnesi tekrar oynamasın
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                break;
            case LanguageIndex:
                Localization.Toggle(); // menü açık kalır; yazılar anında yenilenir
                break;
            case 3:
                Resume();
                CheckpointState.Clear();
                if (GameManager.Instance != null) SceneManager.LoadScene(GameManager.Instance.mainMenuSceneName);
                else SceneManager.LoadScene(MainMenuScene);
                break;
        }
    }

    // ---------------------------------------------------------------- girdi

    void HandleInput()
    {
        if (Time.unscaledTime - _openedAt < 0.15f) return;

        if (PausePressed()) { Resume(); return; }

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) { Choose(_selected); return; }
        }
        if (gp != null)
        {
            float y = gp.leftStick.y.ReadValue();
            if (Mathf.Abs(y) > 0.6f) { if (!_stickHeld) Move(y > 0 ? -1 : 1); _stickHeld = true; }
            else _stickHeld = false;
            if (gp.dpad.up.wasPressedThisFrame) Move(-1);
            if (gp.dpad.down.wasPressedThisFrame) Move(1);
            if (gp.buttonSouth.wasPressedThisFrame) { Choose(_selected); return; }
            if (gp.buttonEast.wasPressedThisFrame) { Resume(); return; }
        }
        if (mouse != null)
        {
            var mp = mouse.position.ReadValue();
            int over = -1;
            for (int i = 0; i < _buttons.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_buttons[i], mp, null)) over = i;
            if (over >= 0 && (mp - _lastMouse).sqrMagnitude > 1f) _selected = over;
            _lastMouse = mp;
            if (over >= 0 && mouse.leftButton.wasPressedThisFrame) { Choose(over); return; }
        }
        // Mobil: dokunmatik ekranda butona dokunma
        if (MobileInput.Available && Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                for (int i = 0; i < _buttons.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_buttons[i], touch.position, null)) { Choose(i); return; }
            }
        }
    }

    void Move(int d) => _selected = (_selected + d + _buttons.Length) % _buttons.Length;

    void Animate()
    {
        float k = Mathf.Clamp01((Time.unscaledTime - _openedAt) / 0.2f);
        _dim.color = new Color(0f, 0f, 0f, 0.65f * k);
        for (int i = 0; i < _buttons.Length; i++)
        {
            bool sel = i == _selected;
            _buttonBg[i].color = sel ? new Color(0.85f, 0.65f, 0.3f, 0.95f) : new Color(0.15f, 0.13f, 0.12f, 0.92f);
            float s = Mathf.Lerp(_buttons[i].localScale.x, sel ? 1.06f : 1f, 15f * Time.unscaledDeltaTime);
            _buttons[i].localScale = Vector3.one * s;
        }
    }

    // ---------------------------------------------------------------- kurulum

    void Build()
    {
        if (_root != null) return;

        var cgo = new GameObject("PauseCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000; // her şeyin üstünde
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // sahnedeki yazı tipini kullan (Türkçe karakterler için)
        TMP_FontAsset font = null;
        var anyText = FindFirstObjectByType<TMP_Text>();
        if (anyText != null) font = anyText.font;

        _root = NewRect("Root", (RectTransform)cgo.transform);
        Stretch(_root);
        _dim = _root.gameObject.AddComponent<Image>();
        _dim.raycastTarget = false;

        _title = NewText("Title", _root, font, 72f, new Color(1f, 0.85f, 0.45f));
        Place((RectTransform)_title.transform, new Vector2(0f, 260f), new Vector2(1200f, 100f));

        for (int i = 0; i < _buttons.Length; i++)
        {
            var b = NewRect("Button" + i, _root);
            Place(b, new Vector2(0f, 110f - i * 105f), new Vector2(560f, 86f));
            _buttonBg[i] = b.gameObject.AddComponent<Image>();
            _buttonBg[i].raycastTarget = false;
            var t = NewText("Label", b, font, 36f, Color.white);
            Stretch((RectTransform)t.transform);
            _labelTexts[i] = t;
            _buttons[i] = b;
        }

        _hint = NewText("Hint", _root, font, 24f, new Color(1f, 1f, 1f, 0.6f));
        Place((RectTransform)_hint.transform, new Vector2(0f, -350f), new Vector2(1400f, 40f));

        RefreshTexts();
        _root.gameObject.SetActive(false);
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
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static TMP_Text NewText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color)
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
