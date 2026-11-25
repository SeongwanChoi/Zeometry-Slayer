using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 각 Player 오브젝트 위에 일렬로 배치되는 3종 스킬 버튼 UI 그룹을 관리합니다 (PDF 3~5페이지 명세):
/// - 스킬이 쿨타임이 아닐 때:
///   * 아이콘을 클릭 중(PointerDown)일 때 아이콘 크기가 조금 작아짐 (0.84배)
///   * 클릭 이벤트가 End(PointerUp)일 때 원래 크기로 돌아오며 FGO 스타일 스킬 설명/확인창(SkillConfirmModalUI) 표시
/// - 스킬이 쿨타임 중일 때:
///   * 스킬 아이콘을 어둡게 만들고, 아이콘 위에 남은 쿨타임 숫자를 표기
///   * 클릭 시 좌우로 진동(Shake)하도록 연출
/// </summary>
public class PlayerSkillUI : MonoBehaviour
{
    [Header("연결된 대상 플레이어 유닛")]
    [SerializeField] private SelectableUnit targetUnit;

    [Header("UI 그룹 및 버튼 참조 (왼쪽부터 1스킬, 2스킬, 3스킬)")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button attackUpButton;
    [SerializeField] private Button defenseUpButton;
    [SerializeField] private Button critDamageUpButton;
    [SerializeField] private Image attackUpImage;
    [SerializeField] private Image defenseUpImage;
    [SerializeField] private Image critDamageUpImage;
    [SerializeField] private TextMeshProUGUI attackCooldownText;
    [SerializeField] private TextMeshProUGUI defenseCooldownText;
    [SerializeField] private TextMeshProUGUI critCooldownText;

    [Header("스킬 아이콘 스프라이트 참조")]
    [SerializeField] private Sprite attackUpSprite;
    [SerializeField] private Sprite defenseUpSprite;
    [SerializeField] private Sprite critDamageUpSprite;

    [Header("위치 및 크기 설정")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.78f, 0f);
    [SerializeField] private Vector2 iconSize = new Vector2(38f, 38f);
    [SerializeField] private float iconSpacing = 8f;

    [Header("쿨타임 색상 설정")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color cooldownColor = new Color(0.26f, 0.26f, 0.30f, 0.96f);

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private static Sprite cachedAttackUpSprite;
    private static Sprite cachedDefenseUpSprite;
    private static Sprite cachedCritDamageUpSprite;

    private readonly Coroutine[] shakeCoroutines = new Coroutine[3];
    private readonly Vector2[] buttonBasePositions = new Vector2[3];

    public SelectableUnit TargetUnit => targetUnit;
    public CanvasGroup CanvasGroup => canvasGroup;

    public void Initialize(
        SelectableUnit unit,
        Sprite atkSprite = null,
        Sprite defSprite = null,
        Sprite critSprite = null)
    {
        targetUnit = unit;
        if (atkSprite != null) attackUpSprite = atkSprite;
        if (defSprite != null) defenseUpSprite = defSprite;
        if (critSprite != null) critDamageUpSprite = critSprite;

        EnsureUIHierarchy();
        BindButtonListeners();
        RefreshSkillButtons();
    }

    private void Awake()
    {
        EnsureUIHierarchy();
        BindButtonListeners();
    }

    private void Start()
    {
        EnsureUIHierarchy();
        BindButtonListeners();
        RefreshSkillButtons();
    }

    private void LateUpdate()
    {
        if (targetUnit == null)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            return;
        }
    }

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
    /// 각 스킬의 쿨타임 상태에 따라 어두운 색상 및 쿨타임 숫자를 갱신합니다.
    /// </summary>
    public void RefreshSkillButtons()
    {
        EnsureUIHierarchy();

        int cd1 = targetUnit != null ? targetUnit.GetSkillCooldown(ESkillType.SKILL_1) : 0;
        int cd2 = targetUnit != null ? targetUnit.GetSkillCooldown(ESkillType.SKILL_2) : 0;
        int cd3 = targetUnit != null ? targetUnit.GetSkillCooldown(ESkillType.SKILL_3) : 0;

        ApplyButtonVisualState(attackUpButton, attackUpImage, attackCooldownText, cd1);
        ApplyButtonVisualState(defenseUpButton, defenseUpImage, defenseCooldownText, cd2);
        ApplyButtonVisualState(critDamageUpButton, critDamageUpImage, critCooldownText, cd3);
    }

    private void ApplyButtonVisualState(Button btn, Image img, TextMeshProUGUI cdText, int cooldownRemaining)
    {
        bool isOnCooldown = cooldownRemaining > 0;

        if (img != null)
        {
            img.color = isOnCooldown ? cooldownColor : normalColor;
        }

        if (cdText != null)
        {
            if (isOnCooldown)
            {
                cdText.text = cooldownRemaining.ToString();
                cdText.gameObject.SetActive(true);
            }
            else
            {
                cdText.gameObject.SetActive(false);
            }
        }

        if (btn != null)
        {
            // 쿨타임 중에도 클릭 시 좌우 진동 연출이 동작하도록 interactable은 true로 유지하고 색상 변화를 직접 제어
            btn.interactable = true;
            btn.transition = Selectable.Transition.None;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void BindButtonListeners()
    {
        if (!Application.isPlaying) return;

        // 기존 Button.onClick은 비우고 SkillIconPointerHandler에서 PointerDown(축소) / PointerUp(복귀 + 모달 or 쿨타임 진동) 제어
        if (attackUpButton != null) attackUpButton.onClick.RemoveAllListeners();
        if (defenseUpButton != null) defenseUpButton.onClick.RemoveAllListeners();
        if (critDamageUpButton != null) critDamageUpButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// 스킬 아이콘 포인터 다운 시 호출: 쿨타임이 아니면 크기가 조금 작아짐
    /// </summary>
    public void HandleSkillPointerDown(ESkillType skillType, RectTransform btnRect)
    {
        if (targetUnit == null || targetUnit.IsDead) return;
        if (BattleManager.Ins != null && !BattleManager.Ins.IsPlayerInputAllowed) return;
        if (SkillConfirmModalUI.Ins != null && SkillConfirmModalUI.Ins.IsModalOpen) return;
        if (DeckViewerModalUI.Ins != null && DeckViewerModalUI.Ins.IsModalOpen) return;

        if (!targetUnit.IsSkillOnCooldown(skillType))
        {
            if (btnRect != null)
                btnRect.localScale = Vector3.one * 0.86f;
        }
    }

    /// <summary>
    /// 스킬 아이콘 포인터 업(End) 시 호출:
    /// - 쿨타임 중이면: 좌우로 진동(Shake) 연출
    /// - 쿨타임이 아니면: 원래 크기로 돌아오며 스킬 설명창(SkillConfirmModalUI) 표시
    /// </summary>
    public void HandleSkillPointerUp(ESkillType skillType, RectTransform btnRect, bool isPointerInside)
    {
        if (btnRect != null)
            btnRect.localScale = isPointerInside ? Vector3.one * SkillIconPointerReceiver.HoverScale : Vector3.one;

        if (!isPointerInside) return;
        if (targetUnit == null || targetUnit.IsDead) return;
        if (BattleManager.Ins != null && !BattleManager.Ins.IsPlayerInputAllowed) return;
        if (SkillConfirmModalUI.Ins != null && SkillConfirmModalUI.Ins.IsModalOpen) return;
        if (DeckViewerModalUI.Ins != null && DeckViewerModalUI.Ins.IsModalOpen) return;

        if (targetUnit.IsSkillOnCooldown(skillType))
        {
            // 쿨타임 중 클릭 시 좌우 진동 연출 + 알림음
            if (SoundManager.Ins != null)
                SoundManager.Ins.PlaySkillBlocked();

            TriggerCooldownShake((int)skillType, btnRect);
            return;
        }

        // 쿨타임이 아니라면 스킬 확인 모달창 열기
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        OpenConfirmModalForSkill(skillType);
    }

    private void OpenConfirmModalForSkill(ESkillType skillType)
    {
        if (targetUnit == null) return;

        PlayerSkillMetadata meta = targetUnit.GetSkillMetadata(skillType);
        bool canAfford = targetUnit.CanPaySkillResource(skillType, out string lackReason);
        Sprite iconSp = GetIconSpriteForSlot(skillType);

        if (SkillConfirmModalUI.Ins != null)
        {
            SkillConfirmModalUI.Ins.OpenSkillModal(
                targetUnit,
                skillType,
                iconSp,
                meta.skillName,
                meta.cooldownTurns,
                meta.description,
                canAfford,
                lackReason,
                () =>
                {
                    if (targetUnit != null)
                        targetUnit.TryUseSkill(skillType);
                }
            );
        }
        else
        {
            targetUnit.TryUseSkill(skillType);
        }
    }

    private Sprite GetIconSpriteForSlot(ESkillType skillType)
    {
        switch (skillType)
        {
            case ESkillType.SKILL_1: return attackUpSprite != null ? attackUpSprite : GetOrCreateSkillSprite(ESkillType.SKILL_1);
            case ESkillType.SKILL_2: return defenseUpSprite != null ? defenseUpSprite : GetOrCreateSkillSprite(ESkillType.SKILL_2);
            case ESkillType.SKILL_3: return critDamageUpSprite != null ? critDamageUpSprite : GetOrCreateSkillSprite(ESkillType.SKILL_3);
            default:                 return attackUpSprite;
        }
    }

    private void TriggerCooldownShake(int slotIdx, RectTransform btnRect)
    {
        if (btnRect == null || slotIdx < 0 || slotIdx >= 3) return;

        if (shakeCoroutines[slotIdx] != null)
        {
            StopCoroutine(shakeCoroutines[slotIdx]);
            btnRect.anchoredPosition = buttonBasePositions[slotIdx];
        }

        shakeCoroutines[slotIdx] = StartCoroutine(CoShakeCooldownIcon(slotIdx, btnRect));
    }

    private IEnumerator CoShakeCooldownIcon(int slotIdx, RectTransform btnRect)
    {
        Vector2 basePos = buttonBasePositions[slotIdx];
        float duration = 0.28f;
        float elapsed = 0f;
        float magnitude = 6.5f;
        float frequency = 52f;

        while (elapsed < duration)
        {
            if (btnRect == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float damper = 1f - t;
            float offsetX = Mathf.Sin(elapsed * frequency) * magnitude * damper;

            btnRect.anchoredPosition = basePos + new Vector2(offsetX, 0f);
            yield return null;
        }

        if (btnRect != null)
            btnRect.anchoredPosition = basePos;

        shakeCoroutines[slotIdx] = null;
    }

    public void OnClickAttackUp() => OpenConfirmModalForSkill(ESkillType.SKILL_1);
    public void OnClickDefenseUp() => OpenConfirmModalForSkill(ESkillType.SKILL_2);
    public void OnClickCritDamageUp() => OpenConfirmModalForSkill(ESkillType.SKILL_3);

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

        float totalWidth = (iconSize.x * 3f) + (iconSpacing * 2f);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(totalWidth, iconSize.y);
        rootRect.localScale = Vector3.one;

        if (attackUpSprite == null)
            attackUpSprite = GetOrCreateSkillSprite(ESkillType.SKILL_1);
        if (defenseUpSprite == null)
            defenseUpSprite = GetOrCreateSkillSprite(ESkillType.SKILL_2);
        if (critDamageUpSprite == null)
            critDamageUpSprite = GetOrCreateSkillSprite(ESkillType.SKILL_3);

        float leftX = -(iconSize.x + iconSpacing);
        float centerX = 0f;
        float rightX = +(iconSize.x + iconSpacing);

        buttonBasePositions[0] = new Vector2(leftX, 0f);
        buttonBasePositions[1] = new Vector2(centerX, 0f);
        buttonBasePositions[2] = new Vector2(rightX, 0f);

        EnsureSingleSkillButton(
            "SkillBtn_AttackUp",
            ESkillType.SKILL_1,
            leftX,
            attackUpSprite,
            ref attackUpButton,
            ref attackUpImage,
            ref attackCooldownText
        );

        EnsureSingleSkillButton(
            "SkillBtn_DefenseUp",
            ESkillType.SKILL_2,
            centerX,
            defenseUpSprite,
            ref defenseUpButton,
            ref defenseUpImage,
            ref defenseCooldownText
        );

        EnsureSingleSkillButton(
            "SkillBtn_CritDamageUp",
            ESkillType.SKILL_3,
            rightX,
            critDamageUpSprite,
            ref critDamageUpButton,
            ref critDamageUpImage,
            ref critCooldownText
        );
    }

    private void EnsureSingleSkillButton(
        string buttonName,
        ESkillType skillType,
        float anchoredX,
        Sprite iconSprite,
        ref Button btnRef,
        ref Image imgRef,
        ref TextMeshProUGUI cdTextRef)
    {
        Transform btnTr = rootRect.Find(buttonName);
        GameObject btnObj;
        if (btnTr == null)
        {
            btnObj = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.layer = LayerMask.NameToLayer("UI");
            btnObj.transform.SetParent(rootRect, false);
        }
        else
        {
            btnObj = btnTr.gameObject;
        }

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        UIHelper.ConfigureCenteredRect(rt, new Vector2(anchoredX, 0f), iconSize);

        imgRef = btnObj.GetComponent<Image>();
        if (imgRef == null)
            imgRef = btnObj.AddComponent<Image>();
        if (iconSprite != null)
            imgRef.sprite = iconSprite;
        imgRef.preserveAspect = true;
        imgRef.raycastTarget = true;

        btnRef = btnObj.GetComponent<Button>();
        if (btnRef == null)
            btnRef = btnObj.AddComponent<Button>();
        btnRef.targetGraphic = imgRef;
        btnRef.transition = Selectable.Transition.None;

        Navigation nav = btnRef.navigation;
        nav.mode = Navigation.Mode.None;
        btnRef.navigation = nav;

        // 포인터 다운/업 인터랙션 리시버 부착
        SkillIconPointerReceiver receiver = btnObj.GetComponent<SkillIconPointerReceiver>();
        if (receiver == null)
            receiver = btnObj.AddComponent<SkillIconPointerReceiver>();
        receiver.Configure(this, skillType, rt);

        // 쿨타임 숫자 텍스트(CooldownText) 생성/연결
        Transform cdTr = btnObj.transform.Find("CooldownText");
        if (cdTr == null)
        {
            cdTextRef = UIHelper.CreateOutlinedText(
                btnObj.transform,
                "CooldownText",
                "0",
                20,
                FontStyle.Bold,
                new Color(1f, 0.95f, 0.75f, 1f),
                Vector2.zero,
                iconSize,
                TextAnchor.MiddleCenter,
                new Color(0.02f, 0.02f, 0.05f, 0.98f),
                new Vector2(1.8f, -1.8f)
            );
            cdTextRef.gameObject.SetActive(false);
        }
        else
        {
            cdTextRef = UIHelper.GetOrAddTMPText(cdTr.gameObject);
        }
    }

    public static Sprite GetOrCreateSkillSprite(ESkillType skillType)
    {
        switch (skillType)
        {
            case ESkillType.SKILL_1:
                if (cachedAttackUpSprite == null)
                    cachedAttackUpSprite = LoadSkillSpriteFromDisk("Assets/Resource/UI/icon_skill_attack_up.png", "icon_skill_attack_up");
                return cachedAttackUpSprite;

            case ESkillType.SKILL_2:
                if (cachedDefenseUpSprite == null)
                    cachedDefenseUpSprite = LoadSkillSpriteFromDisk("Assets/Resource/UI/icon_skill_defense_up.png", "icon_skill_defense_up");
                return cachedDefenseUpSprite;

            case ESkillType.SKILL_3:
                if (cachedCritDamageUpSprite == null)
                    cachedCritDamageUpSprite = LoadSkillSpriteFromDisk("Assets/Resource/UI/icon_skill_crit_damage_up.png", "icon_skill_crit_damage_up");
                return cachedCritDamageUpSprite;

            default:
                return null;
        }
    }

    private static Sprite LoadSkillSpriteFromDisk(string relativeAssetPath, string spriteName)
    {
        return UIHelper.LoadSpriteFromAssetOrDisk(
            relativeAssetPath,
            spriteName,
            UIHelper.GetOrCreateSolidWhiteSprite()
        );
    }
}
