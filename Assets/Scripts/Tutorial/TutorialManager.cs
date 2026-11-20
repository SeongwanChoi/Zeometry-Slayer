using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zeometry Slayer 전투 리메이크 기준 튜토리얼 컨트롤러:
/// 1) 설명할 오브젝트/UI만 원래 밝기로 유지(Spotlight)하고 주변을 어둡게(Dimmed) 처리
/// 2) 리메이크된 덱(5~15장)/손패(최대 10장)/버린카드더미, 3종 카드 규칙(Arts/Quick/Buster & SuperBuster),
///    플레이어 고유 스킬 및 FGO 스타일 스킬 확인 모달, Spline 타겟팅 화살표 공격 방식을 단계별로 안내합니다.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Ins { get; private set; }

    private const int TotalTutorialSteps = 6;

    [Header("스프라이트 리소스")]
    [SerializeField] private Sprite playerTriangleSprite;
    [SerializeField] private Sprite playerPentagonSprite;
    [SerializeField] private Sprite enemySquareSprite;
    [SerializeField] private Sprite enemyFeatherBuffSprite;
    [SerializeField] private Sprite cardRedSprite;
    [SerializeField] private Sprite cardBlueSprite;
    [SerializeField] private Sprite cardGreenSprite;
    [SerializeField] private Sprite skillAttackUpSprite;
    [SerializeField] private Sprite skillDefenseUpSprite;
    [SerializeField] private Sprite skillCritDamageUpSprite;

    private Canvas tutorialCanvas;
    private RectTransform canvasRect;

    private RectTransform topDim;
    private RectTransform bottomDim;
    private RectTransform leftDim;
    private RectTransform rightDim;
    private RectTransform spotlightBorder;
    private Image spotlightBorderImg;
    private readonly List<RectTransform> extraDimPanels = new List<RectTransform>();
    private readonly List<RectTransform> extraSpotlightBorders = new List<RectTransform>();
    private readonly List<Image> extraSpotlightBorderImgs = new List<Image>();
    private Sprite cachedBorderSprite;

    private RectTransform rightGuidePanel;
    private CanvasGroup rightGuideCg;
    private TextMeshProUGUI stepBadgeText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI hintText;
    private Button nextStepBtn;
    private RectTransform imageContainer;
    private readonly List<Image> displayedImages = new List<Image>();

    private int currentStep = 0;
    private bool isStepWaiting = false;
    private bool stepActionCompleted = false;
    private bool isSpotlightActive = false;
    private readonly HashSet<int> shownSteps = new HashSet<int>();
    private bool hasShownEnemyTurnTutorial = false;

    private SelectableUnit targetEnemyForStep5;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;

        LoadDefaultSpritesIfNeeded();
        BuildTutorialUIHierarchy();
    }

    private void Start()
    {
        BattleManager.EnemyTurnTutorialHook = OnEnemyTurnHook;

        SelectableUnit.OnPlayerSkillUsedEvent += HandlePlayerSkillUsed;
        Card.OnCardClickedEvent += HandleCardClicked;
        Card.OnCardUsedOnEnemyEvent += HandleCardUsedOnEnemy;
        AttackBoxManager.OnCardAddedToAttackBoxEvent += HandleCardAddedToAttackBox;

        StartCoroutine(CoRunTutorialSequence());
    }

    private void OnDestroy()
    {
        BattleManager.EnemyTurnTutorialHook = null;
        SelectableUnit.OnPlayerSkillUsedEvent -= HandlePlayerSkillUsed;
        Card.OnCardClickedEvent -= HandleCardClicked;
        Card.OnCardUsedOnEnemyEvent -= HandleCardUsedOnEnemy;
        AttackBoxManager.OnCardAddedToAttackBoxEvent -= HandleCardAddedToAttackBox;

        if (Ins == this) Ins = null;
    }

    private void LoadDefaultSpritesIfNeeded()
    {
        if (playerTriangleSprite == null)
            playerTriangleSprite = LoadSpriteFromDisk("Assets/Resource/Charactor/player_Arts.png");
        if (playerPentagonSprite == null)
            playerPentagonSprite = LoadSpriteFromDisk("Assets/Resource/Charactor/player_Buster.png");
        if (enemySquareSprite == null)
            enemySquareSprite = LoadSpriteFromDisk("Assets/Resource/Charactor/enemy_neon_square 1.png");
        if (enemyFeatherBuffSprite == null)
        {
            enemyFeatherBuffSprite = LoadSpriteFromDisk("Assets/Resource/UI/icon_buff_feather.png");
            if (enemyFeatherBuffSprite == null)
                enemyFeatherBuffSprite = UnitStatusUI.GetOrCreateFeatherSprite();
        }
        if (cardRedSprite == null)
            cardRedSprite = LoadSpriteFromDisk("Assets/Resource/UI/card_neon_red_blank.png");
        if (cardBlueSprite == null)
            cardBlueSprite = LoadSpriteFromDisk("Assets/Resource/UI/card_neon_blue_blank.png");
        if (cardGreenSprite == null)
            cardGreenSprite = LoadSpriteFromDisk("Assets/Resource/UI/card_neon_green_blank.png");

        if (skillAttackUpSprite == null)
            skillAttackUpSprite = PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.SKILL_1);
        if (skillDefenseUpSprite == null)
            skillDefenseUpSprite = PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.SKILL_2);
        if (skillCritDamageUpSprite == null)
            skillCritDamageUpSprite = PlayerSkillUI.GetOrCreateSkillSprite(ESkillType.SKILL_3);
    }

    private Sprite LoadSpriteFromDisk(string relativePath)
    {
        return UIHelper.LoadSpriteFromAssetOrDisk(relativePath);
    }

    private void Update()
    {
        if (isSpotlightActive)
        {
            float pulse = Mathf.PingPong(Time.unscaledTime * 3.5f, 1f);
            Color baseCol = new Color(0.35f, 0.75f, 1f, 0.85f);
            Color pulsedCol = Color.Lerp(baseCol, Color.white, pulse * 0.7f);

            if (spotlightBorderImg != null)
                spotlightBorderImg.color = pulsedCol;

            for (int i = 0; i < extraSpotlightBorderImgs.Count; i++)
            {
                if (extraSpotlightBorderImgs[i] != null && extraSpotlightBorderImgs[i].gameObject.activeSelf)
                    extraSpotlightBorderImgs[i].color = pulsedCol;
            }

            UpdateCurrentStepSpotlightTarget();
        }
    }

    private void UpdateCurrentStepSpotlightTarget()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // 스킬 확인 모달이 열려 있을 때는 유저가 모달의 [확인]/[취소] 버튼을 누를 수 있도록 모달 창을 스포트라이트
        if (SkillConfirmModalUI.Ins != null && SkillConfirmModalUI.Ins.IsModalOpen)
        {
            Transform modalWin = SkillConfirmModalUI.Ins.transform.Find("SkillConfirmModalGroup/ModalWindow");
            if (modalWin == null)
            {
                GameObject modalObj = GameObject.Find("ModalWindow");
                if (modalObj != null) modalWin = modalObj.transform;
            }
            if (modalWin != null)
            {
                SetSpotlightFromRectTransform(modalWin.GetComponent<RectTransform>(), 16f);
                return;
            }
        }

        if (currentStep == 1)
        {
            GameObject p1 = GameObject.Find("Player_1");
            GameObject p2 = GameObject.Find("Player_2");
            GameObject p3 = GameObject.Find("Player_3");
            SetSpotlightFromWorldObjects(cam, new[] { p1, p2, p3 }, 35f);
        }
        else if (currentStep == 2)
        {
            GameObject e1 = GameObject.Find("Enemy_1");
            GameObject e2 = GameObject.Find("Enemy_2");
            GameObject e3 = GameObject.Find("Enemy_3");
            SetSpotlightFromWorldObjects(cam, new[] { e1, e2, e3 }, 35f);
        }
        else if (currentStep == 3)
        {
            List<RectTransform> targetRects = new List<RectTransform>();

            RectTransform skillUi1 = FindSkillUIRect("SkillUI_Player_1", "Player_1");
            RectTransform skillUi2 = FindSkillUIRect("SkillUI_Player_2", "Player_2");
            RectTransform skillUi3 = FindSkillUIRect("SkillUI_Player_3", "Player_3");
            RectTransform costTray = FindUIRectByName("CostTrayUI");

            if (skillUi1 != null) targetRects.Add(skillUi1);
            if (skillUi2 != null) targetRects.Add(skillUi2);
            if (skillUi3 != null) targetRects.Add(skillUi3);
            if (costTray != null) targetRects.Add(costTray);

            if (targetRects.Count > 0)
            {
                SetSpotlightFromMultipleRectTransforms(targetRects.ToArray(), 12f);
            }
        }
        else if (currentStep == 4)
        {
            List<RectTransform> targetRects = new List<RectTransform>();
            if (HandManager.Ins != null)
            {
                RectTransform handRect = HandManager.Ins.GetComponent<RectTransform>();
                if (handRect != null) targetRects.Add(handRect);
                if (HandManager.Ins.DrawPileRect != null) targetRects.Add(HandManager.Ins.DrawPileRect);
                if (HandManager.Ins.DiscardPileRect != null) targetRects.Add(HandManager.Ins.DiscardPileRect);
            }

            if (targetRects.Count > 0)
            {
                SetSpotlightFromMultipleRectTransforms(targetRects.ToArray(), 18f);
            }
        }
        else if (currentStep == 5)
        {
            // Step 5에서는 카드를 손패 밖으로 끌어올려 적(Enemy)에게 조준해야 하므로 손패와 적 영역을 함께 하이라이트
            GameObject e1 = GameObject.Find("Enemy_1");
            GameObject e2 = GameObject.Find("Enemy_2");
            GameObject e3 = GameObject.Find("Enemy_3");
            RectTransform handRect = HandManager.Ins != null ? HandManager.Ins.GetComponent<RectTransform>() : null;
            SetSpotlightFromWorldAndUI(cam, new[] { e1, e2, e3 }, handRect != null ? new[] { handRect } : null, 28f);
        }
        else if (currentStep == 6 && targetEnemyForStep5 != null)
        {
            SetSpotlightFromWorldObjects(cam, new[] { targetEnemyForStep5.gameObject }, 40f);
        }
    }

    private RectTransform FindSkillUIRect(string skillUiName, string playerObjName)
    {
        RectTransform rt = FindUIRectByName(skillUiName);
        if (rt != null) return rt;

        GameObject unitObj = GameObject.Find(playerObjName);
        if (unitObj != null)
        {
            SelectableUnit unit = unitObj.GetComponent<SelectableUnit>();
            if (unit != null && unit.SkillUI != null)
                return unit.SkillUI.GetComponent<RectTransform>();
        }
        return null;
    }

    private RectTransform FindUIRectByName(string uiObjName)
    {
        GameObject obj = GameObject.Find(uiObjName);
        return obj != null ? obj.GetComponent<RectTransform>() : null;
    }

    #region Tutorial Sequence Coroutine

    private IEnumerator CoRunTutorialSequence()
    {
        yield return new WaitForSeconds(0.8f);

        // =====================================================================
        // STEP 1: 플레이어 캐릭터 & 고유 카드 구성
        // =====================================================================
        currentStep = 1;
        stepActionCompleted = false;
        isStepWaiting = true;

        List<Sprite> step1Sprites = new List<Sprite>();
        if (playerTriangleSprite != null) step1Sprites.Add(playerTriangleSprite);
        if (playerPentagonSprite != null) step1Sprites.Add(playerPentagonSprite);

        ShowGuide(
            stepNum: 1,
            title: "1. 플레이어 & 고유 카드 구성",
            body: "각 <b>Player</b>는 고유한 <b>5장의 카드 패턴</b>을 보유합니다.\n\n" +
                  "• <b>Player_1 (삼각형)</b> : 아츠 3장, 퀵 1장, 버스터 1장\n" +
                  "• <b>Player_2 (팔각형)</b> : 퀵 3장, 아츠 1장, 버스터 1장\n" +
                  "• <b>Player_3 (오각형)</b> : 버스터 3장, 아츠 1장, 퀵 1장\n\n" +
                  "모든 아군의 카드를 합친 <b>통합 덱(5~15장)</b>에서 매 턴 <b>5장</b>을 랜덤으로 뽑아 시작하며, 각 카드의 그림칸에는 소유 캐릭터의 도형이 표시됩니다.",
            hint: "설명을 확인한 후 [다 음 >] 버튼을 눌러 진행하세요.",
            sprites: step1Sprites
        );

        UpdateCurrentStepSpotlightTarget();
        isSpotlightActive = true;

        while (!stepActionCompleted)
        {
            yield return null;
        }

        // =====================================================================
        // STEP 2: 공격할 적 선택 (Enemy & 깃털 특수버프)
        // =====================================================================
        currentStep = 2;
        stepActionCompleted = false;
        isStepWaiting = true;

        List<Sprite> step2Sprites = new List<Sprite>();
        if (enemySquareSprite != null) step2Sprites.Add(enemySquareSprite);
        if (enemyFeatherBuffSprite != null) step2Sprites.Add(enemyFeatherBuffSprite);

        ShowGuide(
            stepNum: 2,
            title: "2. 적(Enemy) & 특수버프",
            body: "<b>Enemy</b> : 공격 대상인 적의 상태를 확인하세요.\n\n" +
                  "<color=#5CE1FF><b>★ Enemy 특수버프 (깃털 버프)</b></color>\n" +
                  "• 적의 HP바 아래에 표시되는 <b>깃털 아이콘</b>은 적 고유의 특수버프입니다.\n" +
                  "• <b>매 턴마다 중첩 수가 +1씩 증가</b>하며, 적의 공격력이 <b>중첩 수만큼 배수(기본 공격력 × 중첩 수)</b>로 강력해집니다!",
            hint: "설명을 확인한 후 [다 음 >] 버튼을 눌러 진행하세요.",
            sprites: step2Sprites,
            placeOnLeft: true
        );

        UpdateCurrentStepSpotlightTarget();

        while (!stepActionCompleted)
        {
            yield return null;
        }

        // =====================================================================
        // STEP 3: 캐릭터 고유 스킬 & 스킬 사용 확인창 UI
        // =====================================================================
        currentStep = 3;
        stepActionCompleted = false;
        isStepWaiting = true;

        List<Sprite> step3SkillSprites = new List<Sprite>();
        if (skillAttackUpSprite != null) step3SkillSprites.Add(skillAttackUpSprite);
        if (skillDefenseUpSprite != null) step3SkillSprites.Add(skillDefenseUpSprite);
        if (skillCritDamageUpSprite != null) step3SkillSprites.Add(skillCritDamageUpSprite);

        ShowGuide(
            stepNum: 3,
            title: "3. 캐릭터 고유 스킬 & 확인창",
            body: "각 캐릭터 위의 <b>스킬 아이콘</b>을 클릭하면 <b>스킬 사용 확인창</b>이 열립니다.\n\n" +
                  "• <b>Player_1</b> : Q&B 코스트 0 / 코스트 +2 / 손패 리드로우\n" +
                  "• <b>Player_2</b> : 스타 +50 / 스타 획득 2배 / 전체 방어력 +50%\n" +
                  "• <b>Player_3</b> : 공격력 +10% / 공격력 +30% / <b>슈퍼Buster 생성</b>\n\n" +
                  "• 쿨타임 중인 스킬은 아이콘 위에 <b>남은 턴 숫자</b>가 표시되며 클릭 시 좌우로 진동합니다.",
            hint: "스킬 아이콘을 클릭한 뒤 확인창에서 [확인]을 눌러보세요!",
            sprites: step3SkillSprites
        );

        UpdateCurrentStepSpotlightTarget();

        while (!stepActionCompleted)
        {
            yield return null;
        }

        // =====================================================================
        // STEP 4: 덱, 손패(최대 10장), 버린 카드 더미
        // =====================================================================
        currentStep = 4;
        stepActionCompleted = false;
        isStepWaiting = true;

        List<Sprite> step4Sprites = new List<Sprite>();
        if (cardBlueSprite != null) step4Sprites.Add(cardBlueSprite);
        if (cardGreenSprite != null) step4Sprites.Add(cardGreenSprite);
        if (cardRedSprite != null) step4Sprites.Add(cardRedSprite);

        ShowGuide(
            stepNum: 4,
            title: "4. 덱 & 손패 & 버린 카드",
            body: "<b>슬레이 더 스파이어 2</b> 방식의 덱/손패 시스템입니다.\n\n" +
                  "• <b>좌측 하단 덱</b>에서 매 턴 <b>5장의 카드</b>를 드로우하며, 손패에는 <b>최대 10장</b>까지 완만한 부채꼴로 보유할 수 있습니다.\n" +
                  "• 사용한 카드는 <b>우측 하단 버린 카드 더미</b>로 날아가며, 덱이 비면 버린 카드를 섞어 다시 보충합니다.",
            hint: "손패의 카드를 클릭하거나 드래그해 보세요!",
            sprites: step4Sprites
        );

        UpdateCurrentStepSpotlightTarget();

        while (!stepActionCompleted)
        {
            yield return null;
        }

        // =====================================================================
        // STEP 5: Spline 화살표 타겟팅 공격 & 3종 카드 규칙
        // =====================================================================
        currentStep = 5;
        stepActionCompleted = false;
        isStepWaiting = true;

        List<Sprite> step5Sprites = new List<Sprite>();
        if (cardBlueSprite != null) step5Sprites.Add(cardBlueSprite);
        if (cardGreenSprite != null) step5Sprites.Add(cardGreenSprite);
        if (cardRedSprite != null) step5Sprites.Add(cardRedSprite);

        ShowGuide(
            stepNum: 5,
            title: "5. 카드 타겟팅 공격 & 카드 규칙",
            body: "카드를 잡고 <b>손패 영역 밖(위쪽)</b>으로 나가면 카드가 하단 중앙에 위치하며 <b>Spline 곡선 화살표</b>가 나타납니다!\n\n" +
                  "<color=#4DA6FF><b>• Arts (1코스트)</b></color> : 데미지 + <b>1장 드로우 & 다음턴 코스트+1</b>\n" +
                  "<color=#5CD65C><b>• Quick (1코스트)</b></color> : 데미지 + <b>크리티컬 스타 생성</b>\n" +
                  "<color=#FF5555><b>• Buster (2코스트)</b></color> : <b>공격력 × 2</b> 강력 공격\n\n" +
                  "<i>화살표를 적(Enemy) 위에 올려 타겟 표시가 떴을 때 마우스를 놓아 공격하세요!</i>",
            hint: "카드를 위로 드래그하여 적(Enemy)에게 사용해 보세요!",
            sprites: step5Sprites,
            placeOnLeft: true
        );

        UpdateCurrentStepSpotlightTarget();

        while (!stepActionCompleted)
        {
            yield return null;
        }

        shownSteps.Add(1);
        shownSteps.Add(2);
        shownSteps.Add(3);
        shownSteps.Add(4);
        shownSteps.Add(5);

        HideGuide();
        isSpotlightActive = false;
        currentStep = 0;
    }

    private IEnumerator OnEnemyTurnHook(SelectableUnit firstAttacker)
    {
        BattleManager.EnemyTurnTutorialHook = null;
        if (hasShownEnemyTurnTutorial || !shownSteps.Add(6))
        {
            yield break;
        }
        hasShownEnemyTurnTutorial = true;

        targetEnemyForStep5 = firstAttacker;
        currentStep = 6;
        stepActionCompleted = false;
        isStepWaiting = true;

        List<Sprite> step6Sprites = new List<Sprite>();
        if (enemySquareSprite != null) step6Sprites.Add(enemySquareSprite);
        if (enemyFeatherBuffSprite != null) step6Sprites.Add(enemyFeatherBuffSprite);

        ShowGuide(
            stepNum: 6,
            title: "6. Enemy 턴",
            body: "<b>Enemy</b> : 우측 하단 <b>[턴 종료]</b> 버튼을 누르면 적의 턴이 시작됩니다.\n\n" +
                  "<color=#5CE1FF><b>• 적 특수버프(깃털) 효과</b></color> : 매 턴마다 HP바 아래의 <b>깃털 버프 중첩이 +1씩 증가</b>하여 적의 공격 데미지가 강해집니다.\n" +
                  "• 하단의 <b>[다 음 >]</b> 버튼을 누르면 적 턴이 진행됩니다.",
            hint: "[다 음 >] 버튼을 눌러 적의 공격을 확인하세요.",
            sprites: step6Sprites,
            placeOnLeft: true
        );

        UpdateCurrentStepSpotlightTarget();
        isSpotlightActive = true;

        while (!stepActionCompleted)
        {
            yield return null;
        }

        HideGuide();
        isSpotlightActive = false;
        targetEnemyForStep5 = null;
        currentStep = 0;
    }

    #endregion

    #region Event Handlers

    private void HandlePlayerSelected(SelectableUnit player)
    {
        if (currentStep == 1 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    private void HandleEnemySelected(SelectableUnit enemy)
    {
        if (currentStep == 2 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
        else if (currentStep == 6 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    private void HandlePlayerSkillUsed(SelectableUnit player, ESkillType skillType)
    {
        if (currentStep == 3 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    private void HandleCardClicked(Card card)
    {
        if (currentStep == 4 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    private void HandleCardUsedOnEnemy(Card card, SelectableUnit enemy)
    {
        if (currentStep == 5 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    private void HandleCardAddedToAttackBox(Card card)
    {
        if (currentStep == 5 && isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    private void OnNextButtonClicked()
    {
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        if (isStepWaiting)
        {
            stepActionCompleted = true;
            isStepWaiting = false;
        }
    }

    #endregion

    #region UI Hierarchy Builder

    private void BuildTutorialUIHierarchy()
    {
        GameObject canvasObj = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        tutorialCanvas = canvasObj.GetComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.sortingOrder = 1500;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObj.GetComponent<RectTransform>();

        Color dimColor = new Color(0.05f, 0.07f, 0.12f, 0.82f);
        topDim = CreateDimPanel("TopDim", dimColor);
        bottomDim = CreateDimPanel("BottomDim", dimColor);
        leftDim = CreateDimPanel("LeftDim", dimColor);
        rightDim = CreateDimPanel("RightDim", dimColor);

        GameObject borderObj = new GameObject("SpotlightBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(canvasRect, false);
        spotlightBorder = borderObj.GetComponent<RectTransform>();
        spotlightBorderImg = borderObj.GetComponent<Image>();
        spotlightBorderImg.sprite = CreateBorderSprite();
        spotlightBorderImg.type = Image.Type.Sliced;
        spotlightBorderImg.raycastTarget = false;
        spotlightBorderImg.color = new Color(0.35f, 0.75f, 1f, 0.85f);
        borderObj.SetActive(false);

        BuildRightGuidePanel();
        HideGuide();
    }

    private RectTransform CreateDimPanel(string name, Color color)
    {
        GameObject panelObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvasRect, false);

        Image img = panelObj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        return rt;
    }

    private void BuildRightGuidePanel()
    {
        GameObject panelObj = new GameObject("RightGuidePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObj.transform.SetParent(canvasRect, false);
        rightGuidePanel = panelObj.GetComponent<RectTransform>();
        rightGuideCg = panelObj.GetComponent<CanvasGroup>();

        rightGuidePanel.anchorMin = new Vector2(1f, 0.5f);
        rightGuidePanel.anchorMax = new Vector2(1f, 0.5f);
        rightGuidePanel.pivot = new Vector2(1f, 0.5f);
        rightGuidePanel.anchoredPosition = new Vector2(-40f, 0f);
        rightGuidePanel.sizeDelta = new Vector2(490f, 750f);

        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.sprite = CreateRoundedRectSprite(24);
        panelBg.type = Image.Type.Sliced;
        panelBg.color = new Color(0.08f, 0.11f, 0.19f, 0.95f);
        panelBg.raycastTarget = true;

        GameObject outlineObj = new GameObject("CardOutline", typeof(RectTransform), typeof(Image));
        outlineObj.transform.SetParent(rightGuidePanel, false);
        RectTransform outlineRect = outlineObj.GetComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.sizeDelta = Vector2.zero;
        Image outlineImg = outlineObj.GetComponent<Image>();
        outlineImg.sprite = CreateBorderSprite();
        outlineImg.type = Image.Type.Sliced;
        outlineImg.color = new Color(0.28f, 0.42f, 0.75f, 0.85f);
        outlineImg.raycastTarget = false;

        Font font = GetBestFont();

        TMP_FontAsset tmpFont = UIHelper.GetOrCreateTMPFont();

        GameObject badgeObj = new GameObject("StepBadge", typeof(RectTransform), typeof(Image));
        badgeObj.transform.SetParent(rightGuidePanel, false);
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -24f);
        badgeRect.sizeDelta = new Vector2(230f, 38f);
        Image badgeImg = badgeObj.GetComponent<Image>();
        badgeImg.sprite = CreateRoundedRectSprite(18);
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = new Color(0.18f, 0.28f, 0.52f, 1f);
        badgeImg.raycastTarget = false;

        GameObject badgeTextObj = new GameObject("StepBadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        RectTransform badgeTextRect = badgeTextObj.GetComponent<RectTransform>();
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.sizeDelta = Vector2.zero;

        stepBadgeText = badgeTextObj.GetComponent<TextMeshProUGUI>();
        stepBadgeText.font = tmpFont;
        stepBadgeText.fontSize = 17;
        stepBadgeText.fontStyle = FontStyles.Bold;
        stepBadgeText.color = new Color(0.85f, 0.92f, 1f, 1f);
        stepBadgeText.alignment = TextAlignmentOptions.Midline;
        stepBadgeText.text = $"TUTORIAL STEP 1/{TotalTutorialSteps}";

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(rightGuidePanel, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -74f);
        titleRect.sizeDelta = new Vector2(430f, 60f);

        titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.font = tmpFont;
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Midline;

        GameObject dividerObj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dividerObj.transform.SetParent(rightGuidePanel, false);
        RectTransform divRect = dividerObj.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 1f);
        divRect.anchorMax = new Vector2(0.5f, 1f);
        divRect.pivot = new Vector2(0.5f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -140f);
        divRect.sizeDelta = new Vector2(430f, 2f);
        Image divImg = dividerObj.GetComponent<Image>();
        divImg.color = new Color(0.28f, 0.38f, 0.60f, 0.5f);
        divImg.raycastTarget = false;

        GameObject imgContObj = new GameObject("ImageContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        imgContObj.transform.SetParent(rightGuidePanel, false);
        imageContainer = imgContObj.GetComponent<RectTransform>();
        imageContainer.anchorMin = new Vector2(0.5f, 1f);
        imageContainer.anchorMax = new Vector2(0.5f, 1f);
        imageContainer.pivot = new Vector2(0.5f, 1f);
        imageContainer.anchoredPosition = new Vector2(0f, -154f);
        imageContainer.sizeDelta = new Vector2(430f, 150f);

        HorizontalLayoutGroup hlg = imgContObj.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 18f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject bodyObj = new GameObject("BodyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(rightGuidePanel, false);
        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -320f);
        bodyRect.sizeDelta = new Vector2(430f, 290f);

        bodyText = bodyObj.GetComponent<TextMeshProUGUI>();
        bodyText.font = tmpFont;
        bodyText.fontSize = 17;
        bodyText.lineSpacing = 8f;
        bodyText.richText = true;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.color = new Color(0.88f, 0.92f, 1f, 1f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;

        GameObject hintObj = new GameObject("HintText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        hintObj.transform.SetParent(rightGuidePanel, false);
        RectTransform hintRect = hintObj.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 75f);
        hintRect.sizeDelta = new Vector2(430f, 32f);

        hintText = hintObj.GetComponent<TextMeshProUGUI>();
        hintText.font = tmpFont;
        hintText.fontSize = 15;
        hintText.fontStyle = FontStyles.Italic;
        hintText.color = new Color(0.45f, 0.85f, 1f, 1f);
        hintText.alignment = TextAlignmentOptions.Midline;

        GameObject btnObj = new GameObject("NextStepButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(rightGuidePanel, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 20f);
        btnRect.sizeDelta = new Vector2(430f, 46f);

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.sprite = CreateRoundedRectSprite(12);
        btnImg.type = Image.Type.Sliced;
        btnImg.color = new Color(0.24f, 0.44f, 0.86f, 1f);

        nextStepBtn = btnObj.GetComponent<Button>();
        nextStepBtn.onClick.AddListener(OnNextButtonClicked);
        UIHelper.EnsureHoverScale(btnObj, 1.05f);

        GameObject btnTextObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnText = btnTextObj.GetComponent<TextMeshProUGUI>();
        btnText.font = tmpFont;
        btnText.fontSize = 17;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Midline;
        btnText.text = "다 음  >";
    }

    #endregion

    #region Spotlight & Guide Display

    public void ShowGuide(int stepNum, string title, string body, string hint, List<Sprite> sprites, bool placeOnLeft = false)
    {
        if (rightGuidePanel == null) return;

        SetGuidePanelSide(placeOnLeft);
        rightGuidePanel.gameObject.SetActive(true);
        rightGuidePanel.SetAsLastSibling();
        if (rightGuideCg != null)
        {
            rightGuideCg.alpha = 1f;
            rightGuideCg.blocksRaycasts = true;
        }

        if (stepBadgeText != null) stepBadgeText.text = $"TUTORIAL STEP {stepNum}/{TotalTutorialSteps}";
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
        if (hintText != null) hintText.text = hint;

        for (int i = 0; i < displayedImages.Count; i++)
        {
            if (displayedImages[i] != null)
                Destroy(displayedImages[i].gameObject);
        }
        displayedImages.Clear();

        if (sprites != null && imageContainer != null)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sp = sprites[i];
                if (sp == null) continue;

                GameObject imgObj = new GameObject($"GuideImg_{i}", typeof(RectTransform), typeof(Image));
                imgObj.transform.SetParent(imageContainer, false);
                RectTransform rt = imgObj.GetComponent<RectTransform>();
                Image img = imgObj.GetComponent<Image>();
                img.sprite = sp;
                img.preserveAspect = true;
                img.raycastTarget = false;

                bool isSquareIcon = sp.rect.height > 0f && Mathf.Abs((sp.rect.width / sp.rect.height) - 1f) < 0.15f;
                if (sprites.Count >= 3)
                    rt.sizeDelta = isSquareIcon ? new Vector2(96f, 96f) : new Vector2(85f, 130f);
                else
                    rt.sizeDelta = new Vector2(120f, 120f);

                displayedImages.Add(img);
            }
        }
    }

    private void SetGuidePanelSide(bool placeOnLeft)
    {
        if (rightGuidePanel == null) return;

        if (placeOnLeft)
        {
            rightGuidePanel.anchorMin = new Vector2(0f, 0.5f);
            rightGuidePanel.anchorMax = new Vector2(0f, 0.5f);
            rightGuidePanel.pivot = new Vector2(0f, 0.5f);
            rightGuidePanel.anchoredPosition = new Vector2(40f, 0f);
        }
        else
        {
            rightGuidePanel.anchorMin = new Vector2(1f, 0.5f);
            rightGuidePanel.anchorMax = new Vector2(1f, 0.5f);
            rightGuidePanel.pivot = new Vector2(1f, 0.5f);
            rightGuidePanel.anchoredPosition = new Vector2(-40f, 0f);
        }
    }

    public void HideGuide()
    {
        isSpotlightActive = false;

        if (rightGuidePanel != null)
            rightGuidePanel.gameObject.SetActive(false);

        if (topDim != null) topDim.gameObject.SetActive(false);
        if (bottomDim != null) bottomDim.gameObject.SetActive(false);
        if (leftDim != null) leftDim.gameObject.SetActive(false);
        if (rightDim != null) rightDim.gameObject.SetActive(false);
        if (spotlightBorder != null) spotlightBorder.gameObject.SetActive(false);

        for (int i = 0; i < extraDimPanels.Count; i++)
        {
            if (extraDimPanels[i] != null)
                extraDimPanels[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < extraSpotlightBorders.Count; i++)
        {
            if (extraSpotlightBorders[i] != null)
                extraSpotlightBorders[i].gameObject.SetActive(false);
        }
    }

    public void SetSpotlightHole(Rect screenHoleRect)
    {
        if (canvasRect == null) return;

        for (int i = 0; i < extraDimPanels.Count; i++)
        {
            if (extraDimPanels[i] != null)
                extraDimPanels[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < extraSpotlightBorders.Count; i++)
        {
            if (extraSpotlightBorders[i] != null)
                extraSpotlightBorders[i].gameObject.SetActive(false);
        }

        float screenW = canvasRect.rect.width;
        float screenH = canvasRect.rect.height;

        float xMin = Mathf.Clamp(screenHoleRect.xMin, 0f, screenW);
        float xMax = Mathf.Clamp(screenHoleRect.xMax, 0f, screenW);
        float yMin = Mathf.Clamp(screenHoleRect.yMin, 0f, screenHoleRect.yMax);
        float yMax = Mathf.Clamp(screenHoleRect.yMax, 0f, screenH);

        topDim.gameObject.SetActive(true);
        bottomDim.gameObject.SetActive(true);
        leftDim.gameObject.SetActive(true);
        rightDim.gameObject.SetActive(true);

        bottomDim.anchoredPosition = new Vector2(0f, 0f);
        bottomDim.sizeDelta = new Vector2(screenW, yMin);

        topDim.anchoredPosition = new Vector2(0f, yMax);
        topDim.sizeDelta = new Vector2(screenW, screenH - yMax);

        leftDim.anchoredPosition = new Vector2(0f, yMin);
        leftDim.sizeDelta = new Vector2(xMin, yMax - yMin);

        rightDim.anchoredPosition = new Vector2(xMax, yMin);
        rightDim.sizeDelta = new Vector2(screenW - xMax, yMax - yMin);

        if (spotlightBorder != null)
        {
            spotlightBorder.gameObject.SetActive(true);
            spotlightBorder.anchorMin = Vector2.zero;
            spotlightBorder.anchorMax = Vector2.zero;
            spotlightBorder.pivot = Vector2.zero;
            spotlightBorder.anchoredPosition = new Vector2(xMin - 4f, yMin - 4f);
            spotlightBorder.sizeDelta = new Vector2((xMax - xMin) + 8f, (yMax - yMin) + 8f);
        }

        if (rightGuidePanel != null)
            rightGuidePanel.SetAsLastSibling();
    }

    public void SetSpotlightFromMultipleRectTransforms(RectTransform[] targetRects, float padding = 12f)
    {
        if (targetRects == null || targetRects.Length == 0 || canvasRect == null) return;

        List<Rect> holeRects = new List<Rect>();
        Vector3[] corners = new Vector3[4];

        for (int i = 0; i < targetRects.Length; i++)
        {
            RectTransform rt = targetRects[i];
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;

            Canvas rtCanvas = rt.GetComponentInParent<Canvas>();
            Camera uiCam = (rtCanvas != null && rtCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (rtCanvas.worldCamera != null ? rtCanvas.worldCamera : Camera.main)
                : null;

            rt.GetWorldCorners(corners);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int c = 0; c < 4; c++)
            {
                Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, corners[c]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPt, null, out Vector2 localPoint);

                float canvasX = localPoint.x + (canvasRect.rect.width * 0.5f);
                float canvasY = localPoint.y + (canvasRect.rect.height * 0.5f);

                minX = Mathf.Min(minX, canvasX);
                minY = Mathf.Min(minY, canvasY);
                maxX = Mathf.Max(maxX, canvasX);
                maxY = Mathf.Max(maxY, canvasY);
            }

            holeRects.Add(new Rect(minX - padding, minY - padding, (maxX - minX) + (padding * 2f), (maxY - minY) + (padding * 2f)));
        }

        if (holeRects.Count == 1)
        {
            SetSpotlightHole(holeRects[0]);
        }
        else if (holeRects.Count > 1)
        {
            SetSpotlightHoles(holeRects);
        }
    }

    public void SetSpotlightHoles(List<Rect> screenHoleRects)
    {
        if (canvasRect == null || screenHoleRects == null || screenHoleRects.Count == 0) return;

        if (topDim != null) topDim.gameObject.SetActive(false);
        if (bottomDim != null) bottomDim.gameObject.SetActive(false);
        if (leftDim != null) leftDim.gameObject.SetActive(false);
        if (rightDim != null) rightDim.gameObject.SetActive(false);
        if (spotlightBorder != null) spotlightBorder.gameObject.SetActive(false);

        float screenW = canvasRect.rect.width;
        float screenH = canvasRect.rect.height;

        List<Rect> clampedHoles = new List<Rect>();
        List<float> ySplits = new List<float> { 0f, screenH };

        for (int i = 0; i < screenHoleRects.Count; i++)
        {
            Rect r = screenHoleRects[i];
            float xMin = Mathf.Clamp(r.xMin, 0f, screenW);
            float xMax = Mathf.Clamp(r.xMax, 0f, screenW);
            float yMin = Mathf.Clamp(r.yMin, 0f, screenH);
            float yMax = Mathf.Clamp(r.yMax, 0f, screenH);

            if (xMax > xMin + 0.5f && yMax > yMin + 0.5f)
            {
                Rect clamped = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                clampedHoles.Add(clamped);
                ySplits.Add(yMin);
                ySplits.Add(yMax);
            }
        }

        ySplits.Sort();
        List<float> uniqueY = new List<float>();
        for (int i = 0; i < ySplits.Count; i++)
        {
            if (uniqueY.Count == 0 || Mathf.Abs(ySplits[i] - uniqueY[uniqueY.Count - 1]) > 0.5f)
                uniqueY.Add(ySplits[i]);
        }

        Color dimColor = new Color(0.05f, 0.07f, 0.12f, 0.82f);
        int dimIndex = 0;

        for (int s = 0; s < uniqueY.Count - 1; s++)
        {
            float y0 = uniqueY[s];
            float y1 = uniqueY[s + 1];
            if (y1 - y0 <= 0.5f) continue;

            List<Vector2> xIntervals = new List<Vector2>();
            for (int h = 0; h < clampedHoles.Count; h++)
            {
                Rect hole = clampedHoles[h];
                if (hole.yMin <= y0 + 0.5f && hole.yMax >= y1 - 0.5f)
                {
                    xIntervals.Add(new Vector2(hole.xMin, hole.xMax));
                }
            }

            xIntervals.Sort((a, b) => a.x.CompareTo(b.x));

            float curX = 0f;
            for (int k = 0; k < xIntervals.Count; k++)
            {
                float hx0 = xIntervals[k].x;
                float hx1 = xIntervals[k].y;

                if (hx0 > curX + 0.5f)
                {
                    RectTransform dimRt = GetOrCreateExtraDimPanel(dimIndex++, dimColor);
                    dimRt.gameObject.SetActive(true);
                    dimRt.anchoredPosition = new Vector2(curX, y0);
                    dimRt.sizeDelta = new Vector2(hx0 - curX, y1 - y0);
                }
                curX = Mathf.Max(curX, hx1);
            }

            if (screenW > curX + 0.5f)
            {
                RectTransform dimRt = GetOrCreateExtraDimPanel(dimIndex++, dimColor);
                dimRt.gameObject.SetActive(true);
                dimRt.anchoredPosition = new Vector2(curX, y0);
                dimRt.sizeDelta = new Vector2(screenW - curX, y1 - y0);
            }
        }

        for (int i = dimIndex; i < extraDimPanels.Count; i++)
        {
            if (extraDimPanels[i] != null)
                extraDimPanels[i].gameObject.SetActive(false);
        }

        for (int h = 0; h < clampedHoles.Count; h++)
        {
            Rect hole = clampedHoles[h];
            RectTransform borderRt = GetOrCreateExtraSpotlightBorder(h);
            borderRt.gameObject.SetActive(true);
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.zero;
            borderRt.pivot = Vector2.zero;
            borderRt.anchoredPosition = new Vector2(hole.xMin - 4f, hole.yMin - 4f);
            borderRt.sizeDelta = new Vector2(hole.width + 8f, hole.height + 8f);
            borderRt.SetAsLastSibling();
        }

        for (int i = clampedHoles.Count; i < extraSpotlightBorders.Count; i++)
        {
            if (extraSpotlightBorders[i] != null)
                extraSpotlightBorders[i].gameObject.SetActive(false);
        }

        if (rightGuidePanel != null)
            rightGuidePanel.SetAsLastSibling();
    }

    private RectTransform GetOrCreateExtraDimPanel(int index, Color color)
    {
        while (extraDimPanels.Count <= index)
        {
            RectTransform rt = CreateDimPanel($"ExtraDim_{extraDimPanels.Count}", color);
            extraDimPanels.Add(rt);
        }
        return extraDimPanels[index];
    }

    private RectTransform GetOrCreateExtraSpotlightBorder(int index)
    {
        if (cachedBorderSprite == null)
            cachedBorderSprite = spotlightBorderImg != null && spotlightBorderImg.sprite != null ? spotlightBorderImg.sprite : CreateBorderSprite();

        while (extraSpotlightBorders.Count <= index)
        {
            GameObject borderObj = new GameObject($"SpotlightBorder_{extraSpotlightBorders.Count}", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(canvasRect, false);
            RectTransform rt = borderObj.GetComponent<RectTransform>();
            Image img = borderObj.GetComponent<Image>();
            img.sprite = cachedBorderSprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            img.color = new Color(0.35f, 0.75f, 1f, 0.85f);
            borderObj.SetActive(false);

            extraSpotlightBorders.Add(rt);
            extraSpotlightBorderImgs.Add(img);
        }
        return extraSpotlightBorders[index];
    }

    public void SetSpotlightFromWorldObjects(Camera cam, GameObject[] objs, float padding = 30f)
    {
        if (cam == null || objs == null || objs.Length == 0 || canvasRect == null) return;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        bool hasValidPoint = false;

        for (int i = 0; i < objs.Length; i++)
        {
            GameObject obj = objs[i];
            if (obj == null || !obj.activeInHierarchy) continue;

            Collider2D col = obj.GetComponent<Collider2D>();
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();

            Bounds bounds = col != null ? col.bounds : (sr != null ? sr.bounds : new Bounds(obj.transform.position, Vector3.one * 1.5f));

            Vector3[] worldCorners = new Vector3[4]
            {
                new Vector3(bounds.min.x, bounds.min.y, 0f),
                new Vector3(bounds.min.x, bounds.max.y, 0f),
                new Vector3(bounds.max.x, bounds.min.y, 0f),
                new Vector3(bounds.max.x, bounds.max.y, 0f)
            };

            for (int c = 0; c < 4; c++)
            {
                Vector2 screenPoint = cam.WorldToScreenPoint(worldCorners[c]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

                float canvasX = localPoint.x + (canvasRect.rect.width * 0.5f);
                float canvasY = localPoint.y + (canvasRect.rect.height * 0.5f);

                minX = Mathf.Min(minX, canvasX);
                minY = Mathf.Min(minY, canvasY);
                maxX = Mathf.Max(maxX, canvasX);
                maxY = Mathf.Max(maxY, canvasY);
                hasValidPoint = true;
            }
        }

        if (!hasValidPoint) return;

        Rect holeRect = new Rect(minX - padding, minY - padding, (maxX - minX) + (padding * 2f), (maxY - minY) + (padding * 2f));
        SetSpotlightHole(holeRect);
    }

    public void SetSpotlightFromWorldAndUI(Camera cam, GameObject[] objs, RectTransform[] uiRects, float padding = 30f)
    {
        if (canvasRect == null) return;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        bool hasValidPoint = false;

        if (cam != null && objs != null)
        {
            for (int i = 0; i < objs.Length; i++)
            {
                GameObject obj = objs[i];
                if (obj == null || !obj.activeInHierarchy) continue;

                Collider2D col = obj.GetComponent<Collider2D>();
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();

                Bounds bounds = col != null ? col.bounds : (sr != null ? sr.bounds : new Bounds(obj.transform.position, Vector3.one * 1.5f));

                Vector3[] worldCorners = new Vector3[4]
                {
                    new Vector3(bounds.min.x, bounds.min.y, 0f),
                    new Vector3(bounds.min.x, bounds.max.y, 0f),
                    new Vector3(bounds.max.x, bounds.min.y, 0f),
                    new Vector3(bounds.max.x, bounds.max.y, 0f)
                };

                for (int c = 0; c < 4; c++)
                {
                    Vector2 screenPoint = cam.WorldToScreenPoint(worldCorners[c]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

                    float canvasX = localPoint.x + (canvasRect.rect.width * 0.5f);
                    float canvasY = localPoint.y + (canvasRect.rect.height * 0.5f);

                    minX = Mathf.Min(minX, canvasX);
                    minY = Mathf.Min(minY, canvasY);
                    maxX = Mathf.Max(maxX, canvasX);
                    maxY = Mathf.Max(maxY, canvasY);
                    hasValidPoint = true;
                }
            }
        }

        if (uiRects != null)
        {
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < uiRects.Length; i++)
            {
                RectTransform rt = uiRects[i];
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;

                Canvas rtCanvas = rt.GetComponentInParent<Canvas>();
                Camera uiCam = (rtCanvas != null && rtCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? (rtCanvas.worldCamera != null ? rtCanvas.worldCamera : cam)
                    : null;

                rt.GetWorldCorners(corners);
                for (int c = 0; c < 4; c++)
                {
                    Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, corners[c]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPt, null, out Vector2 localPoint);

                    float canvasX = localPoint.x + (canvasRect.rect.width * 0.5f);
                    float canvasY = localPoint.y + (canvasRect.rect.height * 0.5f);

                    minX = Mathf.Min(minX, canvasX);
                    minY = Mathf.Min(minY, canvasY);
                    maxX = Mathf.Max(maxX, canvasX);
                    maxY = Mathf.Max(maxY, canvasY);
                    hasValidPoint = true;
                }
            }
        }

        if (!hasValidPoint) return;

        Rect holeRect = new Rect(minX - padding, minY - padding, (maxX - minX) + (padding * 2f), (maxY - minY) + (padding * 2f));
        SetSpotlightHole(holeRect);
    }

    public void SetSpotlightFromRectTransform(RectTransform targetRect, float padding = 20f)
    {
        if (targetRect == null || canvasRect == null) return;

        Canvas rtCanvas = targetRect.GetComponentInParent<Canvas>();
        Camera uiCam = (rtCanvas != null && rtCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? (rtCanvas.worldCamera != null ? rtCanvas.worldCamera : Camera.main)
            : null;

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPt, null, out Vector2 localPoint);
            float canvasX = localPoint.x + (canvasRect.rect.width * 0.5f);
            float canvasY = localPoint.y + (canvasRect.rect.height * 0.5f);

            minX = Mathf.Min(minX, canvasX);
            minY = Mathf.Min(minY, canvasY);
            maxX = Mathf.Max(maxX, canvasX);
            maxY = Mathf.Max(maxY, canvasY);
        }

        Rect holeRect = new Rect(minX - padding, minY - padding, (maxX - minX) + (padding * 2f), (maxY - minY) + (padding * 2f));
        SetSpotlightHole(holeRect);
    }

    #endregion

    #region Helper Texture & Font Utilities

    private static Font GetBestFont() => UIHelper.GetOrCreateFont();

    private static Sprite CreateRoundedRectSprite(int radius = 16)
    {
        int size = radius * 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cornerX = (x < radius) ? radius - x : ((x >= size - radius) ? x - (size - radius - 1) : 0);
                int cornerY = (y < radius) ? radius - y : ((y >= size - radius) ? y - (size - radius - 1) : 0);

                if (cornerX > 0 && cornerY > 0)
                {
                    float dist = Mathf.Sqrt((cornerX * cornerX) + (cornerY * cornerY));
                    if (dist > radius)
                        tex.SetPixel(x, y, transparent);
                    else
                        tex.SetPixel(x, y, solid);
                }
                else
                {
                    tex.SetPixel(x, y, solid);
                }
            }
        }
        tex.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite CreateBorderSprite()
    {
        int size = 32;
        int borderW = 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color borderCol = Color.white;
        Color empty = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < borderW || x >= size - borderW || y < borderW || y >= size - borderW);
                tex.SetPixel(x, y, isBorder ? borderCol : empty);
            }
        }
        tex.Apply();

        Vector4 border = new Vector4(borderW + 2, borderW + 2, borderW + 2, borderW + 2);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    #endregion
}
