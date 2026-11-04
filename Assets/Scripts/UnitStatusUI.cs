using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 각 Player 및 Enemy 오브젝트 아래에 표시되는 체력바(Unity 자체 UI)와
/// 체력바 아래 왼쪽에 단일 깃털(버프) 이미지 + 우측 상단(제곱 위치)에 작은 버프 수치를 표시하는 UI를 관리합니다.
/// </summary>
[ExecuteAlways]
public class UnitStatusUI : MonoBehaviour
{
    [Header("연결된 대상 유닛")]
    [SerializeField] private SelectableUnit targetUnit;

    [Header("체력바 UI 참조")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform hpFillRect;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Text hpValueText;

    [Header("버프(깃털 1개 + 제곱 형태 숫자) UI 참조")]
    [SerializeField] private RectTransform buffContainerRect;
    [SerializeField] private Image singleFeatherImage;
    [SerializeField] private Text buffExponentText;
    [SerializeField] private Sprite featherBuffSprite;

    [Header("위치 및 크기 설정")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, -0.78f, 0f);
    [SerializeField] private Vector2 hpBarSize = new Vector2(142f, 22f);
    [SerializeField] private Vector2 featherIconSize = new Vector2(24f, 24f);

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private static Sprite cachedSolidWhiteSprite;
    private static Sprite cachedFeatherSprite;
    private static Font cachedFont;

    public CanvasGroup CanvasGroup => canvasGroup;

    public void Initialize(SelectableUnit unit, Sprite customFeatherSprite = null)
    {
        targetUnit = unit;
        if (customFeatherSprite != null)
            featherBuffSprite = customFeatherSprite;

        EnsureUIHierarchy();
        UpdatePosition();
        RefreshStatus(targetUnit.CurrentHp, targetUnit.MaxHp, targetUnit.BuffCount);
    }

    private void Awake()
    {
        EnsureUIHierarchy();
    }

    private void LateUpdate()
    {
        if (targetUnit == null)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            return;
        }

        UpdatePosition();
    }

    /// <summary>
    /// 대상 유닛의 기본 월드 좌표 바로 아래로 UI 위치를 동기화합니다.
    /// 에디터(실행 전)와 Play 모드(실행 후)에서 항상 100% 동일한 위치값을 유지하도록 카메라 직교 투영 비율로 계산합니다.
    /// </summary>
    public void UpdatePosition()
    {
        if (targetUnit == null) return;
        if (rootRect == null)
            rootRect = GetComponent<RectTransform>();
        if (rootRect == null) return;

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();
        }

        if (parentCanvas == null || canvasRect == null) return;

        Camera worldCam = (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay && parentCanvas.worldCamera != null)
            ? parentCanvas.worldCamera
            : Camera.main;

        if (worldCam == null) return;

        Vector3 unitBaseWorldPos = targetUnit.WorldBasePosition + worldOffset;

        // 직교(Orthographic) 2D 카메라 환경에서는 에디터 창 포커스(Scene 뷰 / Inspector 뷰)에 의해
        // WorldToScreenPoint 해상도가 흔들리지 않도록 Canvas 높이 기준 월드->캔버스 비율로 직접 변환합니다.
        // 이 계산값은 Play 모드 실행 시의 위치값과 정확히 일치합니다.
        if (worldCam.orthographic && worldCam.orthographicSize > 0.001f)
        {
            float canvasHeight = canvasRect.rect.height;
            if (canvasHeight <= 1f)
            {
                CanvasScaler scaler = parentCanvas.GetComponent<CanvasScaler>();
                canvasHeight = scaler != null ? scaler.referenceResolution.y : 1080f;
            }

            float pixelsPerWorldUnit = canvasHeight / (worldCam.orthographicSize * 2f);
            Vector3 camPos = worldCam.transform.position;

            float localX = (unitBaseWorldPos.x - camPos.x) * pixelsPerWorldUnit;
            float localY = (unitBaseWorldPos.y - camPos.y) * pixelsPerWorldUnit;

            rootRect.anchoredPosition = new Vector2(localX, localY);
            rootRect.localPosition = new Vector3(localX, localY, 0f);
            return;
        }

        Camera uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCam;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCam, unitBaseWorldPos);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCam, out Vector2 localPt))
        {
            rootRect.anchoredPosition = localPt;
            rootRect.localPosition = new Vector3(localPt.x, localPt.y, 0f);
        }
    }

    /// <summary>
    /// 현재 체력/최대 체력 및 버프(단일 깃털 + 우상단 제곱 숫자)를 UI에 즉시 반영합니다.
    /// </summary>
    public void RefreshStatus(float currentHp, float maxHp, int buffCount)
    {
        EnsureUIHierarchy();

        float safeMax = Mathf.Max(1f, maxHp);
        float clampedHp = Mathf.Clamp(currentHp, 0f, safeMax);
        float ratio = Mathf.Clamp01(clampedHp / safeMax);

        if (hpFillRect != null)
        {
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = new Vector2(ratio, 1f);
            hpFillRect.offsetMin = Vector2.zero;
            hpFillRect.offsetMax = Vector2.zero;
        }

        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = ratio;
        }

        if (hpValueText != null)
        {
            int maxInt = Mathf.RoundToInt(safeMax);
            // 소수점(.5 등) 데미지가 있을 때는 소수 첫째 자리까지, 정수일 때는 정수로 깔끔하게 표시
            string curStr = Mathf.Approximately(clampedHp % 1f, 0f)
                ? Mathf.RoundToInt(clampedHp).ToString()
                : clampedHp.ToString("0.#");
            hpValueText.text = $"{curStr}/{maxInt}";
        }

        RefreshFeatherBuffDisplay(Mathf.Max(0, buffCount));
    }

    /// <summary>
    /// 버프가 1씩 늘어날 때마다 깃털 이미지를 여러 개 늘리지 않고,
    /// 단일 깃털 이미지 우측 상단에 숫자 제곱(지수)처럼 작게 버프 수치를 표기합니다.
    /// </summary>
    private void RefreshFeatherBuffDisplay(int count)
    {
        if (buffContainerRect == null) return;

        bool hasBuff = count > 0;
        buffContainerRect.gameObject.SetActive(hasBuff);

        if (!hasBuff) return;

        if (featherBuffSprite == null)
            featherBuffSprite = GetOrCreateFeatherSprite();

        if (singleFeatherImage != null)
        {
            singleFeatherImage.sprite = featherBuffSprite;
            singleFeatherImage.gameObject.SetActive(true);
        }

        if (buffExponentText != null)
        {
            buffExponentText.text = count.ToString();
            buffExponentText.gameObject.SetActive(true);
        }
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void EnsureUIHierarchy()
    {
        if (rootRect == null)
            rootRect = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = hpBarSize;
        rootRect.localScale = Vector3.one;

        // 1. 체력바 배경 (HPBarBg)
        Transform bgTr = rootRect.Find("HPBarBg");
        RectTransform bgRect;
        if (bgTr == null)
        {
            GameObject bgObj = new GameObject("HPBarBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            bgObj.layer = LayerMask.NameToLayer("UI");
            bgObj.transform.SetParent(rootRect, false);

            bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgRect.localScale = Vector3.one;
            bgRect.localPosition = Vector3.zero;

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.sprite = GetOrCreateSolidWhiteSprite();
            bgImg.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            bgImg.raycastTarget = false;

            Outline bgOutline = bgObj.GetComponent<Outline>();
            bgOutline.effectColor = new Color(0.22f, 0.27f, 0.34f, 0.95f);
            bgOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        else
        {
            bgRect = bgTr.GetComponent<RectTransform>();
        }

        // 2. 체력바 내부 패딩 영역 (HPFillArea) 및 빨간색 게이지 (HPFill)
        Transform fillAreaTr = bgRect.Find("HPFillArea");
        RectTransform fillAreaRect;
        if (fillAreaTr == null)
        {
            GameObject fillAreaObj = new GameObject("HPFillArea", typeof(RectTransform));
            fillAreaObj.layer = LayerMask.NameToLayer("UI");
            fillAreaObj.transform.SetParent(bgRect, false);

            fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);
            fillAreaRect.localScale = Vector3.one;
            fillAreaRect.localPosition = Vector3.zero;
        }
        else
        {
            fillAreaRect = fillAreaTr.GetComponent<RectTransform>();
        }

        if (hpFillRect == null || hpFillImage == null)
        {
            Transform fillTr = fillAreaRect.Find("HPFill");
            if (fillTr == null)
            {
                GameObject fillObj = new GameObject("HPFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObj.layer = LayerMask.NameToLayer("UI");
                fillObj.transform.SetParent(fillAreaRect, false);

                hpFillRect = fillObj.GetComponent<RectTransform>();
                hpFillRect.anchorMin = Vector2.zero;
                hpFillRect.anchorMax = Vector2.one;
                hpFillRect.pivot = new Vector2(0f, 0.5f);
                hpFillRect.offsetMin = Vector2.zero;
                hpFillRect.offsetMax = Vector2.zero;
                hpFillRect.localScale = Vector3.one;
                hpFillRect.localPosition = Vector3.zero;

                hpFillImage = fillObj.GetComponent<Image>();
                hpFillImage.sprite = GetOrCreateSolidWhiteSprite();
                hpFillImage.color = new Color(0.86f, 0.16f, 0.20f, 1f);
                hpFillImage.raycastTarget = false;
            }
            else
            {
                hpFillRect = fillTr.GetComponent<RectTransform>();
                hpFillImage = fillTr.GetComponent<Image>();
            }
        }

        // 3. 체력 수치 텍스트 (예: 250/250, 50/50)
        if (hpValueText == null)
        {
            Transform txtTr = bgRect.Find("HPValueText");
            if (txtTr == null)
            {
                GameObject txtObj = new GameObject("HPValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                txtObj.layer = LayerMask.NameToLayer("UI");
                txtObj.transform.SetParent(bgRect, false);

                RectTransform txtRect = txtObj.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;
                txtRect.localScale = Vector3.one;
                txtRect.localPosition = Vector3.zero;

                hpValueText = txtObj.GetComponent<Text>();
                hpValueText.font = GetOrCreateFont();
                hpValueText.fontSize = 15;
                hpValueText.fontStyle = FontStyle.Bold;
                hpValueText.alignment = TextAnchor.MiddleCenter;
                hpValueText.color = Color.white;
                hpValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                hpValueText.verticalOverflow = VerticalWrapMode.Overflow;
                hpValueText.raycastTarget = false;

                Outline txtOutline = txtObj.GetComponent<Outline>();
                txtOutline.effectColor = new Color(0.28f, 0.02f, 0.04f, 0.98f);
                txtOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                hpValueText = txtTr.GetComponent<Text>();
            }
        }

        // 4. 체력바 아래 왼쪽: 단일 깃털 이미지 + 우측 상단 제곱(지수) 위치에 버프 숫자 표시
        if (buffContainerRect == null)
        {
            Transform buffTr = rootRect.Find("BuffContainer");
            if (buffTr == null)
            {
                GameObject buffObj = new GameObject("BuffContainer", typeof(RectTransform));
                buffObj.layer = LayerMask.NameToLayer("UI");
                buffObj.transform.SetParent(rootRect, false);

                buffContainerRect = buffObj.GetComponent<RectTransform>();
                buffContainerRect.anchorMin = new Vector2(0f, 0f);
                buffContainerRect.anchorMax = new Vector2(1f, 0f);
                buffContainerRect.pivot = new Vector2(0f, 1f);
                buffContainerRect.anchoredPosition = new Vector2(0f, -4f);
                buffContainerRect.sizeDelta = new Vector2(0f, 28f);
                buffContainerRect.localScale = Vector3.one;
                buffContainerRect.localPosition = new Vector3(0f, -(hpBarSize.y * 0.5f) - 4f, 0f);
            }
            else
            {
                buffContainerRect = buffTr.GetComponent<RectTransform>();
                // 이전에 GridLayoutGroup이나 다중 깃털 아이콘이 생성되어 있었다면 정리
                GridLayoutGroup oldGrid = buffContainerRect.GetComponent<GridLayoutGroup>();
                if (oldGrid != null)
                {
                    if (Application.isPlaying) Destroy(oldGrid);
                    else DestroyImmediate(oldGrid);
                }
            }
        }

        if (buffContainerRect != null)
        {
            // 이전 방식의 다중 깃털 오브젝트(FeatherBuff_1, 2...)가 남아있다면 제거
            for (int i = buffContainerRect.childCount - 1; i >= 0; i--)
            {
                Transform child = buffContainerRect.GetChild(i);
                if (child.name.StartsWith("FeatherBuff_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            // 단일 깃털 아이콘 (FeatherBuffSingle) 생성 또는 연결
            if (singleFeatherImage == null)
            {
                Transform singleTr = buffContainerRect.Find("FeatherBuffSingle");
                if (singleTr == null)
                {
                    GameObject iconObj = new GameObject("FeatherBuffSingle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconObj.layer = LayerMask.NameToLayer("UI");
                    iconObj.transform.SetParent(buffContainerRect, false);

                    RectTransform rt = iconObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(2f, -2f);
                    rt.sizeDelta = featherIconSize;
                    rt.localScale = Vector3.one;

                    singleFeatherImage = iconObj.GetComponent<Image>();
                    singleFeatherImage.sprite = featherBuffSprite != null ? featherBuffSprite : GetOrCreateFeatherSprite();
                    singleFeatherImage.preserveAspect = true;
                    singleFeatherImage.raycastTarget = false;
                }
                else
                {
                    singleFeatherImage = singleTr.GetComponent<Image>();
                }
            }

            // 깃털 이미지 우측 상단에 숫자 제곱(지수)처럼 작게 표시되는 텍스트 (BuffExponentText)
            if (singleFeatherImage != null && buffExponentText == null)
            {
                Transform expTr = singleFeatherImage.transform.Find("BuffExponentText");
                if (expTr == null)
                {
                    GameObject expObj = new GameObject("BuffExponentText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                    expObj.layer = LayerMask.NameToLayer("UI");
                    expObj.transform.SetParent(singleFeatherImage.transform, false);

                    RectTransform expRect = expObj.GetComponent<RectTransform>();
                    // 깃털 이미지의 우측 상단 모서리(제곱 위치)에 배치
                    expRect.anchorMin = new Vector2(1f, 1f);
                    expRect.anchorMax = new Vector2(1f, 1f);
                    expRect.pivot = new Vector2(0.5f, 0.5f);
                    expRect.anchoredPosition = new Vector2(4f, 2f);
                    expRect.sizeDelta = new Vector2(26f, 18f);
                    expRect.localScale = Vector3.one;

                    buffExponentText = expObj.GetComponent<Text>();
                    buffExponentText.font = GetOrCreateFont();
                    buffExponentText.fontSize = 13;
                    buffExponentText.fontStyle = FontStyle.Bold;
                    buffExponentText.alignment = TextAnchor.UpperLeft;
                    buffExponentText.color = new Color(1f, 0.96f, 0.72f, 1f);
                    buffExponentText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    buffExponentText.verticalOverflow = VerticalWrapMode.Overflow;
                    buffExponentText.raycastTarget = false;

                    Outline expOutline = expObj.GetComponent<Outline>();
                    expOutline.effectColor = new Color(0.02f, 0.05f, 0.12f, 0.98f);
                    expOutline.effectDistance = new Vector2(1.4f, -1.4f);
                }
                else
                {
                    buffExponentText = expTr.GetComponent<Text>();
                }
            }
        }
    }

    private static Font GetOrCreateFont()
    {
        if (cachedFont != null) return cachedFont;

        string[] fontCandidates = { "Malgun Gothic", "맑은 고딕", "Arial" };
        cachedFont = Font.CreateDynamicFontFromOSFont(fontCandidates, 16);
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedFont;
    }

    public static Sprite GetOrCreateSolidWhiteSprite()
    {
        if (cachedSolidWhiteSprite != null) return cachedSolidWhiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16];
        for (int i = 0; i < 16; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();

        cachedSolidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        cachedSolidWhiteSprite.name = "ui_solid_white";
        return cachedSolidWhiteSprite;
    }

    public static Sprite GetOrCreateFeatherSprite()
    {
        if (cachedFeatherSprite != null) return cachedFeatherSprite;

        Texture2D tex = CreateFeatherTexture(64, 64);
        cachedFeatherSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        cachedFeatherSprite.name = "icon_buff_feather";
        return cachedFeatherSprite;
    }

    public static Texture2D CreateFeatherTexture(int width = 64, int height = 64)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];

        Color outlineDark = new Color(0.05f, 0.12f, 0.22f, 0.95f);
        Color vaneBase = new Color(0.22f, 0.78f, 1.0f, 1.0f);
        Color vaneTip = new Color(0.70f, 0.96f, 1.0f, 1.0f);
        Color shaftColor = new Color(1.0f, 0.95f, 0.78f, 1.0f);

        for (int y = 0; y < height; y++)
        {
            float ny = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f) / width;

                float dx = nx - 0.14f;
                float dy = ny - 0.14f;

                float u = (dx + dy) * 0.7071f / 1.02f;
                float v = (dy - dx) * 0.7071f;

                float curveCenter = 0.035f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                float vr = v - curveCenter;

                Color c = Color.clear;

                if (u >= 0.0f && u <= 1.0f)
                {
                    float shaftHalfWidth = Mathf.Lerp(0.022f, 0.008f, u);
                    float distToShaft = Mathf.Abs(vr);

                    float vaneAlpha = 0f;
                    float edgeDist = 1f;

                    if (u >= 0.15f && u <= 0.97f)
                    {
                        float tVane = (u - 0.15f) / 0.82f;
                        float maxWidthPos = 0.19f * Mathf.Sin(Mathf.Pow(tVane, 0.75f) * Mathf.PI);
                        float maxWidthNeg = 0.14f * Mathf.Sin(Mathf.Pow(tVane, 0.80f) * Mathf.PI);

                        float notch1 = Mathf.Exp(-Mathf.Pow((tVane - 0.42f) / 0.035f, 2f)) * 0.045f;
                        float notch2 = Mathf.Exp(-Mathf.Pow((tVane - 0.68f) / 0.030f, 2f)) * 0.038f;
                        if (vr > 0f) maxWidthPos = Mathf.Max(0.01f, maxWidthPos - notch1);
                        else         maxWidthNeg = Mathf.Max(0.01f, maxWidthNeg - notch2);

                        float allowedWidth = vr >= 0f ? maxWidthPos : maxWidthNeg;
                        edgeDist = allowedWidth - Mathf.Abs(vr);

                        if (edgeDist >= -0.025f)
                        {
                            vaneAlpha = Mathf.Clamp01((edgeDist + 0.025f) / 0.035f);
                        }
                    }

                    float shaftAlpha = 0f;
                    if (u >= 0.02f && u <= 0.94f)
                    {
                        shaftAlpha = Mathf.Clamp01((shaftHalfWidth + 0.016f - distToShaft) / 0.018f);
                    }

                    if (vaneAlpha > 0f || shaftAlpha > 0f)
                    {
                        float tVane = Mathf.Clamp01((u - 0.15f) / 0.82f);
                        float barbStripe = 0.5f + 0.5f * Mathf.Sin((u * 42f - Mathf.Abs(vr) * 28f) * Mathf.PI);
                        Color bodyCol = Color.Lerp(vaneBase, vaneTip, Mathf.Clamp01(tVane * 0.75f + barbStripe * 0.25f));

                        float innerMask = Mathf.Clamp01(edgeDist / 0.025f);
                        Color featheredCol = Color.Lerp(outlineDark, bodyCol, innerMask);

                        if (distToShaft <= shaftHalfWidth + 0.008f && u >= 0.02f && u <= 0.94f)
                        {
                            float shaftCore = Mathf.Clamp01((shaftHalfWidth - distToShaft + 0.008f) / 0.012f);
                            featheredCol = Color.Lerp(featheredCol, shaftColor, shaftCore);
                        }

                        c = featheredCol;
                        c.a = Mathf.Max(vaneAlpha, shaftAlpha);
                    }
                }

                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
