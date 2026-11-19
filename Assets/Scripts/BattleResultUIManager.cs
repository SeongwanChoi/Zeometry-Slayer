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

    public bool IsResultShowing => isResultShowing;

    protected override void Awake()
    {
        base.Awake();
        EnsureResultUI();
        HideResultUI();
    }

    /// <summary>
    /// 모든 Enemy가 사망했을 때: 화면 가운데 승리 UI와 [게임 종료] 버튼을 표시합니다.
    /// (튜토리얼 씬인 경우 튜토리얼 완료 안내 및 [실전 배틀 시작] 버튼으로 전환)
    /// </summary>
    public void ShowVictoryUI()
    {
        if (TutorialManager.Ins != null)
            TutorialManager.Ins.HideGuide();

        EnsureResultUI();
        isResultShowing = true;
        isVictoryMode = true;

        bool isTutorial = SceneManager.GetActiveScene().name.Contains("Tutorial");

        if (titleText != null)
        {
            titleText.text = isTutorial ? "튜토리얼 완료!" : "승 리";
            titleText.color = new Color(0.45f, 0.96f, 1.0f, 1.0f);
            titleText.alignment = TextAnchor.MiddleCenter;
        }

        if (subTitleText != null)
        {
            subTitleText.text = isTutorial ? "기초 전투 규칙을 모두 습득하셨습니다!" : "모든 적을 처치했습니다!";
            subTitleText.alignment = TextAnchor.MiddleCenter;
        }

        if (actionButtonText != null)
        {
            actionButtonText.text = isTutorial ? "실전 배틀 시작" : "게임 종료";
            actionButtonText.alignment = TextAnchor.MiddleCenter;
        }

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.transform.SetAsLastSibling();
            resultOverlayPanel.SetActive(true);
        }

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayVictory();
    }

    /// <summary>
    /// 모든 Player가 사망했을 때: 화면 가운데 패배 UI와 [다시하기] 버튼을 표시합니다.
    /// </summary>
    public void ShowDefeatUI()
    {
        if (TutorialManager.Ins != null)
            TutorialManager.Ins.HideGuide();

        EnsureResultUI();
        isResultShowing = true;
        isVictoryMode = false;

        if (titleText != null)
        {
            titleText.text = "패 배";
            titleText.color = new Color(1.0f, 0.28f, 0.36f, 1.0f);
            titleText.alignment = TextAnchor.MiddleCenter;
        }

        if (subTitleText != null)
        {
            subTitleText.text = "모든 플레이어가 쓰러졌습니다";
            subTitleText.alignment = TextAnchor.MiddleCenter;
        }

        if (actionButtonText != null)
        {
            actionButtonText.text = "다시하기";
            actionButtonText.alignment = TextAnchor.MiddleCenter;
        }

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.transform.SetAsLastSibling();
            resultOverlayPanel.SetActive(true);
        }

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayDefeat();
    }

    public void HideResultUI()
    {
        isResultShowing = false;
        if (resultOverlayPanel != null)
            resultOverlayPanel.SetActive(false);
    }

    private void OnActionButtonClicked()
    {
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        if (isVictoryMode)
        {
            if (SceneManager.GetActiveScene().name.Contains("Tutorial"))
            {
                Time.timeScale = 1f;
                isResultShowing = false;
                SceneManager.LoadScene("BattleScene");
                return;
            }

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
            resultWindowRect.anchorMin = new Vector2(0.5f, 0.5f);
            resultWindowRect.anchorMax = new Vector2(0.5f, 0.5f);
            resultWindowRect.pivot = new Vector2(0.5f, 0.5f);
            resultWindowRect.anchoredPosition = Vector2.zero;
            resultWindowRect.sizeDelta = new Vector2(540f, 340f);
            resultWindowRect.localScale = Vector3.one;
            resultWindowRect.localPosition = Vector3.zero;

            if (titleText == null)
            {
                Transform t = resultWindowRect.Find("ResultTitleText");
                titleText = t != null
                    ? t.GetComponent<Text>()
                    : CreateText(resultWindowRect, "ResultTitleText", "승 리", 42, FontStyle.Bold, new Color(0.45f, 0.96f, 1f, 1f), new Vector2(0f, 88f), new Vector2(480f, 64f));
            }
            if (titleText != null)
            {
                NormalizeCenteredRect(titleText.rectTransform, new Vector2(0f, 88f), new Vector2(480f, 64f));
                titleText.fontSize = 42;
                titleText.fontStyle = FontStyle.Bold;
                titleText.alignment = TextAnchor.MiddleCenter;
            }

            if (subTitleText == null)
            {
                Transform st = resultWindowRect.Find("ResultSubTitleText");
                subTitleText = st != null
                    ? st.GetComponent<Text>()
                    : CreateText(resultWindowRect, "ResultSubTitleText", "", 21, FontStyle.Normal, new Color(0.84f, 0.90f, 0.98f, 0.95f), new Vector2(0f, 18f), new Vector2(480f, 44f));
            }
            if (subTitleText != null)
            {
                NormalizeCenteredRect(subTitleText.rectTransform, new Vector2(0f, 18f), new Vector2(480f, 44f));
                subTitleText.fontSize = 21;
                subTitleText.alignment = TextAnchor.MiddleCenter;
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
                        26,
                        FontStyle.Bold,
                        Color.white,
                        Vector2.zero,
                        new Vector2(300f, 64f)
                    );
                }
            }

            if (actionButton != null)
            {
                RectTransform btnRt = actionButton.GetComponent<RectTransform>();
                NormalizeCenteredRect(btnRt, new Vector2(0f, -72f), new Vector2(320f, 72f));

                actionButton.onClick.RemoveListener(OnActionButtonClicked);
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            if (actionButtonText != null)
            {
                NormalizeCenteredRect(actionButtonText.rectTransform, Vector2.zero, new Vector2(300f, 64f));
                actionButtonText.fontSize = 26;
                actionButtonText.fontStyle = FontStyle.Bold;
                actionButtonText.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    private static void NormalizeCenteredRect(RectTransform rt, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        UIHelper.ConfigureCenteredRect(rt, anchoredPos, sizeDelta);
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
        return UIHelper.CreateOutlinedText(
            parent,
            name,
            content,
            fontSize,
            style,
            color,
            anchoredPos,
            sizeDelta,
            TextAnchor.MiddleCenter,
            new Color(0f, 0.05f, 0.15f, 0.95f),
            new Vector2(2f, -2f)
        );
    }
}
