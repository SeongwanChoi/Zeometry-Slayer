using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 각 Player 및 Enemy 오브젝트 아래에 표시되는 체력바(Unity 자체 UI)와
/// 체력바 아래에 표시되는 버프 아이콘(적의 FeatherBuffSingle 및 플레이어의 스킬 버프 3종) +
/// 각 버프 이미지의 우측 하단에 표시되는 중첩 수치를 관리합니다.
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
    [SerializeField] private RectTransform hpDamagePreviewRect;
    [SerializeField] private Image hpDamagePreviewImage;
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private TextMeshProUGUI hpDamagePreviewText;

    [Header("버프(단일 아이콘 + 우측 하단 중첩 숫자) UI 참조")]
    [SerializeField] private RectTransform buffContainerRect;
    [SerializeField] private Image singleFeatherImage;
    [SerializeField] private TextMeshProUGUI buffExponentText;
    [SerializeField] private Sprite featherBuffSprite;

    [Header("플레이어 스킬 버프 아이콘 UI 참조")]
    [SerializeField] private Image attackBuffIconImage;
    [SerializeField] private TextMeshProUGUI attackBuffCountText;
    [SerializeField] private Image defenseBuffIconImage;
    [SerializeField] private TextMeshProUGUI defenseBuffCountText;
    [SerializeField] private Image critBuffIconImage;
    [SerializeField] private TextMeshProUGUI critBuffCountText;
    [SerializeField] private Sprite attackBuffSprite;
    [SerializeField] private Sprite defenseBuffSprite;
    [SerializeField] private Sprite critBuffSprite;

    [Header("위치 및 크기 설정")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, -0.78f, 0f);
    [SerializeField] private Vector2 hpBarSize = new Vector2(142f, 22f);
    [SerializeField] private Vector2 featherIconSize = new Vector2(24f, 24f);

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private bool isPreviewingDamage = false;
    private float previewDamageAmount = 0f;

    private static Sprite cachedFeatherSprite;

    public CanvasGroup CanvasGroup => canvasGroup;

    public void Initialize(
        SelectableUnit unit,
        Sprite customFeatherSprite = null,
        Sprite atkSkillSprite = null,
        Sprite defSkillSprite = null,
        Sprite critSkillSprite = null)
    {
        targetUnit = unit;
        if (customFeatherSprite != null)
            featherBuffSprite = customFeatherSprite;
        if (atkSkillSprite != null)
            attackBuffSprite = atkSkillSprite;
        if (defSkillSprite != null)
            defenseBuffSprite = defSkillSprite;
        if (critSkillSprite != null)
            critBuffSprite = critSkillSprite;

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
        EnforceBuffContainerLeftZero();
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

        UIHelper.SyncRectTransformToWorldPosition(
            rootRect,
            parentCanvas,
            canvasRect,
            targetUnit.WorldBasePosition + worldOffset
        );
    }

    /// <summary>
    /// 공격 카드로 이 적을 타겟팅 중일 때(EnemyTargetBrackets 활성화 시)
    /// 현재 체력 게이지의 오른쪽에서부터 줄어들 예상 피해량을 짙은 붉은색 게이지와 텍스트 수치로 미리 표시합니다.
    /// </summary>
    public void SetDamagePreview(float previewDamage)
    {
        bool shouldPreview = previewDamage > 0.001f;
        if (isPreviewingDamage == shouldPreview && Mathf.Approximately(previewDamageAmount, previewDamage))
            return;

        isPreviewingDamage = shouldPreview;
        previewDamageAmount = Mathf.Max(0f, previewDamage);

        if (targetUnit != null)
        {
            RefreshStatus(targetUnit.CurrentHp, targetUnit.MaxHp, targetUnit.BuffCount);
        }
    }

    public void ClearDamagePreview()
    {
        if (!isPreviewingDamage && previewDamageAmount <= 0f)
            return;

        isPreviewingDamage = false;
        previewDamageAmount = 0f;

        if (targetUnit != null)
        {
            RefreshStatus(targetUnit.CurrentHp, targetUnit.MaxHp, targetUnit.BuffCount);
        }
        else
        {
            if (hpDamagePreviewRect != null) hpDamagePreviewRect.gameObject.SetActive(false);
            if (hpDamagePreviewText != null) hpDamagePreviewText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 체력/최대 체력 및 버프(단일 아이콘 + 우측 하단 중첩 숫자)를 UI에 즉시 반영합니다.
    /// </summary>
    public void RefreshStatus(float currentHp, float maxHp, int buffCount)
    {
        EnsureUIHierarchy();

        float safeMax = Mathf.Max(1f, maxHp);
        float clampedHp = Mathf.Clamp(currentHp, 0f, safeMax);
        float currentRatio = Mathf.Clamp01(clampedHp / safeMax);

        bool showDamagePreview = isPreviewingDamage && previewDamageAmount > 0.001f && clampedHp > 0f;
        float afterHp = showDamagePreview ? Mathf.Clamp(clampedHp - previewDamageAmount, 0f, safeMax) : clampedHp;
        float afterRatio = Mathf.Clamp01(afterHp / safeMax);

        if (hpFillRect != null)
        {
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = new Vector2(showDamagePreview ? afterRatio : currentRatio, 1f);
            hpFillRect.offsetMin = Vector2.zero;
            hpFillRect.offsetMax = Vector2.zero;
        }

        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = showDamagePreview ? afterRatio : currentRatio;
        }

        // 오른쪽에서부터 줄어들 체력 구간(afterRatio ~ currentRatio)을 짙은 붉은색으로 미리 표시
        if (hpDamagePreviewRect != null)
        {
            if (showDamagePreview && currentRatio > afterRatio)
            {
                hpDamagePreviewRect.gameObject.SetActive(true);
                hpDamagePreviewRect.anchorMin = new Vector2(afterRatio, 0f);
                hpDamagePreviewRect.anchorMax = new Vector2(currentRatio, 1f);
                hpDamagePreviewRect.offsetMin = Vector2.zero;
                hpDamagePreviewRect.offsetMax = Vector2.zero;

                if (hpDamagePreviewImage != null)
                {
                    hpDamagePreviewImage.color = new Color(0.38f, 0.02f, 0.05f, 0.98f);
                }
            }
            else
            {
                hpDamagePreviewRect.gameObject.SetActive(false);
            }
        }

        if (hpValueText != null)
        {
            int maxInt = Mathf.RoundToInt(safeMax);
            if (showDamagePreview)
            {
                string afterStr = UIHelper.FormatCompactNumber(afterHp);
                string dmgStr = UIHelper.FormatCompactNumber(previewDamageAmount);
                hpValueText.richText = true;
                hpValueText.text = $"{afterStr}/{maxInt} <color=#FF7A7A>(-{dmgStr})</color>";
            }
            else
            {
                string curStr = UIHelper.FormatCompactNumber(clampedHp);
                hpValueText.text = $"{curStr}/{maxInt}";
            }
        }

        if (hpDamagePreviewText != null)
        {
            if (showDamagePreview)
            {
                string dmgStr = UIHelper.FormatCompactNumber(previewDamageAmount);
                hpDamagePreviewText.gameObject.SetActive(true);
                hpDamagePreviewText.text = $"-{dmgStr}";

                // 짙은 붉은색 감소 구간 바로 위에 수치 텍스트 배치
                float midRatio = (afterRatio + currentRatio) * 0.5f;
                float offsetX = (midRatio - 0.5f) * hpBarSize.x;
                hpDamagePreviewText.rectTransform.anchoredPosition = new Vector2(offsetX, 19f);
            }
            else
            {
                hpDamagePreviewText.gameObject.SetActive(false);
            }
        }

        int atkStacks = targetUnit != null ? targetUnit.GetSkillBuffStacks(ESkillType.ATTACK_UP) : 0;
        int defStacks = targetUnit != null ? targetUnit.GetSkillBuffStacks(ESkillType.DEFENSE_UP) : 0;
        int critStacks = targetUnit != null ? targetUnit.GetSkillBuffStacks(ESkillType.CRIT_DAMAGE_UP) : 0;

        RefreshAllBuffDisplays(Mathf.Max(0, buffCount), atkStacks, defStacks, critStacks);
    }

    /// <summary>
    /// 적의 FeatherBuffSingle 및 플레이어의 스킬 버프 아이콘들을 체력바 아래에 나란히 표시하고,
    /// 각 버프 이미지의 우측 하단에 중첩 숫자를 표기합니다.
    /// </summary>
    private void RefreshAllBuffDisplays(int featherCount, int atkStacks, int defStacks, int critStacks)
    {
        if (buffContainerRect == null) return;

        EnforceBuffContainerLeftZero();

        bool hasFeather = featherCount > 0;
        bool hasAtk = atkStacks > 0;
        bool hasDef = defStacks > 0;
        bool hasCrit = critStacks > 0;
        bool hasAnyBuff = hasFeather || hasAtk || hasDef || hasCrit;

        buffContainerRect.gameObject.SetActive(hasAnyBuff);
        if (!hasAnyBuff)
        {
            if (singleFeatherImage != null) singleFeatherImage.gameObject.SetActive(false);
            if (attackBuffIconImage != null) attackBuffIconImage.gameObject.SetActive(false);
            if (defenseBuffIconImage != null) defenseBuffIconImage.gameObject.SetActive(false);
            if (critBuffIconImage != null) critBuffIconImage.gameObject.SetActive(false);
            return;
        }

        int slotIndex = 0;
        float stepX = featherIconSize.x + 8f;

        // 1. 깃털 버프 (FeatherBuffSingle)
        if (featherBuffSprite == null)
            featherBuffSprite = GetOrCreateFeatherSprite();

        UpdateSingleBuffSlot(
            singleFeatherImage,
            buffExponentText,
            featherBuffSprite,
            featherCount,
            ref slotIndex,
            stepX
        );

        // 2. 공격력 업 버프 (SkillBuff_AttackUp)
        if (attackBuffSprite == null)
            attackBuffSprite = PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.ATTACK_UP);

        UpdateSingleBuffSlot(
            attackBuffIconImage,
            attackBuffCountText,
            attackBuffSprite,
            atkStacks,
            ref slotIndex,
            stepX
        );

        // 3. 방어력 업 버프 (SkillBuff_DefenseUp)
        if (defenseBuffSprite == null)
            defenseBuffSprite = PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.DEFENSE_UP);

        UpdateSingleBuffSlot(
            defenseBuffIconImage,
            defenseBuffCountText,
            defenseBuffSprite,
            defStacks,
            ref slotIndex,
            stepX
        );

        // 4. 크리티컬 위력 업 버프 (SkillBuff_CritDamageUp)
        if (critBuffSprite == null)
            critBuffSprite = PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.CRIT_DAMAGE_UP);

        UpdateSingleBuffSlot(
            critBuffIconImage,
            critBuffCountText,
            critBuffSprite,
            critStacks,
            ref slotIndex,
            stepX
        );
    }

    private void UpdateSingleBuffSlot(
        Image iconImg,
        TextMeshProUGUI countTxt,
        Sprite sprite,
        int stackCount,
        ref int slotIndex,
        float stepX)
    {
        if (iconImg == null) return;

        if (stackCount <= 0)
        {
            iconImg.gameObject.SetActive(false);
            return;
        }

        iconImg.sprite = sprite;
        iconImg.gameObject.SetActive(true);

        RectTransform rt = iconImg.GetComponent<RectTransform>();
        if (rt != null)
        {
            float posX = 2f + (slotIndex * stepX);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(posX, -2f);
            rt.sizeDelta = featherIconSize;
            rt.localScale = Vector3.one;
        }

        if (countTxt != null)
        {
            ApplyBottomRightCountTextLayout(countTxt);
            countTxt.text = stackCount.ToString();
            countTxt.gameObject.SetActive(true);
        }

        slotIndex++;
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

        // 2-1. 타겟팅 시 오른쪽에서부터 줄어들 체력을 미리 보여주는 짙은 붉은색 프리뷰 게이지 (HPDamagePreviewFill)
        if (hpDamagePreviewRect == null || hpDamagePreviewImage == null)
        {
            Transform prevTr = fillAreaRect.Find("HPDamagePreviewFill");
            if (prevTr == null)
            {
                GameObject prevObj = new GameObject("HPDamagePreviewFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                prevObj.layer = LayerMask.NameToLayer("UI");
                prevObj.transform.SetParent(fillAreaRect, false);

                hpDamagePreviewRect = prevObj.GetComponent<RectTransform>();
                hpDamagePreviewRect.anchorMin = new Vector2(1f, 0f);
                hpDamagePreviewRect.anchorMax = new Vector2(1f, 1f);
                hpDamagePreviewRect.pivot = new Vector2(1f, 0.5f);
                hpDamagePreviewRect.offsetMin = Vector2.zero;
                hpDamagePreviewRect.offsetMax = Vector2.zero;
                hpDamagePreviewRect.localScale = Vector3.one;
                hpDamagePreviewRect.localPosition = Vector3.zero;

                hpDamagePreviewImage = prevObj.GetComponent<Image>();
                hpDamagePreviewImage.sprite = GetOrCreateSolidWhiteSprite();
                hpDamagePreviewImage.color = new Color(0.38f, 0.02f, 0.05f, 0.98f); // 짙은 붉은색
                hpDamagePreviewImage.raycastTarget = false;

                prevObj.SetActive(false);
            }
            else
            {
                hpDamagePreviewRect = prevTr.GetComponent<RectTransform>();
                hpDamagePreviewImage = prevTr.GetComponent<Image>();
            }
        }

        // 3. 체력 수치 텍스트 (예: 250/250, 50/50) - 항상 HP바 정중앙 정렬 보장
        if (hpValueText == null)
        {
            Transform txtTr = bgRect.Find("HPValueText");
            if (txtTr == null)
            {
                GameObject txtObj = new GameObject("HPValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtObj.layer = LayerMask.NameToLayer("UI");
                txtObj.transform.SetParent(bgRect, false);
                hpValueText = txtObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                hpValueText = UIHelper.GetOrAddTMPText(txtTr.gameObject);
            }
        }

        if (hpValueText != null)
        {
            RectTransform txtRect = hpValueText.rectTransform;
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.pivot = new Vector2(0.5f, 0.5f);
            txtRect.anchoredPosition = Vector2.zero;
            txtRect.sizeDelta = Vector2.zero;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            txtRect.localScale = Vector3.one;
            txtRect.localPosition = Vector3.zero;

            hpValueText.font = GetOrCreateFont();
            hpValueText.fontSize = 15;
            hpValueText.fontStyle = FontStyles.Bold;
            hpValueText.alignment = TextAlignmentOptions.Midline;
            hpValueText.color = Color.white;
            hpValueText.textWrappingMode = TextWrappingModes.NoWrap;
            hpValueText.overflowMode = TextOverflowModes.Overflow;
            hpValueText.raycastTarget = false;
        }

        // 3-1. 타겟팅 시 짙은 붉은색 감소 게이지 구간 바로 위에 표시되는 예상 피해량 수치 텍스트 (HPDamagePreviewText)
        if (hpDamagePreviewText == null)
        {
            Transform prevTxtTr = bgRect.Find("HPDamagePreviewText");
            if (prevTxtTr == null)
            {
                hpDamagePreviewText = UIHelper.CreateOutlinedText(
                    bgRect,
                    "HPDamagePreviewText",
                    "-10",
                    15,
                    FontStyle.Bold,
                    new Color(1.0f, 0.35f, 0.38f, 1f),
                    new Vector2(0f, 19f),
                    new Vector2(90f, 22f),
                    TextAnchor.MiddleCenter,
                    new Color(0.12f, 0.01f, 0.02f, 0.98f),
                    new Vector2(1.5f, -1.5f)
                );
                hpDamagePreviewText.gameObject.SetActive(false);
            }
            else
            {
                hpDamagePreviewText = UIHelper.GetOrAddTMPText(prevTxtTr.gameObject);
            }
        }

        // 4. 체력바 아래 버프 컨테이너: 적 FeatherBuffSingle 및 플레이어 스킬 버프 3종 (중첩 숫자는 모두 우측 하단 통일)
        if (buffContainerRect == null)
        {
            Transform buffTr = rootRect.Find("BuffContainer");
            if (buffTr == null)
            {
                GameObject buffObj = new GameObject("BuffContainer", typeof(RectTransform));
                buffObj.layer = LayerMask.NameToLayer("UI");
                buffObj.transform.SetParent(rootRect, false);
                buffContainerRect = buffObj.GetComponent<RectTransform>();
            }
            else
            {
                buffContainerRect = buffTr.GetComponent<RectTransform>();
            }
        }

        if (buffContainerRect != null)
        {
            GridLayoutGroup oldGrid = buffContainerRect.GetComponent<GridLayoutGroup>();
            if (oldGrid != null)
            {
                if (Application.isPlaying) Destroy(oldGrid);
                else DestroyImmediate(oldGrid);
            }

            EnforceBuffContainerLeftZero();

            for (int i = buffContainerRect.childCount - 1; i >= 0; i--)
            {
                Transform child = buffContainerRect.GetChild(i);
                if (child.name.StartsWith("FeatherBuff_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            // 4-1. 적/공통 깃털 버프 아이콘 (FeatherBuffSingle)
            EnsureBuffIconWithCountText(
                "FeatherBuffSingle",
                "BuffExponentText",
                featherBuffSprite != null ? featherBuffSprite : GetOrCreateFeatherSprite(),
                2f,
                ref singleFeatherImage,
                ref buffExponentText
            );

            // 4-2. 플레이어 스킬 버프 3종 아이콘 (HP바 아래에 FeatherBuffSingle처럼 작게 표시 + 우측 하단 중첩 숫자)
            EnsureBuffIconWithCountText(
                "SkillBuff_AttackUp",
                "BuffExponentText",
                attackBuffSprite != null ? attackBuffSprite : PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.ATTACK_UP),
                2f,
                ref attackBuffIconImage,
                ref attackBuffCountText
            );

            EnsureBuffIconWithCountText(
                "SkillBuff_DefenseUp",
                "BuffExponentText",
                defenseBuffSprite != null ? defenseBuffSprite : PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.DEFENSE_UP),
                31f,
                ref defenseBuffIconImage,
                ref defenseBuffCountText
            );

            EnsureBuffIconWithCountText(
                "SkillBuff_CritDamageUp",
                "BuffExponentText",
                critBuffSprite != null ? critBuffSprite : PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.CRIT_DAMAGE_UP),
                60f,
                ref critBuffIconImage,
                ref critBuffCountText
            );
        }
    }

    /// <summary>
    /// 플레이어와 적의 BuffContainer RectTransform의 Left(offsetMin.x) 값을 항상 0으로 고정합니다.
    /// </summary>
    private void EnforceBuffContainerLeftZero()
    {
        if (buffContainerRect == null) return;

        buffContainerRect.anchorMin = new Vector2(0f, 0f);
        buffContainerRect.anchorMax = new Vector2(1f, 0f);
        buffContainerRect.pivot = new Vector2(0f, 1f);
        buffContainerRect.anchoredPosition = new Vector2(0f, -4f);
        buffContainerRect.sizeDelta = new Vector2(0f, 28f);
        buffContainerRect.offsetMin = new Vector2(0f, -32f);
        buffContainerRect.offsetMax = new Vector2(0f, -4f);
        buffContainerRect.localScale = Vector3.one;
    }

    private void EnsureBuffIconWithCountText(
        string iconObjectName,
        string textObjectName,
        Sprite defaultSprite,
        float defaultPosX,
        ref Image iconImgRef,
        ref TextMeshProUGUI countTxtRef)
    {
        if (iconImgRef == null)
        {
            Transform existingIconTr = buffContainerRect.Find(iconObjectName);
            if (existingIconTr == null)
            {
                GameObject iconObj = new GameObject(iconObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.layer = LayerMask.NameToLayer("UI");
                iconObj.transform.SetParent(buffContainerRect, false);

                RectTransform rt = iconObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(defaultPosX, -2f);
                rt.sizeDelta = featherIconSize;
                rt.localScale = Vector3.one;

                iconImgRef = iconObj.GetComponent<Image>();
                iconImgRef.sprite = defaultSprite;
                iconImgRef.preserveAspect = true;
                iconImgRef.raycastTarget = false;
            }
            else
            {
                iconImgRef = existingIconTr.GetComponent<Image>();
            }
        }

        if (iconImgRef != null)
        {
            if (countTxtRef == null)
            {
                Transform expTr = iconImgRef.transform.Find(textObjectName);
                if (expTr == null)
                {
                    GameObject expObj = new GameObject(textObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    expObj.layer = LayerMask.NameToLayer("UI");
                    expObj.transform.SetParent(iconImgRef.transform, false);
                    countTxtRef = expObj.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    countTxtRef = UIHelper.GetOrAddTMPText(expTr.gameObject);
                }
            }

            if (countTxtRef != null)
            {
                ApplyBottomRightCountTextLayout(countTxtRef);
            }
        }
    }

    /// <summary>
    /// 버프 중첩 숫자를 버프 이미지의 우측 하단(Bottom-Right)에 통일된 규칙으로 배치합니다.
    /// </summary>
    private void ApplyBottomRightCountTextLayout(TextMeshProUGUI txt)
    {
        if (txt == null) return;

        RectTransform expRect = txt.GetComponent<RectTransform>();
        if (expRect != null)
        {
            expRect.anchorMin = new Vector2(1f, 0f);
            expRect.anchorMax = new Vector2(1f, 0f);
            expRect.pivot = new Vector2(1f, 0f);
            expRect.anchoredPosition = new Vector2(5f, -3f);
            expRect.sizeDelta = new Vector2(26f, 18f);
            expRect.localScale = Vector3.one;
            expRect.localPosition = new Vector3((featherIconSize.x * 0.5f) + 5f, -(featherIconSize.y * 0.5f) - 3f, 0f);
        }

        txt.font = GetOrCreateFont();
        txt.fontSize = 13;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.BottomRight;
        txt.color = new Color(1f, 0.96f, 0.72f, 1f);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.raycastTarget = false;
    }

    private static TMP_FontAsset GetOrCreateFont() => UIHelper.GetOrCreateTMPFont();

    public static Sprite GetOrCreateSolidWhiteSprite() => UIHelper.GetOrCreateSolidWhiteSprite();

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
