using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ESC 일시정지 메뉴 매니저:
/// 1. 게임 중 ESC 키를 누르면 화면 전체가 어두워지고(Dim Overlay) 중앙에 메뉴창이 표시됩니다.
/// 2. 메뉴창 안에 [게임 종료] 버튼이 제공되어 클릭 시 게임을 종료합니다.
/// 3. 메뉴창이 열린 상태에서 ESC 키를 다시 한 번 누르면 메뉴창이 닫히고 원래 게임 상태로 돌아옵니다.
/// </summary>
public class PauseMenuManager : Singleton<PauseMenuManager>
{
    [Header("ESC 메뉴 UI 참조 (미할당 시 Canvas 하위에 자동 생성/연결)")]
    [SerializeField] private GameObject pauseOverlayPanel;   // 화면 전체를 어둡게 덮는 배경 패널 (활성/비활성 토글 대상)
    [SerializeField] private RectTransform menuWindowRect;   // 중앙 메뉴창 프레임
    [SerializeField] private Button quitGameButton;          // [게임 종료] 버튼

    [Header("화면 어두워짐(Dim) 및 시간 정지 설정")]
    [SerializeField] private Color dimOverlayColor = new Color(0f, 0f, 0f, 0.72f); // ESC 눌렀을 때 화면 어두워지는 색상/투명도
    [SerializeField] private bool pauseTimeScaleWhenOpen = true;                   // 메뉴가 열렸을 때 Time.timeScale = 0 처리 여부

    private bool isPaused = false;
    private static Sprite cachedWindowSprite;
    private static Sprite cachedButtonSprite;
    private static Font cachedKoreanFont;

    /// <summary>
    /// 현재 ESC 메뉴창이 열려 있는지 여부
    /// </summary>
    public bool IsPaused => isPaused;

    protected override void Awake()
    {
        base.Awake();
        EnsurePauseMenuUI();
        ApplyPauseState(false);
    }

    private void Start()
    {
        EnsurePauseMenuUI();
        ApplyPauseState(false);
    }

    private void Update()
    {
        if (WasEscapePressedThisFrame())
        {
            TogglePauseMenu();
        }
    }

    private bool WasEscapePressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;

        return false;
    }

    /// <summary>
    /// ESC 키를 누를 때마다 메뉴창 열기 / 닫기 상태를 토글합니다.
    /// </summary>
    public void TogglePauseMenu()
    {
        SetPauseMenuOpen(!isPaused);
    }

    /// <summary>
    /// 메뉴창의 열림(true) / 닫힘(false) 상태를 설정합니다.
    /// </summary>
    public void SetPauseMenuOpen(bool open)
    {
        EnsurePauseMenuUI();
        ApplyPauseState(open);
    }

    private void ApplyPauseState(bool open)
    {
        isPaused = open;

        if (pauseOverlayPanel != null)
        {
            if (open)
            {
                // 항상 다른 모든 UI(카드, AttackBox, 배너 등)보다 가장 위에 그려지도록 마지막 자식으로 정렬
                pauseOverlayPanel.transform.SetAsLastSibling();
            }

            pauseOverlayPanel.SetActive(open);
        }

        if (pauseTimeScaleWhenOpen)
        {
            Time.timeScale = open ? 0f : 1f;
        }
    }

    /// <summary>
    /// [게임 종료] 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void QuitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("[PauseMenu] 게임 종료 버튼이 클릭되었습니다. 게임을 종료합니다.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (isPaused && Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Canvas 하위에 어두운 배경 오버레이(PauseOverlayPanel), 메뉴창(PauseMenuWindow), [게임 종료] 버튼이 없으면 자동 생성 및 연결합니다.
    /// </summary>
    public void EnsurePauseMenuUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 1. 어두운 전체 화면 오버레이 패널 찾기 또는 생성
        if (pauseOverlayPanel == null)
        {
            Transform existingOverlay = canvas.transform.Find("PauseOverlayPanel");
            if (existingOverlay != null)
            {
                pauseOverlayPanel = existingOverlay.gameObject;
            }
            else
            {
                pauseOverlayPanel = new GameObject(
                    "PauseOverlayPanel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                pauseOverlayPanel.layer = LayerMask.NameToLayer("UI");
                pauseOverlayPanel.transform.SetParent(canvas.transform, false);

                RectTransform overlayRt = pauseOverlayPanel.GetComponent<RectTransform>();
                overlayRt.anchorMin = Vector2.zero;
                overlayRt.anchorMax = Vector2.one;
                overlayRt.pivot = new Vector2(0.5f, 0.5f);
                overlayRt.anchoredPosition = Vector2.zero;
                overlayRt.sizeDelta = Vector2.zero;
                overlayRt.localScale = Vector3.one;
                overlayRt.localPosition = Vector3.zero;

                Image dimImg = pauseOverlayPanel.GetComponent<Image>();
                dimImg.color = dimOverlayColor;
                dimImg.raycastTarget = true; // 메뉴 뒤에 있는 카드나 캐릭터가 클릭되지 않도록 차단
            }
        }
        else
        {
            Image dimImg = pauseOverlayPanel.GetComponent<Image>();
            if (dimImg != null)
                dimImg.color = dimOverlayColor;
        }

        // 2. 중앙 메뉴창(PauseMenuWindow) 찾기 또는 생성
        if (menuWindowRect == null && pauseOverlayPanel != null)
        {
            Transform existingWin = pauseOverlayPanel.transform.Find("PauseMenuWindow");
            if (existingWin != null)
            {
                menuWindowRect = existingWin.GetComponent<RectTransform>();
            }
            else
            {
                GameObject winObj = new GameObject(
                    "PauseMenuWindow",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                winObj.layer = LayerMask.NameToLayer("UI");
                winObj.transform.SetParent(pauseOverlayPanel.transform, false);

                menuWindowRect = winObj.GetComponent<RectTransform>();
                menuWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
                menuWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
                menuWindowRect.pivot = new Vector2(0.5f, 0.5f);
                menuWindowRect.anchoredPosition = Vector2.zero;
                menuWindowRect.sizeDelta = new Vector2(500f, 320f);
                menuWindowRect.localScale = Vector3.one;
                menuWindowRect.localPosition = Vector3.zero;

                Image winImg = winObj.GetComponent<Image>();
                winImg.sprite = GetOrCreateWindowSprite();
                winImg.color = Color.white;
                winImg.raycastTarget = true;

                // 메뉴 상단 타이틀 텍스트 생성
                CreateTextElement(
                    winObj.transform,
                    "MenuTitleText",
                    "PAUSED",
                    36,
                    FontStyle.Bold,
                    new Color(0.55f, 0.95f, 1f, 1f),
                    new Vector2(0f, 82f),
                    new Vector2(420f, 60f)
                );

                // 안내 서브 텍스트 생성
                CreateTextElement(
                    winObj.transform,
                    "MenuHintText",
                    "ESC 키를 다시 누르면 게임으로 돌아갑니다",
                    18,
                    FontStyle.Normal,
                    new Color(0.78f, 0.86f, 0.95f, 0.9f),
                    new Vector2(0f, 28f),
                    new Vector2(440f, 40f)
                );
            }
        }

        // 3. [게임 종료] 버튼 찾기 또는 생성
        if (quitGameButton == null && menuWindowRect != null)
        {
            Transform existingBtn = menuWindowRect.Find("QuitGameButton");
            if (existingBtn != null)
            {
                quitGameButton = existingBtn.GetComponent<Button>();
            }
            else
            {
                GameObject btnObj = new GameObject(
                    "QuitGameButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button)
                );
                btnObj.layer = LayerMask.NameToLayer("UI");
                btnObj.transform.SetParent(menuWindowRect, false);

                RectTransform btnRt = btnObj.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.5f, 0.5f);
                btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0f, -62f);
                btnRt.sizeDelta = new Vector2(260f, 72f);
                btnRt.localScale = Vector3.one;
                btnRt.localPosition = new Vector3(0f, -62f, 0f);

                Image btnImg = btnObj.GetComponent<Image>();
                btnImg.sprite = GetOrCreateButtonSprite();
                btnImg.color = Color.white;
                btnImg.raycastTarget = true;

                quitGameButton = btnObj.GetComponent<Button>();
                quitGameButton.targetGraphic = btnImg;

                ColorBlock cb = quitGameButton.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1f, 0.85f, 0.88f, 1f);
                cb.pressedColor = new Color(0.75f, 0.55f, 0.6f, 1f);
                cb.selectedColor = Color.white;
                quitGameButton.colors = cb;

                // 버튼 내부 "게임 종료" 텍스트
                CreateTextElement(
                    btnObj.transform,
                    "QuitButtonText",
                    "게임 종료",
                    28,
                    FontStyle.Bold,
                    new Color(1f, 0.93f, 0.95f, 1f),
                    Vector2.zero,
                    new Vector2(240f, 64f)
                );
            }
        }

        // 버튼 클릭 이벤트 연결 보장
        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveListener(QuitGame);
            quitGameButton.onClick.AddListener(QuitGame);
        }
    }

    private static Text CreateTextElement(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Color textColor,
        Vector2 anchoredPos,
        Vector2 sizeDelta)
    {
        GameObject txtObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        txtObj.layer = LayerMask.NameToLayer("UI");
        txtObj.transform.SetParent(parent, false);

        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;
        rt.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);

        Text txt = txtObj.GetComponent<Text>();
        txt.font = GetOrCreateKoreanFont();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.color = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        Outline outline = txtObj.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0.05f, 0.15f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        return txt;
    }

    private static Font GetOrCreateKoreanFont()
    {
        if (cachedKoreanFont != null)
            return cachedKoreanFont;

        // Windows 기본 한글 폰트(맑은 고딕) 우선 로드 -> 실패 시 내장 폰트 Fallback
        string[] fontCandidates = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Gulim", "Arial" };
        cachedKoreanFont = Font.CreateDynamicFontFromOSFont(fontCandidates, 28);

        if (cachedKoreanFont == null)
        {
            cachedKoreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedKoreanFont;
    }

    /// <summary>
    /// 사이버 네온 스타일의 메뉴창 배경 프레임 스프라이트를 생성합니다.
    /// </summary>
    public static Sprite GetOrCreateWindowSprite()
    {
        if (cachedWindowSprite != null)
            return cachedWindowSprite;

        int width = 500;
        int height = 320;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        int margin = 12;
        int chamfer = 22;
        float borderThickness = 3.2f;
        float glowRange = 10f;

        Color bgDark = new Color(0.03f, 0.06f, 0.13f, 0.95f);
        Color deepBlueGlow = new Color(0.02f, 0.35f, 0.90f, 1f);
        Color neonCyanMid = new Color(0.10f, 0.68f, 1.0f, 1f);
        Color neonCyanCore = new Color(0.65f, 0.96f, 1.0f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int dx = Mathf.Min(x - margin, (width - 1 - margin) - x);
                int dy = Mathf.Min(y - margin, (height - 1 - margin) - y);
                float dCorner = (dx + dy - chamfer) * 0.7071f;
                float signedDist = Mathf.Min(Mathf.Min(dx, dy), dCorner);

                float distBorder = Mathf.Abs(signedDist);
                Color finalCol = Color.clear;

                if (distBorder <= borderThickness)
                {
                    float t = 1f - (distBorder / borderThickness);
                    finalCol = Color.Lerp(neonCyanMid, neonCyanCore, t * t);
                    finalCol.a = 1f;
                }
                else if (signedDist > borderThickness)
                {
                    // 내부 어두운 배경 + 상단 헤더 구분선
                    finalCol = bgDark;
                    int headerLineY = height - 88;
                    if (Mathf.Abs(y - headerLineY) <= 1 && dx > 28)
                    {
                        finalCol = Color.Lerp(bgDark, neonCyanMid, 0.45f);
                    }
                }
                else
                {
                    float outDist = -signedDist - borderThickness;
                    if (outDist < glowRange)
                    {
                        float g = 1f - (outDist / glowRange);
                        finalCol = deepBlueGlow;
                        finalCol.a = g * g * 0.75f;
                    }
                }

                pixels[y * width + x] = finalCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedWindowSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedWindowSprite.name = "pause_menu_window_runtime";
        return cachedWindowSprite;
    }

    /// <summary>
    /// 사이버 네온 레드 스타일의 [게임 종료] 버튼 배경 스프라이트를 생성합니다.
    /// </summary>
    public static Sprite GetOrCreateButtonSprite()
    {
        if (cachedButtonSprite != null)
            return cachedButtonSprite;

        int width = 260;
        int height = 72;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        int margin = 8;
        int chamfer = 12;
        float borderThickness = 2.6f;
        float glowRange = 7f;

        Color btnBg = new Color(0.18f, 0.03f, 0.07f, 0.94f);
        Color redGlow = new Color(0.85f, 0.05f, 0.22f, 1f);
        Color neonRedMid = new Color(1.0f, 0.18f, 0.35f, 1f);
        Color neonRedCore = new Color(1.0f, 0.72f, 0.80f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int dx = Mathf.Min(x - margin, (width - 1 - margin) - x);
                int dy = Mathf.Min(y - margin, (height - 1 - margin) - y);
                float dCorner = (dx + dy - chamfer) * 0.7071f;
                float signedDist = Mathf.Min(Mathf.Min(dx, dy), dCorner);

                float distBorder = Mathf.Abs(signedDist);
                Color finalCol = Color.clear;

                if (distBorder <= borderThickness)
                {
                    float t = 1f - (distBorder / borderThickness);
                    finalCol = Color.Lerp(neonRedMid, neonRedCore, t * t);
                    finalCol.a = 1f;
                }
                else if (signedDist > borderThickness)
                {
                    finalCol = btnBg;
                }
                else
                {
                    float outDist = -signedDist - borderThickness;
                    if (outDist < glowRange)
                    {
                        float g = 1f - (outDist / glowRange);
                        finalCol = redGlow;
                        finalCol.a = g * g * 0.7f;
                    }
                }

                pixels[y * width + x] = finalCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedButtonSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedButtonSprite.name = "btn_quit_game_runtime";
        return cachedButtonSprite;
    }
}
