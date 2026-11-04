using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 승리 / 패배 결과창 UI 매니저:
/// - 승리 (모든 Enemy 사망 시): 화면 가운데 승리 UI + [게임 종료] 버튼 표시 -> 클릭 시 프로그램 종료
/// - 패배 (모든 Player 사망 시): 화면 가운데 패배 UI + [다시하기] 버튼 표시 -> 클릭 시 처음부터 다시 진행
/// </summary>
public class BattleResultUIManager : Singleton<BattleResultUIManager>
{
    [Header("결과창 UI 참조 (미할당 시 Canvas 하위에 자동 생성)")]
    [SerializeField] private GameObject resultOverlayPanel;
    [SerializeField] private RectTransform resultWindowRect;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subTitleText;
    [SerializeField] private Button actionButton;
    [SerializeField] private Text actionButtonText;

    private bool isResultShowing = false;
    private bool isVictoryMode = false;
    private static Font cachedKoreanFont;

    public bool IsResultShowing => isResultShowing;

    protected override void Awake()
    {
        base.Awake();
        EnsureResultUI();
        HideResultUI();
    }

    /// <summary>
    /// 모든 Enemy가 사망했을 때: 화면 가운데 승리 UI와 [게임 종료] 버튼을 표시합니다.
    /// </summary>
    public void ShowVictoryUI()
    {
        EnsureResultUI();
        isResultShowing = true;
        isVictoryMode = true;

        if (titleText != null)
        {
            titleText.text = "승 리";
            titleText.color = new Color(0.45f, 0.96f, 1.0f, 1.0f);
        }

        if (subTitleText != null)
        {
            subTitleText.text = "모든 적을 처치했습니다!";
        }

        if (actionButtonText != null)
        {
            actionButtonText.text = "게임 종료";
        }

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.transform.SetAsLastSibling();
            resultOverlayPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 모든 Player가 사망했을 때: 화면 가운데 패배 UI와 [다시하기] 버튼을 표시합니다.
    /// </summary>
    public void ShowDefeatUI()
    {
        EnsureResultUI();
        isResultShowing = true;
        isVictoryMode = false;

        if (titleText != null)
        {
            titleText.text = "패 배";
            titleText.color = new Color(1.0f, 0.28f, 0.36f, 1.0f);
        }

        if (subTitleText != null)
        {
            subTitleText.text = "모든 플레이어가 쓰러졌습니다";
        }

        if (actionButtonText != null)
        {
            actionButtonText.text = "다시하기";
        }

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.transform.SetAsLastSibling();
            resultOverlayPanel.SetActive(true);
        }
    }

    public void HideResultUI()
    {
        isResultShowing = false;
        if (resultOverlayPanel != null)
            resultOverlayPanel.SetActive(false);
    }

    private void OnActionButtonClicked()
    {
        if (isVictoryMode)
        {
            QuitApplication();
        }
        else
        {
            RestartFromBeginning();
        }
    }

    /// <summary>
    /// 승리 시 [게임 종료] 버튼 동작: 프로그램을 종료합니다.
    /// </summary>
    public void QuitApplication()
    {
        Time.timeScale = 1f;
        Debug.Log("[BattleResult] 승리 - 게임 종료 버튼이 클릭되어 프로그램을 종료합니다.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 패배 시 [다시하기] 버튼 동작: 현재 씬을 다시 로드하여 처음부터 다시 진행합니다.
    /// </summary>
    public void RestartFromBeginning()
    {
        Time.timeScale = 1f;
        isResultShowing = false;
        Debug.Log("[BattleResult] 패배 - 다시하기 버튼이 클릭되어 처음부터 다시 시작합니다.");

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void EnsureResultUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (resultOverlayPanel == null)
        {
            Transform existingOverlay = canvas.transform.Find("BattleResultOverlayPanel");
            if (existingOverlay != null)
            {
                resultOverlayPanel = existingOverlay.gameObject;
            }
            else
            {
                resultOverlayPanel = new GameObject(
                    "BattleResultOverlayPanel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                resultOverlayPanel.layer = LayerMask.NameToLayer("UI");
                resultOverlayPanel.transform.SetParent(canvas.transform, false);

                RectTransform rt = resultOverlayPanel.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.localPosition = Vector3.zero;

                Image dimImg = resultOverlayPanel.GetComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.76f);
                dimImg.raycastTarget = true;
            }
        }

        if (resultWindowRect == null && resultOverlayPanel != null)
        {
            Transform existingWin = resultOverlayPanel.transform.Find("BattleResultWindow");
            if (existingWin != null)
            {
                resultWindowRect = existingWin.GetComponent<RectTransform>();
            }
            else
            {
                GameObject winObj = new GameObject(
                    "BattleResultWindow",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                winObj.layer = LayerMask.NameToLayer("UI");
                winObj.transform.SetParent(resultOverlayPanel.transform, false);

                resultWindowRect = winObj.GetComponent<RectTransform>();
                resultWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
                resultWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
                resultWindowRect.pivot = new Vector2(0.5f, 0.5f);
                resultWindowRect.anchoredPosition = Vector2.zero;
                resultWindowRect.sizeDelta = new Vector2(520f, 330f);
                resultWindowRect.localScale = Vector3.one;
                resultWindowRect.localPosition = Vector3.zero;

                Image winImg = winObj.GetComponent<Image>();
                winImg.sprite = PauseMenuManager.GetOrCreateWindowSprite();
                winImg.color = Color.white;
                winImg.raycastTarget = true;
            }
        }

        if (resultWindowRect != null)
        {
            if (titleText == null)
            {
                Transform t = resultWindowRect.Find("ResultTitleText");
                titleText = t != null
                    ? t.GetComponent<Text>()
                    : CreateText(resultWindowRect, "ResultTitleText", "승 리", 44, FontStyle.Bold, new Color(0.45f, 0.96f, 1f, 1f), new Vector2(0f, 82f), new Vector2(440f, 64f));
            }

            if (subTitleText == null)
            {
                Transform st = resultWindowRect.Find("ResultSubTitleText");
                subTitleText = st != null
                    ? st.GetComponent<Text>()
                    : CreateText(resultWindowRect, "ResultSubTitleText", "", 20, FontStyle.Normal, new Color(0.84f, 0.90f, 0.98f, 0.95f), new Vector2(0f, 22f), new Vector2(460f, 42f));
            }

            if (actionButton == null)
            {
                Transform btnTr = resultWindowRect.Find("ResultActionButton");
                if (btnTr != null)
                {
                    actionButton = btnTr.GetComponent<Button>();
                    Transform btnTxtTr = btnTr.Find("ActionButtonText");
                    if (btnTxtTr != null)
                        actionButtonText = btnTxtTr.GetComponent<Text>();
                }
                else
                {
                    GameObject btnObj = new GameObject(
                        "ResultActionButton",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                        typeof(Button)
                    );
                    btnObj.layer = LayerMask.NameToLayer("UI");
                    btnObj.transform.SetParent(resultWindowRect, false);

                    RectTransform btnRt = btnObj.GetComponent<RectTransform>();
                    btnRt.anchorMin = new Vector2(0.5f, 0.5f);
                    btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                    btnRt.pivot = new Vector2(0.5f, 0.5f);
                    btnRt.anchoredPosition = new Vector2(0f, -66f);
                    btnRt.sizeDelta = new Vector2(260f, 72f);
                    btnRt.localScale = Vector3.one;
                    btnRt.localPosition = new Vector3(0f, -66f, 0f);

                    Image btnImg = btnObj.GetComponent<Image>();
                    btnImg.sprite = PauseMenuManager.GetOrCreateButtonSprite();
                    btnImg.color = Color.white;
                    btnImg.raycastTarget = true;

                    actionButton = btnObj.GetComponent<Button>();
                    actionButton.targetGraphic = btnImg;

                    actionButtonText = CreateText(
                        btnObj.transform,
                        "ActionButtonText",
                        "게임 종료",
                        28,
                        FontStyle.Bold,
                        Color.white,
                        Vector2.zero,
                        new Vector2(240f, 64f)
                    );
                }
            }

            if (actionButton != null)
            {
                actionButton.onClick.RemoveListener(OnActionButtonClicked);
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }
        }
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle style,
        Color color,
        Vector2 anchoredPos,
        Vector2 sizeDelta)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        obj.layer = LayerMask.NameToLayer("UI");
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;
        rt.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);

        Text txt = obj.GetComponent<Text>();
        txt.font = GetOrCreateFont();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        Outline outline = obj.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0.05f, 0.15f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        return txt;
    }

    private static Font GetOrCreateFont()
    {
        if (cachedKoreanFont != null) return cachedKoreanFont;

        string[] fontCandidates = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Gulim", "Arial" };
        cachedKoreanFont = Font.CreateDynamicFontFromOSFont(fontCandidates, 28);
        if (cachedKoreanFont == null)
            cachedKoreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedKoreanFont;
    }
}
