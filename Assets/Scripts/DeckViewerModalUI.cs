using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 1) 플레이어 오브젝트(Player_1~3) 클릭 시:
///    SkillConfirmModalGroup과 동일한 스타일로 화면을 어둡게 만들고, "덱"이라 적힌 중앙 박스 안에 해당 플레이어 캐릭터가 가진 카드를 모두 표시합니다.
/// 2) DrawPileUI(덱) / DiscardPileUI(버린 카드) 클릭 시:
///    첨부된 레퍼런스 이미지(Slay the Spire 덱 뷰어 레이아웃)와 동일하게 화면 전체를 어둡게 덮고,
///    상단 요약 바 + 중앙 5열 카드 그리드 + 우측 스크롤바 + 좌측 중앙 붉은 리본 [↩] 뒤로가기 버튼 + 하단 안내 박스를 표시합니다.
/// </summary>
public class DeckViewerModalUI : Singleton<DeckViewerModalUI>
{
    [Header("공통 최상위 딤드 오버레이 그룹")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private Image rootDimImage;

    [Header("1. 플레이어 클릭 전용 '덱' 모달 박스 (SkillConfirmModalGroup 스타일)")]
    [SerializeField] private RectTransform playerDeckWindowRect;
    [SerializeField] private TextMeshProUGUI playerDeckTitleText;
    [SerializeField] private TextMeshProUGUI playerDeckSubTitleText;
    [SerializeField] private RectTransform playerDeckCardContainer;
    [SerializeField] private Button playerDeckCloseXButton;

    [Header("2. DrawPile / DiscardPile 클릭 전용 전체 화면 카드 나열 뷰어 (첨부 이미지 스타일)")]
    [SerializeField] private RectTransform pileViewerGroupRect;
    [SerializeField] private ScrollRect pileScrollRect;
    [SerializeField] private RectTransform pileGridContentRect;
    [SerializeField] private TextMeshProUGUI emptyPileNoticeText;
    [SerializeField] private Button leftRibbonBackButton;
    [SerializeField] private TextMeshProUGUI bottomHintBannerText;

    private readonly List<GameObject> spawnedCardPreviews = new List<GameObject>();
    private Coroutine openAnimCoroutine;

    private static Sprite cachedRedRibbonBackSprite;
    private static Sprite cachedDiamondHandleSprite;

    public bool IsModalOpen => rootCanvasGroup != null && rootCanvasGroup.gameObject.activeSelf && rootCanvasGroup.alpha > 0.01f;

    protected override void Awake()
    {
        base.Awake();
        EnsureModalUI();
        HideImmediate();
    }

    private void Update()
    {
        if (!IsModalOpen) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnClickClose();
        }
    }

    // =========================================================================
    // 1. 플레이어 오브젝트 클릭 시 -> SkillConfirmModalGroup 스타일 "덱" 박스 표시
    // =========================================================================
    public void OpenPlayerDeckModal(SelectableUnit playerUnit)
    {
        if (playerUnit == null) return;

        EnsureModalUI();
        if (rootCanvasGroup == null) return;

        ClearSpawnedCardPreviews();

        // 어두운 배경 투명도 (SkillConfirmModalGroup 스타일)
        if (rootDimImage != null)
            rootDimImage.color = new Color(0.01f, 0.02f, 0.06f, 0.72f);

        if (playerDeckWindowRect != null)
            playerDeckWindowRect.gameObject.SetActive(true);
        if (pileViewerGroupRect != null)
            pileViewerGroupRect.gameObject.SetActive(false);

        int pType = playerUnit.ResolvePlayerTypeIndex();
        string playerLabel = $"Player {pType + 1}";

        List<ECardColor> ownedPattern = playerUnit.GetOwnedCardPattern();
        int artsCount = 0, quickCount = 0, busterCount = 0;
        for (int i = 0; i < ownedPattern.Count; i++)
        {
            if (ownedPattern[i] == ECardColor.ARTS) artsCount++;
            else if (ownedPattern[i] == ECardColor.QUICK) quickCount++;
            else if (ownedPattern[i].IsBusterAttribute()) busterCount++;
        }

        if (playerDeckTitleText != null)
            playerDeckTitleText.text = "덱";

        if (playerDeckSubTitleText != null)
            playerDeckSubTitleText.text = $"{playerLabel} 보유 카드 ({ownedPattern.Count}장 — Arts {artsCount} / Quick {quickCount} / Buster {busterCount})";

        // 박스 내부에 해당 플레이어가 가진 카드 5장을 일렬로 생성
        int count = ownedPattern.Count;
        float cardWidth = 176f;
        float cardHeight = 250f;
        float spacing = 26f;
        float startX = -((count - 1) * (cardWidth + spacing)) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector2 pos = new Vector2(startX + i * (cardWidth + spacing), -20f);
            GameObject cardObj = CreatePreviewCardObject(
                playerDeckCardContainer,
                $"PlayerCardPreview_{i + 1}",
                pType,
                playerUnit,
                ownedPattern[i],
                pos,
                new Vector2(cardWidth, cardHeight),
                useManualPosition: true
            );
            spawnedCardPreviews.Add(cardObj);
        }

        ShowModalWithAnimation(playerDeckWindowRect);
    }

    // =========================================================================
    // 2. DrawPileUI / DiscardPileUI 클릭 시 -> 첨부 이미지(Slay the Spire) 레이아웃 표시
    // =========================================================================
    public void OpenPileViewerModal(bool isDiscardPile)
    {
        EnsureModalUI();
        if (rootCanvasGroup == null) return;

        ClearSpawnedCardPreviews();

        // 첨부 이미지처럼 화면 전체가 깊게 어두워진 오버레이
        if (rootDimImage != null)
            rootDimImage.color = new Color(0.01f, 0.02f, 0.04f, 0.88f);

        if (playerDeckWindowRect != null)
            playerDeckWindowRect.gameObject.SetActive(false);
        if (pileViewerGroupRect != null)
            pileViewerGroupRect.gameObject.SetActive(true);

        List<DeckCardEntry> entries = new List<DeckCardEntry>();
        if (HandManager.Ins != null)
        {
            IReadOnlyList<DeckCardEntry> source = isDiscardPile
                ? HandManager.Ins.DiscardPileEntries
                : HandManager.Ins.DrawPileEntries;

            if (source != null)
                entries.AddRange(source);
        }

        // 뽑을 카드 더미(덱)는 카드 종류 및 소유 플레이어 순으로 보기 좋게 정렬하여 표시
        if (!isDiscardPile && entries.Count > 1)
        {
            entries.Sort((a, b) =>
            {
                int cmpPlayer = a.ownerPlayerIndex.CompareTo(b.ownerPlayerIndex);
                if (cmpPlayer != 0) return cmpPlayer;
                return a.cardColor.CompareTo(b.cardColor);
            });
        }

        if (bottomHintBannerText != null)
        {
            bottomHintBannerText.text = isDiscardPile
                ? "이번 전투에서 사용하거나 버려진 카드들이 이곳에 모입니다."
                : "매 턴마다 이곳에서 카드를 뽑아 전투를 시작합니다.";
        }

        if (emptyPileNoticeText != null)
        {
            emptyPileNoticeText.gameObject.SetActive(entries.Count == 0);
            emptyPileNoticeText.text = isDiscardPile
                ? "현재 버린 카드 더미에 카드가 없습니다."
                : "현재 뽑을 카드 더미(덱)에 남은 카드가 없습니다.";
        }

        // 5열 그리드 안에 카드 생성 (첨부 이미지처럼 한 줄에 5장씩 나열)
        Vector2 cellSize = new Vector2(180f, 256f);
        for (int i = 0; i < entries.Count; i++)
        {
            DeckCardEntry entry = entries[i];
            GameObject cardObj = CreatePreviewCardObject(
                pileGridContentRect,
                $"PileCard_{i + 1}",
                entry.ownerPlayerIndex,
                entry.ownerUnit,
                entry.cardColor,
                Vector2.zero,
                cellSize,
                useManualPosition: false
            );
            spawnedCardPreviews.Add(cardObj);
        }

        // 그리드 행 수에 맞춰 스크롤 영역 높이 계산
        if (pileGridContentRect != null)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(entries.Count / 5f));
            float contentHeight = Mathf.Max(560f, rows * 256f + (rows - 1) * 28f + 40f);
            pileGridContentRect.sizeDelta = new Vector2(1020f, contentHeight);
            pileGridContentRect.anchoredPosition = Vector2.zero;
        }

        if (pileScrollRect != null)
        {
            pileScrollRect.verticalNormalizedPosition = 1f;
        }

        ShowModalWithAnimation(pileViewerGroupRect);
    }

    private void ShowModalWithAnimation(RectTransform targetWindow)
    {
        rootRect.SetAsLastSibling();
        rootCanvasGroup.gameObject.SetActive(true);
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;

        if (openAnimCoroutine != null)
            StopCoroutine(openAnimCoroutine);
        openAnimCoroutine = StartCoroutine(CoAnimateOpen(targetWindow));
    }

    private IEnumerator CoAnimateOpen(RectTransform targetWindow)
    {
        float duration = 0.16f;
        float elapsed = 0f;

        rootCanvasGroup.alpha = 0f;
        if (targetWindow != null)
            targetWindow.localScale = new Vector3(0.92f, 0.92f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            rootCanvasGroup.alpha = eased;
            if (targetWindow != null)
                targetWindow.localScale = Vector3.Lerp(new Vector3(0.92f, 0.92f, 1f), Vector3.one, eased);

            yield return null;
        }

        rootCanvasGroup.alpha = 1f;
        if (targetWindow != null)
            targetWindow.localScale = Vector3.one;
        openAnimCoroutine = null;
    }

    public void OnClickClose()
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
        ClearSpawnedCardPreviews();

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void ClearSpawnedCardPreviews()
    {
        for (int i = spawnedCardPreviews.Count - 1; i >= 0; i--)
        {
            if (spawnedCardPreviews[i] != null)
                Destroy(spawnedCardPreviews[i]);
        }
        spawnedCardPreviews.Clear();
    }

    // =========================================================================
    // 카드 미리보기 오브젝트 생성 (실제 인게임 카드와 동일한 비주얼 + 호버 확대)
    // =========================================================================
    private GameObject CreatePreviewCardObject(
        Transform parent,
        string objName,
        int ownerPlayerIndex,
        SelectableUnit ownerUnit,
        ECardColor cardColor,
        Vector2 anchoredPos,
        Vector2 cardSize,
        bool useManualPosition)
    {
        GameObject cardObj = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.layer = LayerMask.NameToLayer("UI");
        cardObj.transform.SetParent(parent, false);

        RectTransform rt = cardObj.GetComponent<RectTransform>();
        if (useManualPosition)
        {
            UIHelper.ConfigureCenteredRect(rt, anchoredPos, cardSize);
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = cardSize;
            rt.localScale = Vector3.one;
        }

        // 1) 카드 외곽 프레임 이미지 (Red / Blue / Green)
        Image frameImg = cardObj.GetComponent<Image>();
        Sprite frameSp = HandManager.Ins != null ? HandManager.Ins.GetSpriteForColor(cardColor) : null;
        if (frameSp == null)
        {
            string path = cardColor == ECardColor.ARTS
                ? "Assets/Resource/UI/card_neon_blue_blank.png"
                : (cardColor == ECardColor.QUICK ? "Assets/Resource/UI/card_neon_green_blank.png" : "Assets/Resource/UI/card_neon_red_blank.png");
            frameSp = UIHelper.LoadSpriteFromAssetOrDisk(path);
        }
        frameImg.sprite = frameSp;
        frameImg.color = Color.white;
        frameImg.raycastTarget = true;

        // 마우스 호버 시 카드 확대 (IPointerEnterHandler / IPointerExitHandler)
        UIHelper.EnsureHoverScale(cardObj, 1.12f);

        // 2) 상단 그림칸 소유 플레이어 도형 이미지
        GameObject portraitObj = new GameObject("OwnerPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitObj.layer = cardObj.layer;
        portraitObj.transform.SetParent(cardObj.transform, false);

        RectTransform pRt = portraitObj.GetComponent<RectTransform>();
        UIHelper.ConfigureCenteredRect(pRt, new Vector2(0f, 38f), new Vector2(88f, 88f));

        Image pImg = portraitObj.GetComponent<Image>();
        pImg.sprite = ResolveOwnerPortraitSprite(ownerPlayerIndex, ownerUnit, cardColor);
        pImg.preserveAspect = true;
        pImg.raycastTarget = false;

        // 3) 좌측 상단 코스트 숫자
        int effCost = cardColor.GetBaseCost();
        if (HandManager.Ins != null && HandManager.Ins.IsZeroCostQuickBusterActive &&
            (cardColor == ECardColor.QUICK || cardColor.IsBusterAttribute()))
        {
            effCost = 0;
        }

        Color costCol = effCost < cardColor.GetBaseCost() ? new Color(0.45f, 1f, 0.55f, 1f) : Color.white;
        UIHelper.CreateOutlinedText(
            cardObj.transform,
            "CostText",
            effCost.ToString(),
            22,
            FontStyle.Bold,
            costCol,
            new Vector2(26.5f, -26.5f),
            new Vector2(40f, 40f),
            TextAnchor.MiddleCenter,
            new Color(0.08f, 0.04f, 0.02f, 0.98f),
            new Vector2(1.8f, -1.8f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f)
        );

        // 4) 카드 종류 라벨 (Arts / Quick / Buster)
        Color labelCol;
        string descStr;
        float rawDmg = ownerUnit != null ? ownerUnit.MaxAttack : 5f;
        switch (cardColor)
        {
            case ECardColor.ARTS:
                labelCol = new Color(0.55f, 0.92f, 1f, 1f);
                descStr = $"피해 {UIHelper.FormatCompactNumber(rawDmg)}\n카드 2장 드로우";
                break;
            case ECardColor.QUICK:
                labelCol = new Color(0.55f, 1f, 0.72f, 1f);
                descStr = $"피해 {UIHelper.FormatCompactNumber(rawDmg)}\n스타 획득 (1~3개)";
                break;
            default:
                labelCol = new Color(1f, 0.65f, 0.55f, 1f);
                descStr = $"피해 {UIHelper.FormatCompactNumber(rawDmg * 1.5f)}\n강력한 공격 (1.5배)";
                break;
        }

        UIHelper.CreateOutlinedText(
            cardObj.transform,
            "CardTypeLabel",
            cardColor.GetCardName(),
            17,
            FontStyle.Bold,
            labelCol,
            new Vector2(0f, -15f),
            new Vector2(150f, 26f),
            TextAnchor.MiddleCenter,
            new Color(0.01f, 0.02f, 0.06f, 0.98f),
            new Vector2(1.4f, -1.4f)
        );

        // 5) 하단 카드 효과 설명 텍스트
        UIHelper.CreateOutlinedText(
            cardObj.transform,
            "CardEffectDesc",
            descStr,
            12,
            FontStyle.Bold,
            new Color(0.92f, 0.95f, 1f, 0.96f),
            new Vector2(0f, 22f),
            new Vector2(148f, 42f),
            TextAnchor.MiddleCenter,
            new Color(0.01f, 0.02f, 0.06f, 0.98f),
            new Vector2(1.2f, -1.2f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f)
        );

        return cardObj;
    }

    private static Sprite ResolveOwnerPortraitSprite(int playerIndex, SelectableUnit ownerUnit, ECardColor cardColor)
    {
        if (cardColor == ECardColor.SUPER_BUSTER)
        {
            Sprite sb = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/Charactor/player_SuperBuster.png");
            if (sb != null) return sb;
        }

        if (ownerUnit != null && ownerUnit.UnitPortraitSprite != null)
            return ownerUnit.UnitPortraitSprite;

        int idx = Mathf.Clamp(playerIndex, 0, 2);
        string path = idx == 0
            ? "Assets/Resource/Charactor/player_Arts.png"
            : (idx == 1 ? "Assets/Resource/Charactor/player_Quick.png" : "Assets/Resource/Charactor/player_Buster.png");

        return UIHelper.LoadSpriteFromAssetOrDisk(path);
    }

    // =========================================================================
    // 모달 UI 계층 구조 자동 생성
    // =========================================================================
    public void EnsureModalUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (rootRect == null)
        {
            Transform existing = canvas.transform.Find("DeckViewerModalGroup");
            if (existing != null)
            {
                rootRect = existing.GetComponent<RectTransform>();
                rootCanvasGroup = existing.GetComponent<CanvasGroup>();
                rootDimImage = existing.GetComponent<Image>();
            }
        }

        if (rootRect == null)
        {
            GameObject rootObj = new GameObject("DeckViewerModalGroup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Button));
            rootObj.layer = LayerMask.NameToLayer("UI");
            rootObj.transform.SetParent(canvas.transform, false);

            rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            rootDimImage = rootObj.GetComponent<Image>();
            rootDimImage.sprite = UIHelper.GetOrCreateSolidWhiteSprite();
            rootDimImage.color = new Color(0.01f, 0.02f, 0.06f, 0.78f);
            rootDimImage.raycastTarget = true;

            rootCanvasGroup = rootObj.GetComponent<CanvasGroup>();
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;

            Button bgCloseBtn = rootObj.GetComponent<Button>();
            bgCloseBtn.transition = Selectable.Transition.None;
            bgCloseBtn.onClick.AddListener(OnClickClose);
        }

        EnsurePlayerDeckWindow();
        EnsurePileViewerGroup();
    }

    /// <summary>
    /// 1) 플레이어 오브젝트 클릭 시 열리는 SkillConfirmModalGroup 스타일 "덱" 박스 생성
    /// </summary>
    private void EnsurePlayerDeckWindow()
    {
        if (rootRect == null) return;

        Transform winTr = rootRect.Find("PlayerDeckModalWindow");
        if (winTr == null)
        {
            GameObject winObj = new GameObject("PlayerDeckModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            winObj.layer = LayerMask.NameToLayer("UI");
            winObj.transform.SetParent(rootRect, false);

            playerDeckWindowRect = winObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(playerDeckWindowRect, new Vector2(0f, 15f), new Vector2(1140f, 470f));

            Image winImg = winObj.GetComponent<Image>();
            winImg.sprite = SkillConfirmModalUI.GetOrCreateModalWindowSprite();
            winImg.type = Image.Type.Sliced;
            winImg.color = Color.white;
            winImg.raycastTarget = true;
        }
        else
        {
            playerDeckWindowRect = winTr.GetComponent<RectTransform>();
        }

        // 상단 타이틀 "덱"
        Transform titleTr = playerDeckWindowRect.Find("DeckModalTitle");
        if (titleTr == null)
        {
            playerDeckTitleText = UIHelper.CreateOutlinedText(
                playerDeckWindowRect,
                "DeckModalTitle",
                "덱",
                34,
                FontStyle.Bold,
                new Color(0.74f, 0.94f, 1.0f, 1f),
                new Vector2(0f, 185f),
                new Vector2(460f, 46f),
                TextAnchor.MiddleCenter,
                new Color(0.06f, 0.25f, 0.72f, 0.98f),
                new Vector2(2.2f, -2.2f)
            );
        }
        else
        {
            playerDeckTitleText = UIHelper.GetOrAddTMPText(titleTr.gameObject);
        }

        // 영문 서브 타이틀 ("Deck")
        if (playerDeckWindowRect.Find("DeckModalSubLabel") == null)
        {
            UIHelper.CreateOutlinedText(
                playerDeckWindowRect,
                "DeckModalSubLabel",
                "D  e  c  k",
                13,
                FontStyle.Normal,
                new Color(0.55f, 0.70f, 0.92f, 0.78f),
                new Vector2(0f, 150f),
                new Vector2(300f, 22f),
                TextAnchor.MiddleCenter,
                new Color(0.01f, 0.02f, 0.08f, 0.9f),
                new Vector2(1f, -1f)
            );
        }

        // 상단 구분선
        if (playerDeckWindowRect.Find("HeaderDividerLine") == null)
        {
            GameObject divObj = new GameObject("HeaderDividerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divObj.layer = LayerMask.NameToLayer("UI");
            divObj.transform.SetParent(playerDeckWindowRect, false);

            RectTransform divRt = divObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(divRt, new Vector2(0f, 148f), new Vector2(980f, 4f));

            Image divImg = divObj.GetComponent<Image>();
            divImg.sprite = SkillConfirmModalUI.GetOrCreateDividerLineSprite();
            divImg.raycastTarget = false;
        }

        // 플레이어 이름 & 카드 구성 안내 텍스트
        Transform subTr = playerDeckWindowRect.Find("PlayerDeckOwnerInfo");
        if (subTr == null)
        {
            playerDeckSubTitleText = UIHelper.CreateOutlinedText(
                playerDeckWindowRect,
                "PlayerDeckOwnerInfo",
                "Player 1 보유 카드 (5장)",
                20,
                FontStyle.Bold,
                new Color(0.92f, 0.96f, 1.0f, 0.96f),
                new Vector2(0f, 120f),
                new Vector2(880f, 32f),
                TextAnchor.MiddleCenter,
                new Color(0.02f, 0.04f, 0.10f, 0.95f),
                new Vector2(1.5f, -1.5f)
            );
        }
        else
        {
            playerDeckSubTitleText = UIHelper.GetOrAddTMPText(subTr.gameObject);
        }

        // 카드 5장이 나열될 컨테이너
        Transform cardContTr = playerDeckWindowRect.Find("PlayerDeckCardContainer");
        if (cardContTr == null)
        {
            GameObject contObj = new GameObject("PlayerDeckCardContainer", typeof(RectTransform));
            contObj.layer = LayerMask.NameToLayer("UI");
            contObj.transform.SetParent(playerDeckWindowRect, false);

            playerDeckCardContainer = contObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(playerDeckCardContainer, new Vector2(0f, -22f), new Vector2(1060f, 290f));
        }
        else
        {
            playerDeckCardContainer = cardContTr.GetComponent<RectTransform>();
        }

        // 우상단 다이아몬드 [X] 닫기 버튼 (SkillConfirmModalGroup과 동일한 디자인 + 호버 확대)
        Transform closeXTr = playerDeckWindowRect.Find("CloseXButton");
        if (closeXTr == null)
        {
            GameObject xObj = new GameObject("CloseXButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            xObj.layer = LayerMask.NameToLayer("UI");
            xObj.transform.SetParent(playerDeckWindowRect, false);

            RectTransform xRt = xObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(xRt, new Vector2(538f, 198f), new Vector2(78f, 78f));

            Image xImg = xObj.GetComponent<Image>();
            xImg.sprite = SkillConfirmModalUI.GetOrCreateDiamondCloseSprite();
            xImg.preserveAspect = true;
            xImg.raycastTarget = true;

            playerDeckCloseXButton = xObj.GetComponent<Button>();
            playerDeckCloseXButton.targetGraphic = xImg;
            playerDeckCloseXButton.transition = Selectable.Transition.None;

            UIHelper.CreateOutlinedText(
                xObj.transform,
                "XLabel",
                "X",
                30,
                FontStyle.Bold,
                new Color(0.14f, 0.17f, 0.24f, 1f),
                Vector2.zero,
                new Vector2(64f, 64f),
                TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.65f),
                new Vector2(1f, -1f)
            );
        }
        else
        {
            playerDeckCloseXButton = closeXTr.GetComponent<Button>();
        }

        if (playerDeckCloseXButton != null)
        {
            playerDeckCloseXButton.onClick.RemoveAllListeners();
            playerDeckCloseXButton.onClick.AddListener(OnClickClose);
            UIHelper.EnsureHoverScale(playerDeckCloseXButton.gameObject, 1.15f);
        }
    }

    /// <summary>
    /// 2) DrawPileUI / DiscardPileUI 클릭 시 열리는 첨부 이미지(Slay the Spire) 레이아웃 생성
    /// </summary>
    private void EnsurePileViewerGroup()
    {
        if (rootRect == null) return;

        Transform grpTr = rootRect.Find("PileViewerGroup");
        if (grpTr == null)
        {
            GameObject grpObj = new GameObject("PileViewerGroup", typeof(RectTransform));
            grpObj.layer = LayerMask.NameToLayer("UI");
            grpObj.transform.SetParent(rootRect, false);

            pileViewerGroupRect = grpObj.GetComponent<RectTransform>();
            pileViewerGroupRect.anchorMin = Vector2.zero;
            pileViewerGroupRect.anchorMax = Vector2.one;
            pileViewerGroupRect.offsetMin = Vector2.zero;
            pileViewerGroupRect.offsetMax = Vector2.zero;
            pileViewerGroupRect.localScale = Vector3.one;
        }
        else
        {
            pileViewerGroupRect = grpTr.GetComponent<RectTransform>();
        }

        // (A) 중앙 5열 카드 그리드 & ScrollRect + 우측 스크롤바 (첨부 이미지 중앙 5x2 그리드 + 우측 세로 스크롤바)
        Transform scrollTr = pileViewerGroupRect.Find("PileCardScrollView");
        if (scrollTr == null)
        {
            GameObject scrollObj = new GameObject("PileCardScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollObj.layer = LayerMask.NameToLayer("UI");
            scrollObj.transform.SetParent(pileViewerGroupRect, false);

            RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(scrollRt, new Vector2(0f, 12f), new Vector2(1100f, 640f));

            // Viewport
            GameObject vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            vpObj.layer = LayerMask.NameToLayer("UI");
            vpObj.transform.SetParent(scrollObj.transform, false);

            RectTransform vpRt = vpObj.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = new Vector2(-36f, 0f);
            vpRt.localScale = Vector3.one;

            Image vpImg = vpObj.GetComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.01f);
            vpImg.raycastTarget = true;

            // Content (5열 GridLayoutGroup)
            GameObject contentObj = new GameObject("GridContent", typeof(RectTransform), typeof(GridLayoutGroup));
            contentObj.layer = LayerMask.NameToLayer("UI");
            contentObj.transform.SetParent(vpObj.transform, false);

            pileGridContentRect = contentObj.GetComponent<RectTransform>();
            pileGridContentRect.anchorMin = new Vector2(0.5f, 1f);
            pileGridContentRect.anchorMax = new Vector2(0.5f, 1f);
            pileGridContentRect.pivot = new Vector2(0.5f, 1f);
            pileGridContentRect.anchoredPosition = Vector2.zero;
            pileGridContentRect.sizeDelta = new Vector2(1040f, 620f);
            pileGridContentRect.localScale = Vector3.one;

            GridLayoutGroup grid = contentObj.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 256f);
            grid.spacing = new Vector2(26f, 28f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.padding = new RectOffset(10, 10, 16, 20);

            // 우측 세로 스크롤바 (첨부 이미지 우측의 금색 다이아몬드 핸들 스크롤바)
            GameObject sbObj = new GameObject("RightScrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            sbObj.layer = LayerMask.NameToLayer("UI");
            sbObj.transform.SetParent(scrollObj.transform, false);

            RectTransform sbRt = sbObj.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.anchoredPosition = new Vector2(-4f, 0f);
            sbRt.sizeDelta = new Vector2(18f, -24f);
            sbRt.localScale = Vector3.one;

            Image sbTrackImg = sbObj.GetComponent<Image>();
            sbTrackImg.sprite = UIHelper.GetOrCreateSolidWhiteSprite();
            sbTrackImg.color = new Color(0.10f, 0.14f, 0.20f, 0.92f);

            GameObject slideArea = new GameObject("SlidingArea", typeof(RectTransform));
            slideArea.layer = LayerMask.NameToLayer("UI");
            slideArea.transform.SetParent(sbObj.transform, false);
            RectTransform saRt = slideArea.GetComponent<RectTransform>();
            saRt.anchorMin = Vector2.zero;
            saRt.anchorMax = Vector2.one;
            saRt.offsetMin = new Vector2(2f, 2f);
            saRt.offsetMax = new Vector2(-2f, -2f);
            saRt.localScale = Vector3.one;

            GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleObj.layer = LayerMask.NameToLayer("UI");
            handleObj.transform.SetParent(slideArea.transform, false);
            RectTransform hRt = handleObj.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(10f, 10f);
            hRt.localScale = Vector3.one;

            Image hImg = handleObj.GetComponent<Image>();
            hImg.sprite = GetOrCreateGoldScrollbarHandleSprite();
            hImg.type = Image.Type.Sliced;
            hImg.color = Color.white;

            Scrollbar sb = sbObj.GetComponent<Scrollbar>();
            sb.handleRect = hRt;
            sb.targetGraphic = hImg;
            sb.direction = Scrollbar.Direction.BottomToTop;

            pileScrollRect = scrollObj.GetComponent<ScrollRect>();
            pileScrollRect.viewport = vpRt;
            pileScrollRect.content = pileGridContentRect;
            pileScrollRect.horizontal = false;
            pileScrollRect.vertical = true;
            pileScrollRect.scrollSensitivity = 32f;
            pileScrollRect.movementType = ScrollRect.MovementType.Clamped;
            pileScrollRect.verticalScrollbar = sb;
            pileScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }
        else
        {
            pileScrollRect = scrollTr.GetComponent<ScrollRect>();
            Transform contentTr = scrollTr.Find("Viewport/GridContent");
            if (contentTr != null)
                pileGridContentRect = contentTr.GetComponent<RectTransform>();
        }

        // 빈 더미 안내 문구
        Transform emptyTr = pileViewerGroupRect.Find("EmptyPileNoticeText");
        if (emptyTr == null)
        {
            emptyPileNoticeText = UIHelper.CreateOutlinedText(
                pileViewerGroupRect,
                "EmptyPileNoticeText",
                "현재 더미에 카드가 없습니다.",
                26,
                FontStyle.Bold,
                new Color(0.80f, 0.86f, 0.95f, 0.90f),
                new Vector2(0f, 15f),
                new Vector2(680f, 64f),
                TextAnchor.MiddleCenter,
                new Color(0.01f, 0.02f, 0.05f, 0.98f),
                new Vector2(2f, -2f)
            );
            emptyPileNoticeText.gameObject.SetActive(false);
        }
        else
        {
            emptyPileNoticeText = UIHelper.GetOrAddTMPText(emptyTr.gameObject);
        }

        // (C) 좌측 중앙 붉은 리본 [↩] 뒤로가기 버튼 (첨부 이미지 좌측 빨간색 리본 화살표 버튼)
        Transform backBtnTr = pileViewerGroupRect.Find("LeftRibbonBackButton");
        if (backBtnTr == null)
        {
            GameObject backObj = new GameObject("LeftRibbonBackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            backObj.layer = LayerMask.NameToLayer("UI");
            backObj.transform.SetParent(pileViewerGroupRect, false);

            RectTransform backRt = backObj.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.anchoredPosition = new Vector2(0f, -65f);
            backRt.sizeDelta = new Vector2(126f, 74f);
            backRt.localScale = Vector3.one;

            Image backImg = backObj.GetComponent<Image>();
            backImg.sprite = GetOrCreateRedRibbonBackSprite();
            backImg.color = Color.white;
            backImg.raycastTarget = true;

            leftRibbonBackButton = backObj.GetComponent<Button>();
            leftRibbonBackButton.targetGraphic = backImg;
            leftRibbonBackButton.transition = Selectable.Transition.None;

            UIHelper.CreateOutlinedText(
                backObj.transform,
                "BackArrowText",
                "↩",
                38,
                FontStyle.Bold,
                new Color(0.98f, 0.94f, 0.85f, 1f),
                new Vector2(-10f, 0f),
                new Vector2(96f, 64f),
                TextAnchor.MiddleCenter,
                new Color(0.18f, 0.04f, 0.02f, 0.98f),
                new Vector2(2.2f, -2.2f)
            );
        }
        else
        {
            leftRibbonBackButton = backBtnTr.GetComponent<Button>();
        }

        if (leftRibbonBackButton != null)
        {
            leftRibbonBackButton.onClick.RemoveAllListeners();
            leftRibbonBackButton.onClick.AddListener(OnClickClose);
            UIHelper.EnsureHoverScale(leftRibbonBackButton.gameObject, 1.14f);
        }

        // (D) 하단 중앙 설명 박스 ("매 턴마다 이곳에서 카드를 뽑아 전투를 시작합니다.")
        Transform bottomBannerTr = pileViewerGroupRect.Find("BottomHintBanner");
        if (bottomBannerTr == null)
        {
            GameObject bannerObj = new GameObject("BottomHintBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bannerObj.layer = LayerMask.NameToLayer("UI");
            bannerObj.transform.SetParent(pileViewerGroupRect, false);

            RectTransform bannerRt = bannerObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(bannerRt, new Vector2(0f, -385f), new Vector2(640f, 44f));

            Image bannerImg = bannerObj.GetComponent<Image>();
            bannerImg.sprite = UIHelper.GetOrCreateSolidWhiteSprite();
            bannerImg.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
            bannerImg.raycastTarget = false;

            bottomHintBannerText = UIHelper.CreateOutlinedText(
                bannerObj.transform,
                "HintText",
                "매 턴마다 이곳에서 카드를 뽑아 전투를 시작합니다.",
                18,
                FontStyle.Bold,
                new Color(0.95f, 0.96f, 0.98f, 1f),
                Vector2.zero,
                new Vector2(620f, 40f),
                TextAnchor.MiddleCenter,
                new Color(0f, 0f, 0f, 0.98f),
                new Vector2(1.4f, -1.4f)
            );
        }
        else
        {
            Transform ht = bottomBannerTr.Find("HintText");
            if (ht != null)
                bottomHintBannerText = UIHelper.GetOrAddTMPText(ht.gameObject);
        }
    }

    // =========================================================================
    // 프로시저럴 UI 스프라이트 (좌측 붉은 제비꼬리 리본 버튼 / 금색 스크롤바 핸들)
    // =========================================================================
    private static Sprite GetOrCreateRedRibbonBackSprite()
    {
        if (cachedRedRibbonBackSprite != null) return cachedRedRibbonBackSprite;

        int w = 128;
        int h = 72;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color crimsonTop = new Color(0.76f, 0.18f, 0.14f, 0.98f);
        Color crimsonBot = new Color(0.52f, 0.10f, 0.08f, 0.98f);
        Color goldBorder = new Color(0.82f, 0.65f, 0.36f, 0.98f);
        float halfH = (h - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            float dyNorm = Mathf.Abs(y - halfH) / halfH; // 0(중앙) ~ 1(상/하단)
            // 오른쪽 끝이 안쪽으로 파인 제비꼬리(Swallowtail Ribbon) 형태
            float maxRightX = Mathf.Lerp(w - 28f, w - 4f, dyNorm);
            Color fillCol = Color.Lerp(crimsonBot, crimsonTop, (float)y / (h - 1));

            for (int x = 0; x < w; x++)
            {
                if (x <= maxRightX && y >= 2 && y <= h - 3)
                {
                    bool isTopOrBotBorder = (y <= 5 || y >= h - 6);
                    bool isRightNotchBorder = (maxRightX - x) <= 3.5f;
                    pixels[y * w + x] = (isTopOrBotBorder || isRightNotchBorder) ? goldBorder : fillCol;
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedRedRibbonBackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedRedRibbonBackSprite.name = "ui_sts_red_ribbon_back";
        return cachedRedRibbonBackSprite;
    }

    private static Sprite GetOrCreateGoldScrollbarHandleSprite()
    {
        if (cachedDiamondHandleSprite != null) return cachedDiamondHandleSprite;

        int w = 24;
        int h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color goldCore = new Color(0.92f, 0.76f, 0.32f, 0.98f);
        Color goldDark = new Color(0.62f, 0.46f, 0.16f, 0.98f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Min(x, w - 1 - x);
                int dy = Mathf.Min(y, h - 1 - y);
                int corner = dx + dy - 6;
                int dist = Mathf.Min(Mathf.Min(dx, dy), corner);
                if (dist >= 0)
                {
                    pixels[y * w + x] = dist < 2 ? goldDark : goldCore;
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedDiamondHandleSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 8, 6, 8));
        cachedDiamondHandleSprite.name = "ui_sts_gold_scrollbar_handle";
        return cachedDiamondHandleSprite;
    }
}
