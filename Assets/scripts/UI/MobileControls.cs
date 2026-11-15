using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mobil platformlarda ekranın altına sanal joystick ve aksiyon butonları koyar.
/// DontDestroyOnLoad singleton; sadece mobil cihazlarda (veya editörde "Simulate Touch"
/// açıkken) görünür.
///
/// Sol taraf : joystick (hareket)
/// Sağ taraf : Zıpla (büyük), Saldır (orta), Dash (küçük)
/// Üst sağ   : Duraklat butonu (≡)
/// </summary>
public class MobileControls : MonoBehaviour
{
    const string MainMenuScene = "mainMenu";

    static MobileControls _instance;

    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _root;

    // Joystick
    RectTransform _joyBase, _joyKnob;
    int _joyFingerId = -1;
    Vector2 _joyOrigin;
    float _joyRadius = 120f;

    // Butonlar
    MobileButton _jumpBtn, _attackBtn, _dashBtn, _pauseBtn;

    bool _wasMainMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (_instance != null) return;
        if (!IsMobilePlatform()) return;

        // EventSystem yoksa oluştur
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        var go = new GameObject("MobileControls");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MobileControls>();
        MobileInput.Available = true;
    }

    static bool IsMobilePlatform()
    {
#if UNITY_EDITOR
        // Editörde sadece Android/iOS build target seçiliyse göster
        return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android
            || UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS;
#elif UNITY_ANDROID || UNITY_IOS
        return true;
#else
        return false;
#endif
    }

    void Awake()
    {
        Build();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _joyFingerId = -1;
        MobileInput.MoveX = 0f;
        MobileInput.JumpHeld = false;
        MobileInput.LateReset();
        RefreshVisibility();
    }

    void RefreshVisibility()
    {
        bool isMenu = SceneManager.GetActiveScene().name == MainMenuScene;
        _wasMainMenu = isMenu;
        if (_group != null) _group.alpha = isMenu ? 0f : 1f;
        if (_group != null) _group.blocksRaycasts = !isMenu;
    }

    void Update()
    {
        // Ana menüdeyken gizle
        bool isMenu = SceneManager.GetActiveScene().name == MainMenuScene;
        if (isMenu != _wasMainMenu) RefreshVisibility();

        if (isMenu) return;

        // Cutscene/diyalog sırasında kontrol butonlarını gizle, sadece tap-advance çalışsın
        bool inCutscene = CutsceneState.IsActive;
        SetControlsVisible(!inCutscene);

        if (!inCutscene)
        {
            HandleJoystick();
            HandleButtons();
        }
        else
        {
            // Cutscene sırasında sadece tap-advance
            ReleaseJoystick();
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                { MobileInput.TapAdvance = true; break; }
        }
    }

    void SetControlsVisible(bool visible)
    {
        if (_jumpBtn != null && _jumpBtn.gameObject.activeSelf != visible)
        {
            _jumpBtn.gameObject.SetActive(visible);
            _attackBtn.gameObject.SetActive(visible);
            _dashBtn.gameObject.SetActive(visible);
        }
    }

    void LateUpdate()
    {
        MobileInput.LateReset();
    }

    // ---------------------------------------------------------------- Joystick

    void HandleJoystick()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        // EnhancedTouch kullanmak yerine eski Input API ile de çalışabilsin
        int touchCount = Input.touchCount;

        // Aktif parmağı takip et
        if (_joyFingerId >= 0)
        {
            bool found = false;
            for (int i = 0; i < touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.fingerId != _joyFingerId) continue;
                found = true;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    ReleaseJoystick();
                    break;
                }
                UpdateJoystick(t.position);
                break;
            }
            if (!found) ReleaseJoystick();
            return;
        }

        // Yeni dokunuş: ekranın sol yarısında mı?
        for (int i = 0; i < touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase != TouchPhase.Began) continue;
            if (t.position.x > Screen.width * 0.4f) continue;
            if (IsOverUI(t.position)) continue;

            _joyFingerId = t.fingerId;
            _joyOrigin = t.position;
            _joyBase.gameObject.SetActive(true);
            _joyBase.position = t.position;
            UpdateJoystick(t.position);
            break;
        }
    }

    void UpdateJoystick(Vector2 screenPos)
    {
        Vector2 delta = screenPos - _joyOrigin;
        float maxPx = _joyRadius * (_canvas != null ? _canvas.scaleFactor : 1f);
        if (delta.magnitude > maxPx) delta = delta.normalized * maxPx;

        _joyKnob.anchoredPosition = delta;
        MobileInput.MoveX = Mathf.Clamp(delta.x / maxPx, -1f, 1f);
        // Küçük dead zone
        if (Mathf.Abs(MobileInput.MoveX) < 0.15f) MobileInput.MoveX = 0f;
    }

    void ReleaseJoystick()
    {
        _joyFingerId = -1;
        MobileInput.MoveX = 0f;
        _joyKnob.anchoredPosition = Vector2.zero;
        _joyBase.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Butonlar

    void HandleButtons()
    {
        if (_jumpBtn.IsDown && !_jumpBtn.WasDown) MobileInput.JumpPressed = true;
        MobileInput.JumpHeld = _jumpBtn.IsDown;

        if (_attackBtn.IsDown && !_attackBtn.WasDown) MobileInput.AttackPressed = true;
        if (_dashBtn.IsDown && !_dashBtn.WasDown) MobileInput.DashPressed = true;
        if (_pauseBtn.IsDown && !_pauseBtn.WasDown) MobileInput.PausePressed = true;

        // Herhangi bir dokunuş başladığında tap-advance (diyalog, perk seçimi için)
        for (int i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began)
            {
                MobileInput.TapAdvance = true;
                break;
            }
        }

        _jumpBtn.WasDown = _jumpBtn.IsDown;
        _attackBtn.WasDown = _attackBtn.IsDown;
        _dashBtn.WasDown = _dashBtn.IsDown;
        _pauseBtn.WasDown = _pauseBtn.IsDown;
    }

    static bool IsOverUI(Vector2 screenPos)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var ped = new PointerEventData(es) { position = screenPos };
        var results = new System.Collections.Generic.List<RaycastResult>();
        es.RaycastAll(ped, results);
        return results.Count > 0;
    }

    // ---------------------------------------------------------------- Kurulum

    void Build()
    {
        // Canvas
        var cgo = new GameObject("MobileCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 4000;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        _group = cgo.AddComponent<CanvasGroup>();
        _root = (RectTransform)cgo.transform;

        BuildJoystick();
        BuildActionButtons();
        BuildPauseButton();
    }

    void BuildJoystick()
    {
        // Joystick base (başlangıçta gizli, dokunma noktasında gösterilir)
        _joyBase = NewRect("JoyBase", _root);
        _joyBase.anchorMin = _joyBase.anchorMax = _joyBase.pivot = new Vector2(0.5f, 0.5f);
        _joyBase.sizeDelta = new Vector2(280f, 280f);

        var baseImg = _joyBase.gameObject.AddComponent<Image>();
        baseImg.color = new Color(1f, 1f, 1f, 0.15f);
        baseImg.raycastTarget = false;
        MakeCircle(baseImg);

        // Knob
        _joyKnob = NewRect("JoyKnob", _joyBase);
        _joyKnob.anchorMin = _joyKnob.anchorMax = _joyKnob.pivot = new Vector2(0.5f, 0.5f);
        _joyKnob.anchoredPosition = Vector2.zero;
        _joyKnob.sizeDelta = new Vector2(110f, 110f);

        var knobImg = _joyKnob.gameObject.AddComponent<Image>();
        knobImg.color = new Color(1f, 1f, 1f, 0.5f);
        knobImg.raycastTarget = false;
        MakeCircle(knobImg);

        _joyBase.gameObject.SetActive(false);
    }

    void BuildActionButtons()
    {
        float bottomY = 180f;
        float rightX = -160f;

        // Jump - büyük buton (sağ alt)
        _jumpBtn = CreateButton("Jump", _root,
            new Vector2(1f, 0f), new Vector2(rightX, bottomY),
            new Vector2(150f, 150f), new Color(0.3f, 0.75f, 0.4f, 0.45f),
            Localization.Has("mobile.jump") ? Localization.Get("mobile.jump") : "^");

        // Attack - orta buton (jump'ın solunda)
        _attackBtn = CreateButton("Attack", _root,
            new Vector2(1f, 0f), new Vector2(rightX - 170f, bottomY - 30f),
            new Vector2(130f, 130f), new Color(0.9f, 0.35f, 0.25f, 0.45f),
            Localization.Has("mobile.attack") ? Localization.Get("mobile.attack") : "J");

        // Dash - küçük buton (jump'ın üstünde)
        _dashBtn = CreateButton("Dash", _root,
            new Vector2(1f, 0f), new Vector2(rightX - 170f, bottomY + 130f),
            new Vector2(110f, 110f), new Color(0.3f, 0.6f, 1f, 0.45f),
            Localization.Has("mobile.dash") ? Localization.Get("mobile.dash") : ">>");
    }

    void BuildPauseButton()
    {
        _pauseBtn = CreateButton("Pause", _root,
            new Vector2(1f, 1f), new Vector2(-80f, -60f),
            new Vector2(80f, 80f), new Color(0.2f, 0.2f, 0.2f, 0.5f),
            "II");
    }

    MobileButton CreateButton(string name, RectTransform parent,
        Vector2 anchor, Vector2 pos, Vector2 size, Color color, string label)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        MakeCircle(img);

        // Label
        var labelGo = new GameObject("Label", typeof(RectTransform));
        var lrt = (RectTransform)labelGo.transform;
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = size.x * 0.3f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 1f, 1f, 0.8f);
        tmp.raycastTarget = false;

        var btn = rt.gameObject.AddComponent<MobileButton>();
        btn.visual = img;
        btn.baseColor = color;
        return btn;
    }

    static void MakeCircle(Image img)
    {
        // Sprite yoksa kare kullan; runtime'da yuvarlak sprite oluştur
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(32f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }
}

/// <summary>
/// Dokunma ile basılı/bırakılmış durumu takip eden basit buton bileşeni.
/// EventSystem'e bağımlı değildir; doğrudan Touch API kullanır.
/// </summary>
public class MobileButton : MonoBehaviour
{
    public Image visual;
    public Color baseColor;
    public bool IsDown { get; private set; }
    public bool WasDown { get; set; }

    RectTransform _rt;
    Canvas _canvas;
    int _fingerId = -1;

    void Awake()
    {
        _rt = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        int touchCount = Input.touchCount;

        if (_fingerId >= 0)
        {
            bool found = false;
            for (int i = 0; i < touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.fingerId != _fingerId) continue;
                found = true;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    Release();
                }
                break;
            }
            if (!found) Release();
            return;
        }

        for (int i = 0; i < touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase != TouchPhase.Began) continue;
            if (!Contains(t.position)) continue;
            _fingerId = t.fingerId;
            IsDown = true;
            if (visual != null) visual.color = baseColor + new Color(0.2f, 0.2f, 0.2f, 0.25f);
            break;
        }
    }

    void Release()
    {
        _fingerId = -1;
        IsDown = false;
        if (visual != null) visual.color = baseColor;
    }

    bool Contains(Vector2 screenPos)
    {
        // Biraz büyütülmüş dokunma alanı (parmak kalınlığı toleransı)
        Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(_rt, screenPos, cam);
    }
}
