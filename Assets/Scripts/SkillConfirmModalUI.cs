using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FGO 스타일 스킬 사용 확인 모달 UI (PDF 3페이지 명세):
/// - 스킬 아이콘 클릭(PointerUp) 시 즉시 발동하지 않고, 스킬 정보(아이콘, 이름, 차지 타임, 상세 설명)를 표시합니다.
/// - 최상위 CanvasGroup(UI 그룹)으로 구성되어 모달이 떠 있는 동안 다른 UI 및 전투 오브젝트와의 간섭을 완벽히 차단합니다.
/// - [확인] 클릭 시 스킬을 발동하고 닫히며, [취소] 또는 우상단 [X] 클릭 시 취소하고 닫힙니다.
/// </summary>
public class SkillConfirmModalUI : Singleton<SkillConfirmModalUI>
{
    [Header("모달 UI 그룹 참조")]
    [SerializeField] private CanvasGroup modalCanvasGroup;
    [SerializeField] private RectTransform modalRootRect;
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private Image skillIconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subTitleText;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI chargeTimeText;
    [SerializeField] private TextMeshProUGUI skillDescText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeXButton;

    private SelectableUnit pendingUnit;
    private ESkillType pendingSkillType;
    private System.Action onConfirmCallback;
    private Coroutine openAnimCoroutine;

    private static Sprite cachedWindowBgSprite;
    private static Sprite cachedCapsuleBtnSprite;
    private static Sprite cachedDiamondBtnSprite;
    private static Sprite cachedDividerLineSprite;

    public bool IsModalOpen => modalCanvasGroup != null && modalCanvasGroup.gameObject.activeSelf && modalCanvasGroup.alpha > 0.01f;

    protected override void Awake()
    {
        base.Awake();
        EnsureModalUI();
        HideImmediate();
    }

    /// <summary>
    /// 스킬 사용 확인 모달을 열고 유저의 확인/취소 입력을 대기합니다.
    /// </summary>
    public void OpenSkillModal(
        SelectableUnit unit,
        ESkillType skillType,
        Sprite iconSprite,
        string skillTitle,
        int cooldownTurns,
        string description,
        bool canAffordCostOrResource,
        string lackResourceReason,
        System.Action onConfirm)
    {
        EnsureModalUI();
        if (modalCanvasGroup == null) return;

        pendingUnit = unit;
        pendingSkillType = skillType;
        onConfirmCallback = onConfirm;

        if (skillIconImage != null)
        {
            skillIconImage.sprite = iconSprite != null ? iconSprite : PlayerSkillUI.GetOrCreateSkillSprite(skillType);
        }

        if (skillNameText != null)
        {
            skillNameText.text = skillTitle;
        }

        if (chargeTimeText != null)
        {
            chargeTimeText.text = $"차지 타임: {cooldownTurns}";
        }

        if (skillDescText != null)
        {
            if (canAffordCostOrResource || string.IsNullOrEmpty(lackResourceReason))
            {
                skillDescText.text = description;
            }
            else
            {
                skillDescText.text = $"{description}\n<color=#FF6B6B>({lackResourceReason})</color>";
            }
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = canAffordCostOrResource;
            if (confirmButtonText != null)
            {
                confirmButtonText.color = canAffordCostOrResource
                    ? new Color(0.18f, 0.16f, 0.14f, 1f)
                    : new Color(0.45f, 0.45f, 0.48f, 0.65f);
            }
        }

        modalRootRect.SetAsLastSibling();
        modalCanvasGroup.gameObject.SetActive(true);
        modalCanvasGroup.interactable = true;
        modalCanvasGroup.blocksRaycasts = true; // 다른 UI와의 간섭 완벽 방지

        if (openAnimCoroutine != null)
            StopCoroutine(openAnimCoroutine);
        openAnimCoroutine = StartCoroutine(CoAnimateOpen());
    }

    private IEnumerator CoAnimateOpen()
    {
        float duration = 0.16f;
        float elapsed = 0f;

        if (windowRect != null)
            windowRect.localScale = new Vector3(0.88f, 0.88f, 1f);
        modalCanvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            modalCanvasGroup.alpha = eased;
            if (windowRect != null)
                windowRect.localScale = Vector3.Lerp(new Vector3(0.88f, 0.88f, 1f), Vector3.one, eased);

            yield return null;
        }

        modalCanvasGroup.alpha = 1f;
        if (windowRect != null)
            windowRect.localScale = Vector3.one;
        openAnimCoroutine = null;
    }

    public void OnClickConfirm()
    {
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        System.Action callback = onConfirmCallback;
        HideModal();
        callback?.Invoke();
    }

    public void OnClickCancel()
    {
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        HideModal();
    }

    public void HideModal()
    {
        if (openAnimCoroutine != null)
        {
            StopCoroutine(openAnimCoroutine);
            openAnimCoroutine = null;
        }
        HideImmediate();
    }

    public void HideImmediate()
    {
        pendingUnit = null;
        onConfirmCallback = null;

        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.alpha = 0f;
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;
            modalCanvasGroup.gameObject.SetActive(false);
        }
    }

    public void EnsureModalUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (modalRootRect == null)
        {
            Transform existing = canvas.transform.Find("SkillConfirmModalGroup");
            if (existing != null)
            {
                modalRootRect = existing.GetComponent<RectTransform>();
                modalCanvasGroup = existing.GetComponent<CanvasGroup>();
            }
        }

        if (modalRootRect == null)
        {
            GameObject rootObj = new GameObject("SkillConfirmModalGroup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Button));
            rootObj.layer = LayerMask.NameToLayer("UI");
            rootObj.transform.SetParent(canvas.transform, false);

            modalRootRect = rootObj.GetComponent<RectTransform>();
            modalRootRect.anchorMin = Vector2.zero;
            modalRootRect.anchorMax = Vector2.one;
            modalRootRect.offsetMin = Vector2.zero;
            modalRootRect.offsetMax = Vector2.zero;
            modalRootRect.localScale = Vector3.one;

            // 반투명 딤드 배경 (다른 UI 클릭 완전 차단)
            Image dimImg = rootObj.GetComponent<Image>();
            dimImg.sprite = UIHelper.GetOrCreateSolidWhiteSprite();
            dimImg.color = new Color(0.01f, 0.02f, 0.06f, 0.58f);
            dimImg.raycastTarget = true;

            modalCanvasGroup = rootObj.GetComponent<CanvasGroup>();
            modalCanvasGroup.alpha = 0f;
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;

            Button bgCancelBtn = rootObj.GetComponent<Button>();
            bgCancelBtn.transition = Selectable.Transition.None;
            bgCancelBtn.onClick.AddListener(OnClickCancel);
        }

        if (modalCanvasGroup == null)
        {
            modalCanvasGroup = modalRootRect.GetComponent<CanvasGroup>();
            if (modalCanvasGroup == null)
                modalCanvasGroup = modalRootRect.gameObject.AddComponent<CanvasGroup>();
        }

        // 중앙 FGO 스타일 모달 윈도우 (1040 x 430)
        Transform winTr = modalRootRect.Find("ModalWindow");
        if (winTr == null)
        {
            GameObject winObj = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            winObj.layer = LayerMask.NameToLayer("UI");
            winObj.transform.SetParent(modalRootRect, false);

            windowRect = winObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(windowRect, new Vector2(0f, 35f), new Vector2(1040f, 430f));

            Image winImg = winObj.GetComponent<Image>();
            winImg.sprite = GetOrCreateWindowBgSprite();
            winImg.type = Image.Type.Sliced;
            winImg.color = Color.white;
            winImg.raycastTarget = true; // 윈도우 내부 클릭 시 배경 닫기 방지
        }
        else
        {
            windowRect = winTr.GetComponent<RectTransform>();
        }

        // 1) 상단 타이틀 "스킬 사용" 및 서브타이틀 "S k i l l"
        if (titleText == null)
        {
            Transform tTr = windowRect.Find("TitleText");
            if (tTr == null)
            {
                titleText = UIHelper.CreateOutlinedText(
                    windowRect,
                    "TitleText",
                    "스킬 사용",
                    34,
                    FontStyle.Bold,
                    new Color(0.95f, 0.98f, 1f, 1f),
                    new Vector2(0f, 168f),
                    new Vector2(400f, 48f),
                    TextAnchor.MiddleCenter,
                    new Color(0.08f, 0.32f, 0.88f, 0.98f),
                    new Vector2(2.2f, -2.2f)
                );
            }
            else
            {
                titleText = UIHelper.GetOrAddTMPText(tTr.gameObject);
            }
        }

        if (subTitleText == null)
        {
            Transform stTr = windowRect.Find("SubTitleText");
            if (stTr == null)
            {
                subTitleText = UIHelper.CreateOutlinedText(
                    windowRect,
                    "SubTitleText",
                    "S  k  i  l  l",
                    14,
                    FontStyle.Bold,
                    new Color(0.58f, 0.68f, 0.85f, 0.9f),
                    new Vector2(0f, 136f),
                    new Vector2(260f, 24f),
                    TextAnchor.MiddleCenter,
                    new Color(0.02f, 0.04f, 0.12f, 0.9f),
                    new Vector2(1.2f, -1.2f)
                );
            }
            else
            {
                subTitleText = UIHelper.GetOrAddTMPText(stTr.gameObject);
            }
        }

        // 상단 푸른 구분선 (HeaderDivider)
        if (windowRect.Find("HeaderDivider") == null)
        {
            GameObject divObj = new GameObject("HeaderDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divObj.layer = LayerMask.NameToLayer("UI");
            divObj.transform.SetParent(windowRect, false);

            RectTransform divRt = divObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(divRt, new Vector2(0f, 122f), new Vector2(960f, 3f));

            Image divImg = divObj.GetComponent<Image>();
            divImg.sprite = GetOrCreateDividerLineSprite();
            divImg.raycastTarget = false;
        }

        // 2) 우측 상단 마름모 [X] 닫기 버튼
        Transform closeTr = windowRect.Find("CloseXButton");
        if (closeTr == null)
        {
            GameObject closeObj = new GameObject("CloseXButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeObj.layer = LayerMask.NameToLayer("UI");
            closeObj.transform.SetParent(windowRect, false);

            RectTransform closeRt = closeObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(closeRt, new Vector2(492f, 178f), new Vector2(78f, 78f));

            Image closeImg = closeObj.GetComponent<Image>();
            closeImg.sprite = GetOrCreateDiamondCloseSprite();
            closeImg.preserveAspect = true;
            closeImg.raycastTarget = true;

            closeXButton = closeObj.GetComponent<Button>();
            closeXButton.targetGraphic = closeImg;
            closeXButton.onClick.AddListener(OnClickCancel);
        }
        else
        {
            closeXButton = closeTr.GetComponent<Button>();
            if (closeXButton != null)
            {
                closeXButton.onClick.RemoveAllListeners();
                closeXButton.onClick.AddListener(OnClickCancel);
            }
        }

        // 3) 좌측 스킬 아이콘 박스
        Transform iconFrameTr = windowRect.Find("SkillIconFrame");
        RectTransform iconFrameRt;
        if (iconFrameTr == null)
        {
            GameObject frameObj = new GameObject("SkillIconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            frameObj.layer = LayerMask.NameToLayer("UI");
            frameObj.transform.SetParent(windowRect, false);

            iconFrameRt = frameObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(iconFrameRt, new Vector2(-390f, 18f), new Vector2(142f, 142f));

            Image frameImg = frameObj.GetComponent<Image>();
            frameImg.sprite = UIHelper.GetOrCreateSolidWhiteSprite();
            frameImg.color = new Color(0.14f, 0.16f, 0.22f, 0.98f);
            frameImg.raycastTarget = false;

            Outline frameOutline = frameObj.GetComponent<Outline>();
            frameOutline.effectColor = new Color(0.85f, 0.72f, 0.36f, 0.95f);
            frameOutline.effectDistance = new Vector2(2.5f, -2.5f);
        }
        else
        {
            iconFrameRt = iconFrameTr.GetComponent<RectTransform>();
        }

        if (skillIconImage == null && iconFrameRt != null)
        {
            Transform icTr = iconFrameRt.Find("SkillIcon");
            if (icTr == null)
            {
                GameObject icObj = new GameObject("SkillIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                icObj.layer = LayerMask.NameToLayer("UI");
                icObj.transform.SetParent(iconFrameRt, false);

                RectTransform icRt = icObj.GetComponent<RectTransform>();
                icRt.anchorMin = Vector2.zero;
                icRt.anchorMax = Vector2.one;
                icRt.offsetMin = new Vector2(4f, 4f);
                icRt.offsetMax = new Vector2(-4f, -4f);
                icRt.localScale = Vector3.one;

                skillIconImage = icObj.GetComponent<Image>();
                skillIconImage.preserveAspect = true;
                skillIconImage.raycastTarget = false;
            }
            else
            {
                skillIconImage = icTr.GetComponent<Image>();
            }
        }

        // 4) 스킬 이름 텍스트 & 차지 타임 텍스트 & 스킬 설명 텍스트
        if (skillNameText == null)
        {
            Transform snTr = windowRect.Find("SkillNameText");
            if (snTr == null)
            {
                skillNameText = UIHelper.CreateOutlinedText(
                    windowRect,
                    "SkillNameText",
                    "군사의 지휘 A+",
                    31,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(-20f, 66f),
                    new Vector2(540f, 44f),
                    TextAnchor.MiddleLeft,
                    new Color(0.02f, 0.04f, 0.10f, 0.98f),
                    new Vector2(2f, -2f)
                );
            }
            else
            {
                skillNameText = UIHelper.GetOrAddTMPText(snTr.gameObject);
            }
        }

        if (chargeTimeText == null)
        {
            Transform ctTr = windowRect.Find("ChargeTimeText");
            if (ctTr == null)
            {
                chargeTimeText = UIHelper.CreateOutlinedText(
                    windowRect,
                    "ChargeTimeText",
                    "차지 타임: 6",
                    25,
                    FontStyle.Bold,
                    new Color(0.56f, 0.85f, 1.0f, 1f),
                    new Vector2(310f, 66f),
                    new Vector2(260f, 40f),
                    TextAnchor.MiddleRight,
                    new Color(0.02f, 0.06f, 0.16f, 0.98f),
                    new Vector2(1.8f, -1.8f)
                );
            }
            else
            {
                chargeTimeText = UIHelper.GetOrAddTMPText(ctTr.gameObject);
            }
        }

        if (skillDescText == null)
        {
            Transform sdTr = windowRect.Find("SkillDescText");
            if (sdTr == null)
            {
                skillDescText = UIHelper.CreateOutlinedText(
                    windowRect,
                    "SkillDescText",
                    "스킬 효과 설명이 여기에 표시됩니다.",
                    24,
                    FontStyle.Normal,
                    new Color(0.92f, 0.95f, 1.0f, 1f),
                    new Vector2(75f, -4f),
                    new Vector2(730f, 88f),
                    TextAnchor.UpperLeft,
                    new Color(0.02f, 0.04f, 0.10f, 0.98f),
                    new Vector2(1.6f, -1.6f)
                );
                skillDescText.lineSpacing = 6f;
                skillDescText.richText = true;
                skillDescText.textWrappingMode = TextWrappingModes.Normal;
            }
            else
            {
                skillDescText = UIHelper.GetOrAddTMPText(sdTr.gameObject);
                skillDescText.richText = true;
                skillDescText.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        // 5) 하단 [취소] 버튼 & [확인] 버튼 (FGO 스타일 둥근 실버 캡슐 버튼)
        EnsureCapsuleButton(
            windowRect,
            "CancelButton",
            "취소",
            new Vector2(-215f, -138f),
            new Vector2(360f, 68f),
            OnClickCancel,
            ref cancelButton,
            out _
        );

        EnsureCapsuleButton(
            windowRect,
            "ConfirmButton",
            "확인",
            new Vector2(215f, -138f),
            new Vector2(360f, 68f),
            OnClickConfirm,
            ref confirmButton,
            out confirmButtonText
        );
    }

    private void EnsureCapsuleButton(
        RectTransform parent,
        string btnName,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        UnityEngine.Events.UnityAction onClickAction,
        ref Button btnRef,
        out TextMeshProUGUI labelTextRef)
    {
        labelTextRef = null;
        Transform tr = parent.Find(btnName);
        GameObject btnObj;
        if (tr == null)
        {
            btnObj = new GameObject(btnName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.layer = LayerMask.NameToLayer("UI");
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(rt, anchoredPos, size);

            Image img = btnObj.GetComponent<Image>();
            img.sprite = GetOrCreateCapsuleBtnSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = true;

            btnRef = btnObj.GetComponent<Button>();
            btnRef.targetGraphic = img;

            labelTextRef = UIHelper.CreateOutlinedText(
                btnObj.transform,
                "Label",
                label,
                28,
                FontStyle.Bold,
                new Color(0.22f, 0.20f, 0.18f, 1f),
                Vector2.zero,
                size,
                TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.85f),
                new Vector2(1.2f, -1.2f)
            );
        }
        else
        {
            btnObj = tr.gameObject;
            btnRef = btnObj.GetComponent<Button>();
            Transform lblTr = btnObj.transform.Find("Label");
            if (lblTr != null)
                labelTextRef = UIHelper.GetOrAddTMPText(lblTr.gameObject);
        }

        if (btnRef != null)
        {
            btnRef.onClick.RemoveAllListeners();
            btnRef.onClick.AddListener(onClickAction);
            UIHelper.EnsureHoverScale(btnRef.gameObject, 1.08f);
        }
    }

    // =========================================================================
    // FGO 스타일 모달 텍스처 프로시저럴 생성기
    // =========================================================================
    public static Sprite GetOrCreateModalWindowSprite() => GetOrCreateWindowBgSprite();

    private static Sprite GetOrCreateWindowBgSprite()
    {
        if (cachedWindowBgSprite != null) return cachedWindowBgSprite;

        int w = 128;
        int h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color outerBorder = new Color(0.55f, 0.63f, 0.78f, 0.96f);
        Color innerDarkTop = new Color(0.03f, 0.06f, 0.14f, 0.94f);
        Color innerDarkBot = new Color(0.01f, 0.02f, 0.06f, 0.96f);

        for (int y = 0; y < h; y++)
        {
            float ty = (float)y / (h - 1);
            Color bgCol = Color.Lerp(innerDarkBot, innerDarkTop, ty);

            for (int x = 0; x < w; x++)
            {
                int distEdge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                if (distEdge < 2)
                {
                    pixels[y * w + x] = outerBorder;
                }
                else if (distEdge == 2)
                {
                    pixels[y * w + x] = new Color(0.18f, 0.25f, 0.42f, 0.95f);
                }
                else
                {
                    pixels[y * w + x] = bgCol;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedWindowBgSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        cachedWindowBgSprite.name = "ui_fgo_modal_window";
        return cachedWindowBgSprite;
    }

    private static Sprite GetOrCreateCapsuleBtnSprite()
    {
        if (cachedCapsuleBtnSprite != null) return cachedCapsuleBtnSprite;

        int w = 192;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        float radius = h * 0.46f;
        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            // FGO 스타일 은백색 메탈릭 그라디언트
            Color topCol = new Color(0.98f, 0.98f, 0.99f, 1f);
            Color midCol = new Color(0.86f, 0.86f, 0.89f, 1f);
            Color botCol = new Color(0.68f, 0.67f, 0.70f, 1f);
            Color gradCol = ny > 0.5f
                ? Color.Lerp(midCol, topCol, (ny - 0.5f) * 2f)
                : Color.Lerp(botCol, midCol, ny * 2f);

            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x, radius, w - 1 - radius);
                float cy = (h - 1) * 0.5f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                Color c = Color.clear;
                if (dist <= radius)
                {
                    float edge = radius - dist;
                    float alpha = Mathf.Clamp01(edge / 1.5f);
                    if (edge < 2.8f)
                    {
                        c = new Color(0.42f, 0.40f, 0.36f, alpha);
                    }
                    else
                    {
                        c = gradCol;
                        c.a = alpha;
                    }
                }
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCapsuleBtnSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32, 16, 32, 16));
        cachedCapsuleBtnSprite.name = "ui_fgo_capsule_btn";
        return cachedCapsuleBtnSprite;
    }

    public static Sprite GetOrCreateDiamondCloseSprite()
    {
        if (cachedDiamondBtnSprite != null) return cachedDiamondBtnSprite;

        int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        float half = (size - 1) * 0.5f;
        float diamondRadius = half * 0.88f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - half);
                float dy = Mathf.Abs(y - half);
                float manhattan = dx + dy;

                Color c = Color.clear;
                if (manhattan <= diamondRadius)
                {
                    float edge = diamondRadius - manhattan;
                    float alpha = Mathf.Clamp01(edge / 1.8f);

                    // 중앙 X 마크 판정
                    float diag1 = Mathf.Abs((x - half) - (y - half)) * 0.7071f;
                    float diag2 = Mathf.Abs((x - half) + (y - half)) * 0.7071f;
                    float xMarkDist = Mathf.Min(diag1, diag2);
                    bool inXRange = dx < half * 0.42f && dy < half * 0.42f;

                    if (edge < 3.5f)
                    {
                        c = new Color(0.45f, 0.48f, 0.56f, alpha);
                    }
                    else if (inXRange && xMarkDist < 4.2f)
                    {
                        c = new Color(0.10f, 0.14f, 0.28f, alpha);
                    }
                    else
                    {
                        float shade = Mathf.Clamp01((y / (float)size) * 0.35f + 0.65f);
                        c = new Color(shade, shade, shade * 1.02f, alpha);
                    }
                }
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedDiamondBtnSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        cachedDiamondBtnSprite.name = "ui_fgo_diamond_close";
        return cachedDiamondBtnSprite;
    }

    public static Sprite GetOrCreateDividerLineSprite()
    {
        if (cachedDividerLineSprite != null) return cachedDividerLineSprite;

        int w = 256;
        int h = 4;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        for (int x = 0; x < w; x++)
        {
            float nx = Mathf.Abs((x / (float)(w - 1)) - 0.5f) * 2f;
            float alpha = Mathf.Clamp01(1f - nx * nx);
            Color col = Color.Lerp(new Color(0.25f, 0.55f, 0.95f, alpha * 0.85f), new Color(0.75f, 0.90f, 1f, alpha), 1f - nx);
            for (int y = 0; y < h; y++)
            {
                pixels[y * w + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedDividerLineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedDividerLineSprite.name = "ui_fgo_divider";
        return cachedDividerLineSprite;
    }
}
