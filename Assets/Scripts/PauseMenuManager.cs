using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ESC 일시정지 메뉴 매니저:
/// 1. 게임 중 ESC 키를 누르면 화면 전체가 어두워지고(Dim Overlay) 중앙에 메뉴창이 표시됩니다.
/// 2. 메뉴창 안에 사운드 볼륨 조절(슬라이더, -/+ 미세 조절 버튼, 음소거 토글 버튼) 메뉴가 제공되어 실시간으로 효과음 볼륨을 조절합니다.
/// 3. 메뉴창 하단에 [게임 종료] 버튼이 제공되어 클릭 시 게임을 종료합니다.
/// 4. 메뉴창이 열린 상태에서 ESC 키를 다시 한 번 누르면 메뉴창이 닫히고 원래 게임 상태로 돌아옵니다.
/// </summary>
public class PauseMenuManager : Singleton<PauseMenuManager>
{
    [Header("ESC 메뉴 UI 참조 (미할당 시 Canvas 하위에 자동 생성/연결)")]
    [SerializeField] private GameObject pauseOverlayPanel;   // 화면 전체를 어둡게 덮는 배경 패널 (활성/비활성 토글 대상)
    [SerializeField] private RectTransform menuWindowRect;   // 중앙 메뉴창 프레임
    [SerializeField] private Button quitGameButton;          // [게임 종료] 버튼

    [Header("사운드 조절 UI 참조 (미할당 시 PauseMenuWindow 하위에 자동 생성/연결)")]
    [SerializeField] private RectTransform soundControlGroup;
    [SerializeField] private Slider soundVolumeSlider;
    [SerializeField] private Text soundValueText;
    [SerializeField] private Button volumeDownButton;
    [SerializeField] private Button volumeUpButton;
    [SerializeField] private Button muteToggleButton;
    [SerializeField] private Text muteToggleButtonText;

    [Header("화면 어두워짐(Dim) 및 시간 정지 설정")]
    [SerializeField] private Color dimOverlayColor = new Color(0f, 0f, 0f, 0.72f); // ESC 눌렀을 때 화면 어두워지는 색상/투명도
    [SerializeField] private bool pauseTimeScaleWhenOpen = true;                   // 메뉴가 열렸을 때 Time.timeScale = 0 처리 여부

    private bool isPaused = false;
    private bool isUpdatingSoundUI = false;
    private float lastSliderPreviewTime = -10f;

    private static Sprite cachedWindowSprite;
    private static Sprite cachedButtonSprite;
    private static Sprite cachedSubButtonSprite;
    private static Sprite cachedSliderTrackSprite;
    private static Sprite cachedSliderFillSprite;
    private static Sprite cachedSliderHandleSprite;

    /// <summary>
    /// 현재 ESC 메뉴창이 열려 있는지 여부
    /// </summary>
    public bool IsPaused => isPaused;

    protected override void Awake()
    {
        base.Awake();
        EnsurePauseMenuUI();
        RefreshSoundControlUI();
        ApplyPauseState(false);
    }

    private void Start()
    {
        EnsurePauseMenuUI();
        RefreshSoundControlUI();
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
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();
        SetPauseMenuOpen(!isPaused);
    }

    /// <summary>
    /// 메뉴창의 열림(true) / 닫힘(false) 상태를 설정합니다.
    /// </summary>
    public void SetPauseMenuOpen(bool open)
    {
        EnsurePauseMenuUI();
        if (open)
        {
            RefreshSoundControlUI();
        }
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

    // =========================================================================
    // 사운드 조절 기능 (슬라이더 / -10% / +10% / 음소거 토글)
    // =========================================================================

    private void OnSoundSliderValueChanged(float newValue)
    {
        if (isUpdatingSoundUI) return;

        float clamped = Mathf.Clamp01(newValue);
        if (SoundManager.Ins != null)
        {
            SoundManager.Ins.SetMasterSfxVolume(clamped, playPreviewClick: false);

            // 슬라이더 드래그 중 실시간 볼륨 확인용 프리뷰 사운드 (너무 잦은 중첩 방지를 위해 0.09초 간격 제한)
            if (!SoundManager.Ins.IsMuted && clamped > 0.001f && Time.unscaledTime - lastSliderPreviewTime >= 0.09f)
            {
                lastSliderPreviewTime = Time.unscaledTime;
                SoundManager.Ins.PlayUIClick();
            }
        }

        RefreshSoundControlUI();
    }

    private void OnVolumeDownClicked()
    {
        if (SoundManager.Ins == null) return;

        float current = SoundManager.Ins.IsMuted ? 0f : SoundManager.Ins.MasterSfxVolume;
        float next = Mathf.Clamp01(Mathf.Round((current - 0.1f) * 10f) / 10f);
        SoundManager.Ins.SetMasterSfxVolume(next, playPreviewClick: true);
        RefreshSoundControlUI();
    }

    private void OnVolumeUpClicked()
    {
        if (SoundManager.Ins == null) return;

        float current = SoundManager.Ins.IsMuted ? 0f : SoundManager.Ins.MasterSfxVolume;
        float next = Mathf.Clamp01(Mathf.Round((current + 0.1f) * 10f) / 10f);
        SoundManager.Ins.SetMasterSfxVolume(next, playPreviewClick: true);
        RefreshSoundControlUI();
    }

    private void OnMuteToggleClicked()
    {
        if (SoundManager.Ins == null) return;

        SoundManager.Ins.ToggleMute();
        RefreshSoundControlUI();
    }

    /// <summary>
    /// 현재 SoundManager의 볼륨 및 음소거 상태를 슬라이더, 퍼센트 텍스트, 음소거 버튼 UI에 반영합니다.
    /// </summary>
    public void RefreshSoundControlUI()
    {
        isUpdatingSoundUI = true;

        float volume = SoundManager.Ins != null ? SoundManager.Ins.MasterSfxVolume : 0.85f;
        bool muted = SoundManager.Ins != null && SoundManager.Ins.IsMuted;
        float displayVol = muted ? 0f : volume;
        int percent = Mathf.RoundToInt(displayVol * 100f);

        if (soundVolumeSlider != null)
        {
            soundVolumeSlider.SetValueWithoutNotify(displayVol);
        }

        if (soundValueText != null)
        {
            if (muted || percent == 0)
            {
                soundValueText.text = "MUTE (0%)";
                soundValueText.color = new Color(1.0f, 0.42f, 0.48f, 1f);
            }
            else
            {
                soundValueText.text = $"{percent}%";
                soundValueText.color = new Color(0.55f, 0.96f, 1.0f, 1f);
            }
        }

        if (muteToggleButtonText != null)
        {
            muteToggleButtonText.text = (muted || percent == 0) ? "음소거 해제" : "음소거";
            muteToggleButtonText.color = (muted || percent == 0)
                ? new Color(1.0f, 0.78f, 0.42f, 1f)
                : new Color(0.85f, 0.95f, 1.0f, 1f);
        }

        isUpdatingSoundUI = false;
    }

    /// <summary>
    /// [게임 종료] 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void QuitGame()
    {
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        Time.timeScale = 1f;
        Debug.Log("[PauseMenu] 게임 종료 버튼이 클릭되었습니다. 게임을 종료합니다.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    protected override void OnDestroy()
    {
        if (isPaused && Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
        base.OnDestroy();
    }

    /// <summary>
    /// Canvas 하위에 어두운 배경 오버레이(PauseOverlayPanel), 메뉴창(PauseMenuWindow),
    /// 사운드 조절 UI(SoundControlGroup), [게임 종료] 버튼이 없으면 자동 생성 및 연결합니다.
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
            }
        }

        if (menuWindowRect != null)
        {
            menuWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuWindowRect.pivot = new Vector2(0.5f, 0.5f);
            menuWindowRect.anchoredPosition = Vector2.zero;
            menuWindowRect.sizeDelta = new Vector2(520f, 410f);
            menuWindowRect.localScale = Vector3.one;
            menuWindowRect.localPosition = Vector3.zero;

            Image winImg = menuWindowRect.GetComponent<Image>();
            if (winImg != null)
            {
                winImg.sprite = GetOrCreateWindowSprite();
                winImg.color = Color.white;
                winImg.raycastTarget = true;
            }

            // 상단 타이틀 텍스트 찾기 또는 생성 (사운드 메뉴가 들어가도록 상단 배치 조정)
            Transform titleTr = menuWindowRect.Find("MenuTitleText");
            if (titleTr == null)
            {
                CreateTextElement(
                    menuWindowRect,
                    "MenuTitleText",
                    "PAUSED",
                    36,
                    FontStyle.Bold,
                    new Color(0.55f, 0.95f, 1f, 1f),
                    new Vector2(0f, 146f),
                    new Vector2(440f, 56f)
                );
            }
            else
            {
                RectTransform titleRt = titleTr.GetComponent<RectTransform>();
                if (titleRt != null)
                {
                    titleRt.anchoredPosition = new Vector2(0f, 146f);
                    titleRt.localPosition = new Vector3(0f, 146f, 0f);
                }
            }

            // 안내 서브 텍스트 찾기 또는 생성
            Transform hintTr = menuWindowRect.Find("MenuHintText");
            if (hintTr == null)
            {
                CreateTextElement(
                    menuWindowRect,
                    "MenuHintText",
                    "ESC 키를 다시 누르면 게임으로 돌아갑니다",
                    18,
                    FontStyle.Normal,
                    new Color(0.78f, 0.86f, 0.95f, 0.9f),
                    new Vector2(0f, 92f),
                    new Vector2(450f, 36f)
                );
            }
            else
            {
                RectTransform hintRt = hintTr.GetComponent<RectTransform>();
                if (hintRt != null)
                {
                    hintRt.anchoredPosition = new Vector2(0f, 92f);
                    hintRt.localPosition = new Vector3(0f, 92f, 0f);
                }
            }

            // 3. 사운드 조절 UI 그룹(SoundControlGroup) 생성 및 연결
            EnsureSoundControlGroup(menuWindowRect);
        }

        // 4. [게임 종료] 버튼 찾기 또는 생성
        if (quitGameButton == null && menuWindowRect != null)
        {
            Transform existingBtn = menuWindowRect.Find("QuitGameButton");
            if (existingBtn != null)
            {
                quitGameButton = existingBtn.GetComponent<Button>();
                Image existingBtnImg = existingBtn.GetComponent<Image>();
                if (existingBtnImg != null)
                {
                    existingBtnImg.sprite = GetOrCreateButtonSprite();
                    existingBtnImg.color = Color.white;
                }
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

        // 버튼 위치 및 클릭 이벤트 연결 보장
        if (quitGameButton != null)
        {
            RectTransform btnRt = quitGameButton.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchorMin = new Vector2(0.5f, 0.5f);
                btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0f, -124f);
                btnRt.sizeDelta = new Vector2(260f, 68f);
                btnRt.localScale = Vector3.one;
                btnRt.localPosition = new Vector3(0f, -124f, 0f);
            }

            quitGameButton.onClick.RemoveListener(QuitGame);
            quitGameButton.onClick.AddListener(QuitGame);
        }
    }

    private void EnsureSoundControlGroup(RectTransform parentWindow)
    {
        if (parentWindow == null) return;

        if (soundControlGroup == null)
        {
            Transform existingGroup = parentWindow.Find("SoundControlGroup");
            if (existingGroup != null)
            {
                soundControlGroup = existingGroup.GetComponent<RectTransform>();
            }
            else
            {
                GameObject groupObj = new GameObject(
                    "SoundControlGroup",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                groupObj.layer = LayerMask.NameToLayer("UI");
                groupObj.transform.SetParent(parentWindow, false);

                soundControlGroup = groupObj.GetComponent<RectTransform>();

                Image panelBg = groupObj.GetComponent<Image>();
                panelBg.color = new Color(0.05f, 0.11f, 0.22f, 0.72f);
                panelBg.raycastTarget = false;
            }
        }

        soundControlGroup.anchorMin = new Vector2(0.5f, 0.5f);
        soundControlGroup.anchorMax = new Vector2(0.5f, 0.5f);
        soundControlGroup.pivot = new Vector2(0.5f, 0.5f);
        soundControlGroup.anchoredPosition = new Vector2(0f, 2f);
        soundControlGroup.sizeDelta = new Vector2(444f, 108f);
        soundControlGroup.localScale = Vector3.one;
        soundControlGroup.localPosition = new Vector3(0f, 2f, 0f);

        // 1) "사운드 조절 (SFX)" 라벨 텍스트
        Transform labelTr = soundControlGroup.Find("SoundLabelText");
        Text labelText = labelTr != null ? labelTr.GetComponent<Text>() : null;
        if (labelText == null)
        {
            labelText = CreateTextElement(
                soundControlGroup,
                "SoundLabelText",
                "효과음 볼륨 (SFX)",
                20,
                FontStyle.Bold,
                new Color(0.88f, 0.95f, 1.0f, 1f),
                new Vector2(-118f, 26f),
                new Vector2(180f, 34f)
            );
        }
        if (labelText.font == null) labelText.font = GetOrCreateKoreanFont();
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.rectTransform.anchoredPosition = new Vector2(-118f, 26f);
        labelText.rectTransform.localPosition = new Vector3(-118f, 26f, 0f);

        // 2) 현재 볼륨 퍼센트 표시 텍스트 ("85%")
        if (soundValueText == null)
        {
            Transform valTr = soundControlGroup.Find("SoundValueText");
            if (valTr != null)
            {
                soundValueText = valTr.GetComponent<Text>();
            }
            else
            {
                soundValueText = CreateTextElement(
                    soundControlGroup,
                    "SoundValueText",
                    "85%",
                    20,
                    FontStyle.Bold,
                    new Color(0.55f, 0.96f, 1.0f, 1f),
                    new Vector2(36f, 26f),
                    new Vector2(116f, 34f)
                );
            }
        }
        if (soundValueText.font == null) soundValueText.font = GetOrCreateKoreanFont();
        soundValueText.alignment = TextAnchor.MiddleRight;
        soundValueText.rectTransform.anchoredPosition = new Vector2(36f, 26f);
        soundValueText.rectTransform.localPosition = new Vector3(36f, 26f, 0f);

        // 3) [음소거 / 음소거 해제] 토글 버튼
        if (muteToggleButton == null)
        {
            Transform muteTr = soundControlGroup.Find("MuteToggleButton");
            if (muteTr != null)
            {
                muteToggleButton = muteTr.GetComponent<Button>();
                Transform txtTr = muteTr.Find("ButtonLabel");
                if (txtTr != null)
                    muteToggleButtonText = txtTr.GetComponent<Text>();
            }
            else
            {
                muteToggleButton = CreateSubControlButton(
                    soundControlGroup,
                    "MuteToggleButton",
                    "음소거",
                    16,
                    new Vector2(156f, 26f),
                    new Vector2(104f, 34f),
                    out muteToggleButtonText
                );
            }
        }
        EnsureSubButtonVisuals(muteToggleButton, ref muteToggleButtonText);
        muteToggleButton.onClick.RemoveListener(OnMuteToggleClicked);
        muteToggleButton.onClick.AddListener(OnMuteToggleClicked);

        // 4) [-] 볼륨 감소 버튼
        if (volumeDownButton == null)
        {
            Transform downTr = soundControlGroup.Find("VolumeDownButton");
            if (downTr != null)
            {
                volumeDownButton = downTr.GetComponent<Button>();
            }
            else
            {
                volumeDownButton = CreateSubControlButton(
                    soundControlGroup,
                    "VolumeDownButton",
                    "-",
                    24,
                    new Vector2(-188f, -22f),
                    new Vector2(42f, 38f),
                    out _
                );
            }
        }
        Text dummyDownTxt = null;
        EnsureSubButtonVisuals(volumeDownButton, ref dummyDownTxt);
        volumeDownButton.onClick.RemoveListener(OnVolumeDownClicked);
        volumeDownButton.onClick.AddListener(OnVolumeDownClicked);

        // 5) [+] 볼륨 증가 버튼
        if (volumeUpButton == null)
        {
            Transform upTr = soundControlGroup.Find("VolumeUpButton");
            if (upTr != null)
            {
                volumeUpButton = upTr.GetComponent<Button>();
            }
            else
            {
                volumeUpButton = CreateSubControlButton(
                    soundControlGroup,
                    "VolumeUpButton",
                    "+",
                    24,
                    new Vector2(188f, -22f),
                    new Vector2(42f, 38f),
                    out _
                );
            }
        }
        Text dummyUpTxt = null;
        EnsureSubButtonVisuals(volumeUpButton, ref dummyUpTxt);
        volumeUpButton.onClick.RemoveListener(OnVolumeUpClicked);
        volumeUpButton.onClick.AddListener(OnVolumeUpClicked);

        // 6) 볼륨 조절 슬라이더 (SoundVolumeSlider)
        if (soundVolumeSlider == null)
        {
            Transform sliderTr = soundControlGroup.Find("SoundVolumeSlider");
            if (sliderTr != null)
            {
                soundVolumeSlider = sliderTr.GetComponent<Slider>();
            }
            else
            {
                soundVolumeSlider = CreateVolumeSlider(
                    soundControlGroup,
                    "SoundVolumeSlider",
                    new Vector2(0f, -22f),
                    new Vector2(312f, 28f)
                );
            }
        }

        if (soundVolumeSlider != null)
        {
            EnsureSliderVisuals(soundVolumeSlider);
            soundVolumeSlider.minValue = 0f;
            soundVolumeSlider.maxValue = 1f;
            soundVolumeSlider.wholeNumbers = false;
            soundVolumeSlider.onValueChanged.RemoveListener(OnSoundSliderValueChanged);
            soundVolumeSlider.onValueChanged.AddListener(OnSoundSliderValueChanged);
        }
    }

    private static void EnsureSubButtonVisuals(Button btn, ref Text labelTxt)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null && img.sprite == null)
            img.sprite = GetOrCreateSubButtonSprite();

        if (labelTxt == null)
        {
            Transform t = btn.transform.Find("ButtonLabel");
            if (t != null) labelTxt = t.GetComponent<Text>();
        }
        if (labelTxt != null && labelTxt.font == null)
            labelTxt.font = GetOrCreateKoreanFont();
    }

    private static void EnsureSliderVisuals(Slider slider)
    {
        if (slider == null) return;

        Transform bgTr = slider.transform.Find("Background");
        if (bgTr != null)
        {
            Image bgImg = bgTr.GetComponent<Image>();
            if (bgImg != null && bgImg.sprite == null)
                bgImg.sprite = GetOrCreateSliderTrackSprite();
        }

        if (slider.fillRect != null)
        {
            Image fillImg = slider.fillRect.GetComponent<Image>();
            if (fillImg != null && fillImg.sprite == null)
                fillImg.sprite = GetOrCreateSliderFillSprite();
        }

        if (slider.handleRect != null)
        {
            Image handleImg = slider.handleRect.GetComponent<Image>();
            if (handleImg != null && handleImg.sprite == null)
                handleImg.sprite = GetOrCreateSliderHandleSprite();
        }
    }

    private static Button CreateSubControlButton(
        Transform parent,
        string name,
        string label,
        int fontSize,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        out Text labelText)
    {
        GameObject btnObj = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        btnObj.layer = LayerMask.NameToLayer("UI");
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;
        rt.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);

        Image img = btnObj.GetComponent<Image>();
        img.sprite = GetOrCreateSubButtonSprite();
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.82f, 0.96f, 1.0f, 1f);
        cb.pressedColor = new Color(0.55f, 0.78f, 0.92f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        labelText = CreateTextElement(
            btnObj.transform,
            "ButtonLabel",
            label,
            fontSize,
            FontStyle.Bold,
            new Color(0.85f, 0.96f, 1.0f, 1f),
            Vector2.zero,
            sizeDelta
        );

        return btn;
    }

    private static Slider CreateVolumeSlider(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 sizeDelta)
    {
        GameObject sliderObj = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Slider)
        );
        sliderObj.layer = LayerMask.NameToLayer("UI");
        sliderObj.transform.SetParent(parent, false);

        RectTransform sliderRt = sliderObj.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRt.pivot = new Vector2(0.5f, 0.5f);
        sliderRt.anchoredPosition = anchoredPos;
        sliderRt.sizeDelta = sizeDelta;
        sliderRt.localScale = Vector3.one;
        sliderRt.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);

        // 1) Background Track
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgObj.layer = LayerMask.NameToLayer("UI");
        bgObj.transform.SetParent(sliderObj.transform, false);

        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgRt.localScale = Vector3.one;

        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.sprite = GetOrCreateSliderTrackSprite();
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        // 2) Fill Area & Fill
        GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObj.layer = LayerMask.NameToLayer("UI");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);

        RectTransform fillAreaRt = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = new Vector2(4f, 2f);
        fillAreaRt.offsetMax = new Vector2(-4f, -2f);
        fillAreaRt.localScale = Vector3.one;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.layer = LayerMask.NameToLayer("UI");
        fillObj.transform.SetParent(fillAreaObj.transform, false);

        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fillRt.localScale = Vector3.one;

        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.sprite = GetOrCreateSliderFillSprite();
        fillImg.color = Color.white;
        fillImg.raycastTarget = false;

        // 3) Handle Slide Area & Handle
        GameObject handleAreaObj = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaObj.layer = LayerMask.NameToLayer("UI");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);

        RectTransform handleAreaRt = handleAreaObj.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);
        handleAreaRt.localScale = Vector3.one;

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObj.layer = LayerMask.NameToLayer("UI");
        handleObj.transform.SetParent(handleAreaObj.transform, false);

        RectTransform handleRt = handleObj.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 28f);
        handleRt.localScale = Vector3.one;

        Image handleImg = handleObj.GetComponent<Image>();
        handleImg.sprite = GetOrCreateSliderHandleSprite();
        handleImg.color = Color.white;
        handleImg.raycastTarget = true;

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.85f;

        return slider;
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
        return UIHelper.CreateOutlinedText(
            parent,
            name,
            content,
            fontSize,
            fontStyle,
            textColor,
            anchoredPos,
            sizeDelta,
            TextAnchor.MiddleCenter,
            new Color(0f, 0.05f, 0.15f, 0.95f),
            new Vector2(2f, -2f)
        );
    }

    private static Font GetOrCreateKoreanFont() => UIHelper.GetOrCreateFont();

    /// <summary>
    /// 사이버 네온 스타일의 메뉴창 배경 프레임 스프라이트를 생성합니다.
    /// </summary>
    public static Sprite GetOrCreateWindowSprite()
    {
        if (cachedWindowSprite != null)
            return cachedWindowSprite;

        int width = 520;
        int height = 410;
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
    /// 사이버 네온 시안 스타일의 보조 컨트롤 버튼(- / + / 음소거) 스프라이트를 생성합니다.
    /// </summary>
    private static Sprite GetOrCreateSubButtonSprite()
    {
        if (cachedSubButtonSprite != null)
            return cachedSubButtonSprite;

        int width = 96;
        int height = 40;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        Color bgCol = new Color(0.06f, 0.16f, 0.30f, 0.95f);
        Color borderCol = new Color(0.25f, 0.82f, 1.0f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                pixels[y * width + x] = isBorder ? borderCol : bgCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedSubButtonSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedSubButtonSprite.name = "btn_sound_sub_runtime";
        return cachedSubButtonSprite;
    }

    private static Sprite GetOrCreateSliderTrackSprite()
    {
        if (cachedSliderTrackSprite != null)
            return cachedSliderTrackSprite;

        int width = 64;
        int height = 16;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        Color bgCol = new Color(0.02f, 0.05f, 0.11f, 0.96f);
        Color borderCol = new Color(0.18f, 0.52f, 0.78f, 0.9f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                pixels[y * width + x] = isBorder ? borderCol : bgCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedSliderTrackSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedSliderTrackSprite.name = "slider_track_runtime";
        return cachedSliderTrackSprite;
    }

    private static Sprite GetOrCreateSliderFillSprite()
    {
        if (cachedSliderFillSprite != null)
            return cachedSliderFillSprite;

        int width = 64;
        int height = 12;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        Color bottomCyan = new Color(0.10f, 0.68f, 0.98f, 1f);
        Color topCyan = new Color(0.55f, 0.96f, 1.0f, 1f);

        for (int y = 0; y < height; y++)
        {
            float t = (float)y / Mathf.Max(1, height - 1);
            Color rowCol = Color.Lerp(bottomCyan, topCyan, t);
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = rowCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedSliderFillSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedSliderFillSprite.name = "slider_fill_runtime";
        return cachedSliderFillSprite;
    }

    private static Sprite GetOrCreateSliderHandleSprite()
    {
        if (cachedSliderHandleSprite != null)
            return cachedSliderHandleSprite;

        int width = 20;
        int height = 28;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        Color coreCol = new Color(0.88f, 0.98f, 1.0f, 1f);
        Color borderCol = new Color(0.15f, 0.78f, 1.0f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                pixels[y * width + x] = isBorder ? borderCol : coreCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedSliderHandleSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        cachedSliderHandleSprite.name = "slider_handle_runtime";
        return cachedSliderHandleSprite;
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
