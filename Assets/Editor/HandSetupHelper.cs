using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HandSetupHelper
{
    static HandSetupHelper()
    {
        EditorApplication.delayCall += TrySetupHandAndAttackBox;
    }

    [MenuItem("Tools/Zeometry Slayer/플레이어 손패 & Attack Box & 전투/턴 시스템 자동 세팅")]
    public static void TrySetupHandAndAttackBox()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool sceneModified = false;

        // 1. 카드 3종 스프라이트 로드 (card_neon_red_blank, card_neon_blue_blank, card_neon_green_blank)
        Sprite redCardSprite = LoadSpriteWithFallback("Assets/Resource/UI/card_neon_red_blank.png", "Assets/Resource/card_neon_red_blank.png");
        Sprite blueCardSprite = LoadSpriteWithFallback("Assets/Resource/UI/card_neon_blue_blank.png", "Assets/Resource/card_neon_blue_blank.png");
        Sprite greenCardSprite = LoadSpriteWithFallback("Assets/Resource/UI/card_neon_green_blank.png", "Assets/Resource/card_neon_green_blank.png");
        if (blueCardSprite == null) return;

        // 2. Attack Box 전용 네온 프레임 이미지 로드
        string attackBoxPath = "Assets/Resource/UI/attack_box_neon_blue.png";
        EnsureAttackBoxTextureExists(attackBoxPath);
        Sprite attackBoxSprite = AssetDatabase.LoadAssetAtPath<Sprite>(attackBoxPath);

        // 3. 순서 표시용 금색 숫자 이미지(1~6) PNG 생성 및 로드
        Sprite[] goldNumberSprites = new Sprite[6];
        for (int i = 0; i < 6; i++)
        {
            string numAssetPath = $"Assets/Resource/UI/order_num_gold_{i + 1}.png";
            EnsureGoldNumberTextureExists(numAssetPath, i + 1);
            goldNumberSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(numAssetPath);
        }

        // 4. 전투 이펙트 이미지 생성 및 로드
        string[] colorNames = { "red", "blue", "green" };
        Sprite[] playerAuraSprites = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            ECardColor col = (ECardColor)i;
            string auraPath = $"Assets/Resource/Effact/fx_player_aura_{colorNames[i]}.png";
            EnsureEffectTextureExists(auraPath, () => CombatEffectTextureGenerator.CreatePlayerCommonAuraTexture(col, 256));
            playerAuraSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(auraPath);
        }

        string slashPath = "Assets/Resource/Effact/fx_enemy_hit_slash.png";
        EnsureEffectTextureExists(slashPath, () => CombatEffectTextureGenerator.CreateSimpleHitSlashTexture(256));
        Sprite enemyHitSlashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(slashPath);

        // 4-1. 플레이어별(Player_1, Player_2, Player_3) 전용 공격 오라 & 타격 이펙트 6종 생성 및 로드
        string[] specificAuraPaths =
        {
            "Assets/Resource/Effact/fx_player1_aura_triangle.png",
            "Assets/Resource/Effact/fx_player2_aura_octagon.png",
            "Assets/Resource/Effact/fx_player3_aura_pentagon.png"
        };
        string[] specificHitPaths =
        {
            "Assets/Resource/Effact/fx_player1_hit_prism_laser.png",
            "Assets/Resource/Effact/fx_player2_hit_sonic_flurry.png",
            "Assets/Resource/Effact/fx_player3_hit_buster_nova.png"
        };
        Sprite[] playerSpecificAuraSprites = new Sprite[3];
        Sprite[] playerSpecificHitSprites = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            EnsureEffectTextureExists(specificAuraPaths[idx], () => CombatEffectTextureGenerator.CreatePlayerSpecificAuraTexture(idx, 512));
            playerSpecificAuraSprites[idx] = AssetDatabase.LoadAssetAtPath<Sprite>(specificAuraPaths[idx]);

            EnsureEffectTextureExists(specificHitPaths[idx], () => CombatEffectTextureGenerator.CreatePlayerSpecificHitTexture(idx, 512));
            playerSpecificHitSprites[idx] = AssetDatabase.LoadAssetAtPath<Sprite>(specificHitPaths[idx]);
        }

        // 4-2. 버프용 깃털(Feather), 코스트 보석(Gem), 크리티컬 스타(Star) 아이콘 이미지 생성 및 로드
        string featherPath = "Assets/Resource/UI/icon_buff_feather.png";
        EnsureEffectTextureExists(featherPath, () => UnitStatusUI.CreateFeatherTexture(64, 64));
        Sprite featherBuffSprite = AssetDatabase.LoadAssetAtPath<Sprite>(featherPath);

        string critStarPath = "Assets/Resource/UI/icon_critical_star.png";
        EnsureEffectTextureExists(critStarPath, () => BattleResourceUIManager.CreateCriticalStarTexture(96, 96));
        Sprite critStarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(critStarPath);

        // 4-3. 플레이어 스킬 아이콘 3종 (공격력 업, 방어력 업, 크리티컬 위력 업) 임포트 및 로드
        Sprite skillAttackUpSprite = EnsureSpriteImported("Assets/Resource/UI/icon_skill_attack_up.png");
        Sprite skillDefenseUpSprite = EnsureSpriteImported("Assets/Resource/UI/icon_skill_defense_up.png");
        Sprite skillCritDamageUpSprite = EnsureSpriteImported("Assets/Resource/UI/icon_skill_crit_damage_up.png");

        // 5. 턴 UI 이미지 6종 (좌측 상단 상태 배지 3종 + 중앙 슬라이딩 배너 3종) 임포트 및 로드
        Sprite statusPlayerSprite = EnsureSpriteImported("Assets/Resource/UI/turn_status_player.png");
        Sprite statusAttackSprite = EnsureSpriteImported("Assets/Resource/UI/turn_status_attack.png");
        Sprite statusEnemySprite  = EnsureSpriteImported("Assets/Resource/UI/turn_status_enemy.png");
        Sprite bannerPlayerSprite = EnsureSpriteImported("Assets/Resource/UI/turn_banner_player.png");
        Sprite bannerAttackSprite = EnsureSpriteImported("Assets/Resource/UI/turn_banner_attack.png");
        Sprite bannerEnemySprite  = EnsureSpriteImported("Assets/Resource/UI/turn_banner_enemy.png");

        // 6. 프리팹 폴더 및 Card_Neon_Blue 프리팹 생성/업데이트
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        string prefabPath = "Assets/Prefabs/Card_Neon_Blue.prefab";
        GameObject cardPrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (cardPrefabObj == null)
        {
            GameObject tempCard = new GameObject("Card_Neon_Blue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Card));
            RectTransform rt = tempCard.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(180f, 256f);

            Image img = tempCard.GetComponent<Image>();
            img.sprite = blueCardSprite;
            img.raycastTarget = true;

            CreateOrderNumberChild(tempCard.transform, goldNumberSprites[0]);

            cardPrefabObj = PrefabUtility.SaveAsPrefabAsset(tempCard, prefabPath);
            Object.DestroyImmediate(tempCard);
        }
        else if (cardPrefabObj.transform.Find("OrderNumber") == null)
        {
            GameObject contentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            CreateOrderNumberChild(contentsRoot.transform, goldNumberSprites[0]);
            PrefabUtility.SaveAsPrefabAsset(contentsRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(contentsRoot);
            cardPrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        Card cardPrefabComp = cardPrefabObj != null ? cardPrefabObj.GetComponent<Card>() : null;
        if (cardPrefabComp != null)
        {
            SerializedObject prefabCardSo = new SerializedObject(cardPrefabComp);
            bool prefabMod = false;
            SetObjRef(prefabCardSo, "redCardSprite", redCardSprite, ref prefabMod);
            SetObjRef(prefabCardSo, "blueCardSprite", blueCardSprite, ref prefabMod);
            SetObjRef(prefabCardSo, "greenCardSprite", greenCardSprite, ref prefabMod);
            if (prefabMod)
            {
                prefabCardSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cardPrefabObj);
            }
        }

        // 7. 현재 열린 씬의 Canvas 확인 및 Sorting Order 보정 (Screen Space - Camera 모드에서 캐릭터보다 위에 그려지도록 10 설정)
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (canvas.sortingOrder < 10)
        {
            canvas.sortingOrder = 10;
            sceneModified = true;
        }

        // 8. PlayerHand 오브젝트 확인 및 RED/BLUE/GREEN 스프라이트 연결
        HandManager existingHand = Object.FindFirstObjectByType<HandManager>();
        if (existingHand == null)
        {
            GameObject handObj = new GameObject("PlayerHand", typeof(RectTransform), typeof(HandManager));
            handObj.layer = LayerMask.NameToLayer("UI");
            handObj.transform.SetParent(canvas.transform, false);

            RectTransform handRect = handObj.GetComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.5f, 0f);
            handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.pivot = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 40f);
            handRect.sizeDelta = new Vector2(900f, 300f);

            existingHand = handObj.GetComponent<HandManager>();
            sceneModified = true;
        }

        if (existingHand != null)
        {
            SerializedObject so = new SerializedObject(existingHand);
            SetObjRef(so, "handContainer", existingHand.GetComponent<RectTransform>(), ref sceneModified);
            SetObjRef(so, "cardPrefab", cardPrefabComp, ref sceneModified);
            SetObjRef(so, "defaultCardSprite", blueCardSprite, ref sceneModified);
            SetObjRef(so, "redCardSprite", redCardSprite, ref sceneModified);
            SetObjRef(so, "blueCardSprite", blueCardSprite, ref sceneModified);
            SetObjRef(so, "greenCardSprite", greenCardSprite, ref sceneModified);

            SerializedProperty maxHandProp = so.FindProperty("maxHandSize");
            if (maxHandProp != null && maxHandProp.intValue < 10)
            {
                maxHandProp.intValue = 10;
                sceneModified = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            existingHand.EnsureDeckAndDiscardUI();
        }

        // 9. 리메이크에서 AttackBox는 Spline 화살표 타겟팅 방식으로 대체되었으므로 비활성화 유지
        Transform oldBoxTr = canvas.transform.Find("AttackBox");
        if (oldBoxTr != null && oldBoxTr.gameObject.activeSelf)
        {
            oldBoxTr.gameObject.SetActive(false);
            sceneModified = true;
        }

        // 10. 좌측 상단 현재 턴 표시 UI (TurnStatusUI) & 중앙 슬라이딩 배너 UI (TurnBannerUI) 생성
        Transform statusTr = canvas.transform.Find("TurnStatusUI");
        Image topLeftTurnImg;
        if (statusTr == null)
        {
            GameObject statusObj = new GameObject("TurnStatusUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            statusObj.layer = LayerMask.NameToLayer("UI");
            statusObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = statusObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(28f, -24f);
            rt.sizeDelta = new Vector2(320f, 80f);

            topLeftTurnImg = statusObj.GetComponent<Image>();
            topLeftTurnImg.sprite = statusPlayerSprite;
            topLeftTurnImg.raycastTarget = false;
            topLeftTurnImg.preserveAspect = true;
            sceneModified = true;
        }
        else
        {
            topLeftTurnImg = statusTr.GetComponent<Image>();
        }

        Transform bannerTr = canvas.transform.Find("TurnBannerUI");
        RectTransform centerBannerRect;
        Image centerBannerImg;
        if (bannerTr == null)
        {
            GameObject bannerObj = new GameObject("TurnBannerUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bannerObj.layer = LayerMask.NameToLayer("UI");
            bannerObj.transform.SetParent(canvas.transform, false);

            centerBannerRect = bannerObj.GetComponent<RectTransform>();
            centerBannerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerBannerRect.pivot = new Vector2(0.5f, 0.5f);
            centerBannerRect.anchoredPosition = new Vector2(-1500f, 0f);
            centerBannerRect.sizeDelta = new Vector2(900f, 160f);

            centerBannerImg = bannerObj.GetComponent<Image>();
            centerBannerImg.sprite = bannerPlayerSprite;
            centerBannerImg.raycastTarget = false;
            centerBannerImg.preserveAspect = true;
            bannerObj.SetActive(false);
            sceneModified = true;
        }
        else
        {
            centerBannerRect = bannerTr.GetComponent<RectTransform>();
            centerBannerImg = bannerTr.GetComponent<Image>();
        }

        // 11. Player(Player_1~3) 및 Enemy(Enemy_1~3) 하위 오브젝트에 SelectableUnit 부착, 능력치 및 체력바/버프/스킬 UI 세팅
        GameObject playerRootObj = GameObject.Find("Player");
        GameObject enemyRootObj = GameObject.Find("Enemy");
        List<SelectableUnit> playerUnitList = SetupSelectableChildren(
            playerRootObj != null ? playerRootObj.transform : null,
            true,
            featherBuffSprite,
            skillAttackUpSprite,
            skillDefenseUpSprite,
            skillCritDamageUpSprite,
            ref sceneModified
        );
        List<SelectableUnit> enemyUnitList = SetupSelectableChildren(
            enemyRootObj != null ? enemyRootObj.transform : null,
            false,
            featherBuffSprite,
            skillAttackUpSprite,
            skillDefenseUpSprite,
            skillCritDamageUpSprite,
            ref sceneModified
        );

        // 12. BattleManager & TurnUIController 오브젝트 생성 및 참조 연결
        BattleManager battleManager = Object.FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            GameObject bmObj = new GameObject("BattleManager", typeof(BattleManager), typeof(TurnUIController));
            battleManager = bmObj.GetComponent<BattleManager>();
            sceneModified = true;
        }

        TurnUIController turnUI = battleManager.GetComponent<TurnUIController>();
        if (turnUI == null)
        {
            turnUI = Object.FindFirstObjectByType<TurnUIController>();
            if (turnUI == null)
            {
                turnUI = battleManager.gameObject.AddComponent<TurnUIController>();
                sceneModified = true;
            }
        }

        if (turnUI != null)
        {
            SerializedObject uiSo = new SerializedObject(turnUI);
            SetObjRef(uiSo, "topLeftTurnImage", topLeftTurnImg, ref sceneModified);
            SetObjRef(uiSo, "playerTurnStatusSprite", statusPlayerSprite, ref sceneModified);
            SetObjRef(uiSo, "attackPhaseStatusSprite", statusAttackSprite, ref sceneModified);
            SetObjRef(uiSo, "enemyTurnStatusSprite", statusEnemySprite, ref sceneModified);
            SetObjRef(uiSo, "centerBannerRect", centerBannerRect, ref sceneModified);
            SetObjRef(uiSo, "centerBannerImage", centerBannerImg, ref sceneModified);
            SetObjRef(uiSo, "playerTurnBannerSprite", bannerPlayerSprite, ref sceneModified);
            SetObjRef(uiSo, "attackPhaseBannerSprite", bannerAttackSprite, ref sceneModified);
            SetObjRef(uiSo, "enemyTurnBannerSprite", bannerEnemySprite, ref sceneModified);
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (battleManager != null)
        {
            SerializedObject bmSo = new SerializedObject(battleManager);
            SetObjRef(bmSo, "turnUIController", turnUI, ref sceneModified);
            if (playerRootObj != null) SetObjRef(bmSo, "playerRoot", playerRootObj.transform, ref sceneModified);
            if (enemyRootObj != null)  SetObjRef(bmSo, "enemyRoot", enemyRootObj.transform, ref sceneModified);
            AssignUnitList(bmSo.FindProperty("playerUnits"), playerUnitList, ref sceneModified);
            AssignUnitList(bmSo.FindProperty("enemyUnits"), enemyUnitList, ref sceneModified);
            bmSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 13. CombatEffectManager 오브젝트 세팅 및 오라 3종 + 단일 참격 연결
        CombatEffectManager existingFxManager = Object.FindFirstObjectByType<CombatEffectManager>();
        if (existingFxManager == null)
        {
            GameObject fxObj = new GameObject("CombatEffectManager", typeof(CombatEffectManager));
            existingFxManager = fxObj.GetComponent<CombatEffectManager>();
            sceneModified = true;
        }

        if (existingFxManager != null)
        {
            SerializedObject fxSo = new SerializedObject(existingFxManager);
            if (playerUnitList.Count > 0 && playerUnitList[0] != null)
                SetObjRef(fxSo, "playerRenderer", playerUnitList[0].GetComponent<SpriteRenderer>(), ref sceneModified);
            if (enemyUnitList.Count > 0 && enemyUnitList[0] != null)
                SetObjRef(fxSo, "enemyRenderer", enemyUnitList[0].GetComponent<SpriteRenderer>(), ref sceneModified);

            AssignSpriteArray(fxSo.FindProperty("playerAuraSprites"), playerAuraSprites, ref sceneModified);
            SetObjRef(fxSo, "enemyHitSlashSprite", enemyHitSlashSprite, ref sceneModified);
            AssignSpriteArray(fxSo.FindProperty("playerSpecificAuraSprites"), playerSpecificAuraSprites, ref sceneModified);
            AssignSpriteArray(fxSo.FindProperty("playerSpecificHitSprites"), playerSpecificHitSprites, ref sceneModified);
            fxSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 14. PauseMenuManager (ESC 일시정지 어두운 배경 & 메뉴창 + 게임종료 버튼) 자동 세팅
        PauseMenuManager pauseManager = Object.FindFirstObjectByType<PauseMenuManager>();
        if (pauseManager == null && battleManager != null)
        {
            pauseManager = battleManager.gameObject.GetComponent<PauseMenuManager>();
            if (pauseManager == null)
            {
                pauseManager = battleManager.gameObject.AddComponent<PauseMenuManager>();
                sceneModified = true;
            }
        }

        if (pauseManager != null)
        {
            pauseManager.EnsurePauseMenuUI();
            Transform overlayTr = canvas.transform.Find("PauseOverlayPanel");
            if (overlayTr != null && overlayTr.gameObject.activeSelf)
            {
                overlayTr.gameObject.SetActive(false);
                sceneModified = true;
            }
        }

        // 15. BattleResultUIManager (승리 / 패배 결과 UI + 게임종료/다시하기 버튼) 자동 세팅
        BattleResultUIManager resultManager = Object.FindFirstObjectByType<BattleResultUIManager>();
        if (resultManager == null && battleManager != null)
        {
            resultManager = battleManager.gameObject.GetComponent<BattleResultUIManager>();
            if (resultManager == null)
            {
                resultManager = battleManager.gameObject.AddComponent<BattleResultUIManager>();
                sceneModified = true;
            }
        }

        if (resultManager != null)
        {
            resultManager.EnsureResultUI();
            resultManager.HideResultUI();
        }

        // 16. BattleResourceUIManager (좌측 하단 코스트 UI & 우측 하단 크리티컬 스타 UI) 자동 세팅
        BattleResourceUIManager resourceManager = Object.FindFirstObjectByType<BattleResourceUIManager>();
        if (resourceManager == null && battleManager != null)
        {
            resourceManager = battleManager.gameObject.GetComponent<BattleResourceUIManager>();
            if (resourceManager == null)
            {
                resourceManager = battleManager.gameObject.AddComponent<BattleResourceUIManager>();
                sceneModified = true;
            }
        }

        if (resourceManager != null)
        {
            SerializedObject resSo = new SerializedObject(resourceManager);
            SetObjRef(resSo, "criticalStarSprite", critStarSprite, ref sceneModified);
            resSo.ApplyModifiedPropertiesWithoutUndo();

            resourceManager.EnsureResourceUI();
            resourceManager.RefreshAllUI();
        }

        // 17. SkillConfirmModalUI 및 SplineTargetingArrow 자동 세팅
        SkillConfirmModalUI skillModal = Object.FindFirstObjectByType<SkillConfirmModalUI>();
        if (skillModal == null && battleManager != null)
        {
            skillModal = battleManager.gameObject.GetComponent<SkillConfirmModalUI>();
            if (skillModal == null)
            {
                skillModal = battleManager.gameObject.AddComponent<SkillConfirmModalUI>();
                sceneModified = true;
            }
        }
        if (skillModal != null)
        {
            skillModal.EnsureModalUI();
            skillModal.HideImmediate();
        }

        DeckViewerModalUI deckViewerModal = Object.FindFirstObjectByType<DeckViewerModalUI>();
        if (deckViewerModal == null && battleManager != null)
        {
            deckViewerModal = battleManager.gameObject.GetComponent<DeckViewerModalUI>();
            if (deckViewerModal == null)
            {
                deckViewerModal = battleManager.gameObject.AddComponent<DeckViewerModalUI>();
                sceneModified = true;
            }
        }
        if (deckViewerModal != null)
        {
            deckViewerModal.EnsureModalUI();
            deckViewerModal.HideImmediate();
        }

        Camera mainCam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (mainCam != null && mainCam.GetComponent<Physics2DRaycaster>() == null)
        {
            mainCam.gameObject.AddComponent<Physics2DRaycaster>();
            sceneModified = true;
        }

        SplineTargetingArrow splineArrow = Object.FindFirstObjectByType<SplineTargetingArrow>();
        if (splineArrow == null && battleManager != null)
        {
            splineArrow = battleManager.gameObject.GetComponent<SplineTargetingArrow>();
            if (splineArrow == null)
            {
                splineArrow = battleManager.gameObject.AddComponent<SplineTargetingArrow>();
                sceneModified = true;
            }
        }
        if (splineArrow != null)
        {
            splineArrow.EnsureArrowUI();
        }

        // 모든 UnitStatusUI / PlayerSkillUI에 Assets/Resource/UI 영구 스프라이트 재연결 보장
        UnitStatusUI[] allStatusUIs = Object.FindObjectsByType<UnitStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allStatusUIs != null)
        {
            for (int i = 0; i < allStatusUIs.Length; i++)
            {
                if (allStatusUIs[i] != null)
                    allStatusUIs[i].EnsureUIHierarchy();
            }
        }

        PlayerSkillUI[] allSkillUIs = Object.FindObjectsByType<PlayerSkillUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allSkillUIs != null)
        {
            for (int i = 0; i < allSkillUIs.Length; i++)
            {
                if (allSkillUIs[i] != null)
                    allSkillUIs[i].EnsureUIHierarchy();
            }
        }

        // 18. TutorialManager가 씬에 존재하는 경우 스킬 아이콘 3종 스프라이트 참조 자동 연결
        TutorialManager tutorialManager = Object.FindFirstObjectByType<TutorialManager>();
        if (tutorialManager != null)
        {
            SerializedObject tutSo = new SerializedObject(tutorialManager);
            SetObjRef(tutSo, "skillAttackUpSprite", skillAttackUpSprite, ref sceneModified);
            SetObjRef(tutSo, "skillDefenseUpSprite", skillDefenseUpSprite, ref sceneModified);
            SetObjRef(tutSo, "skillCritDamageUpSprite", skillCritDamageUpSprite, ref sceneModified);
            tutSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 19. 씬 내부에 남아있는 레거시 UI.Text 오브젝트가 있으면 전부 TextMeshProUGUI로 일괄 자동 변환
        Text[] remainingLegacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (remainingLegacyTexts != null && remainingLegacyTexts.Length > 0)
        {
            for (int i = 0; i < remainingLegacyTexts.Length; i++)
            {
                if (remainingLegacyTexts[i] != null)
                {
                    UIHelper.GetOrAddTMPText(remainingLegacyTexts[i].gameObject);
                    sceneModified = true;
                }
            }

            if (existingHand != null) existingHand.EnsureDeckAndDiscardUI();
            if (pauseManager != null) pauseManager.EnsurePauseMenuUI();
            if (resultManager != null) { resultManager.EnsureResultUI(); resultManager.HideResultUI(); }
            if (resourceManager != null) { resourceManager.EnsureResourceUI(); resourceManager.RefreshAllUI(); }
            if (skillModal != null) { skillModal.EnsureModalUI(); skillModal.HideImmediate(); }
            if (deckViewerModal != null) { deckViewerModal.EnsureModalUI(); deckViewerModal.HideImmediate(); }
        }

        // 20. 사이버 시티 동적 배경 맵 (Quad + URP 네온 그리드 셰이더 + 기하학 네온 파티클) 자동 세팅
        EnsureCyberCityBackgroundInActiveScene(ref sceneModified);

        // 21. 중요: 씬 파일(level0)에 인메모리 TMP_FontAsset / Material / Texture2D / Sprite가 내장되어
        // 빌드 실행 시 "level0 is corrupted! [Position out of bounds!]" 오류가 나는 것을 방지하기 위해
        // 디스크에 존재하지 않는(!EditorUtility.IsPersistent) 인메모리 에셋 참조를 씬에서 모두 정리합니다.
        SanitizeSceneNonPersistentAssets(ref sceneModified);

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("[HandSetupHelper] 사이버 시티 동적 배경 맵 & 전투/UI 세팅 및 씬 내장 에셋 정리가 완료되었습니다!");
        }

        SanitizeOtherScenesOnce();
    }

    private static bool isSanitizingOtherScenes = false;

    private static void SanitizeOtherScenesOnce()
    {
        if (isSanitizingOtherScenes) return;
        if (SessionState.GetBool("ZeometrySlayer_AllScenesSanitized_v7", false)) return;

        SessionState.SetBool("ZeometrySlayer_AllScenesSanitized_v7", true);
        isSanitizingOtherScenes = true;

        try
        {
            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            string[] scenePaths = { "Assets/Scenes/TutorialScene.unity", "Assets/Scenes/BattleScene.unity" };

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string path = scenePaths[i];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                var openedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool mod = false;

                // 먼저 씬 내부의 비영구 MonoScript / Missing Script를 정리한 뒤 Ensure 메서드로 영구 .cs GUID 기반 재부착
                SanitizeSceneNonPersistentAssets(ref mod);
                EnsureCyberCityBackgroundInActiveScene(ref mod);

                HandManager hm = Object.FindFirstObjectByType<HandManager>();
                if (hm != null) { hm.EnsureDeckAndDiscardUI(); mod = true; }

                PauseMenuManager pm = Object.FindFirstObjectByType<PauseMenuManager>();
                if (pm != null)
                {
                    pm.EnsurePauseMenuUI();
                    Canvas cv = Object.FindFirstObjectByType<Canvas>();
                    Transform ov = cv != null ? cv.transform.Find("PauseOverlayPanel") : null;
                    if (ov != null && ov.gameObject.activeSelf) ov.gameObject.SetActive(false);
                    mod = true;
                }

                BattleResultUIManager rm = Object.FindFirstObjectByType<BattleResultUIManager>();
                if (rm != null) { rm.EnsureResultUI(); rm.HideResultUI(); mod = true; }

                BattleResourceUIManager resm = Object.FindFirstObjectByType<BattleResourceUIManager>();
                if (resm != null) { resm.EnsureResourceUI(); resm.RefreshAllUI(); mod = true; }

                SkillConfirmModalUI sm = Object.FindFirstObjectByType<SkillConfirmModalUI>();
                if (sm != null) { sm.EnsureModalUI(); sm.HideImmediate(); mod = true; }

                DeckViewerModalUI dm = Object.FindFirstObjectByType<DeckViewerModalUI>();
                if (dm != null) { dm.EnsureModalUI(); dm.HideImmediate(); mod = true; }

                SplineTargetingArrow sa = Object.FindFirstObjectByType<SplineTargetingArrow>();
                if (sa != null) { sa.EnsureArrowUI(); mod = true; }

                UnitStatusUI[] statusUIs = Object.FindObjectsByType<UnitStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (statusUIs != null)
                {
                    for (int u = 0; u < statusUIs.Length; u++)
                    {
                        if (statusUIs[u] != null) { statusUIs[u].EnsureUIHierarchy(); mod = true; }
                    }
                }

                PlayerSkillUI[] skillUIs = Object.FindObjectsByType<PlayerSkillUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (skillUIs != null)
                {
                    for (int s = 0; s < skillUIs.Length; s++)
                    {
                        if (skillUIs[s] != null) { skillUIs[s].EnsureUIHierarchy(); mod = true; }
                    }
                }

                Text[] legacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (legacyTexts != null)
                {
                    for (int t = 0; t < legacyTexts.Length; t++)
                    {
                        if (legacyTexts[t] != null)
                        {
                            UIHelper.GetOrAddTMPText(legacyTexts[t].gameObject);
                            mod = true;
                        }
                    }
                }

                SanitizeSceneNonPersistentAssets(ref mod);
                if (mod)
                {
                    EditorSceneManager.MarkSceneDirty(openedScene);
                    EditorSceneManager.SaveScene(openedScene);
                }
            }

            if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath) &&
                EditorSceneManager.GetActiveScene().path != currentScenePath)
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }
        }
        finally
        {
            isSanitizingOtherScenes = false;
        }
    }

    private static void SanitizeSceneNonPersistentAssets(ref bool sceneModified)
    {
        TMP_FontAsset persistentTmpFont = UIHelper.GetDefaultPersistentTMPFont();
        if (persistentTmpFont == null)
        {
            persistentTmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resource/Fonts/LevelUp-TTF/LevelUp/ttf/LevelUp-Bold SDF.asset");
        }

        // 0) 씬 내장 MonoScript(!u!115) 및 Missing Script 제거 후 영구 .cs 스크립트 에셋으로 재바인딩
        // (파일명과 클래스명이 달랐던 UIHoverScaleEffect, SkillIconPointerReceiver가 씬에 내장되어 level0 손상 크래시를 유발하는 문제 근본 해결)
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allObjects != null)
        {
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject go = allObjects[i];
                if (go == null) continue;
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(go);
                    sceneModified = true;
                }
            }
        }

        MonoBehaviour[] allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allBehaviours != null)
        {
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                MonoBehaviour mb = allBehaviours[i];
                if (mb == null) continue;

                MonoScript ms = MonoScript.FromMonoBehaviour(mb);
                if (ms != null && !EditorUtility.IsPersistent(ms))
                {
                    GameObject go = mb.gameObject;
                    if (mb is UIHoverScaleEffect oldHover)
                    {
                        float mult = oldHover.HoverScaleMultiplier;
                        Object.DestroyImmediate(oldHover);
                        UIHoverScaleEffect newHover = go.AddComponent<UIHoverScaleEffect>();
                        newHover.Configure(mult);
                        EditorUtility.SetDirty(go);
                        sceneModified = true;
                    }
                    else if (mb is SkillIconPointerReceiver oldReceiver)
                    {
                        Object.DestroyImmediate(oldReceiver);
                        go.AddComponent<SkillIconPointerReceiver>();
                        EditorUtility.SetDirty(go);
                        sceneModified = true;
                    }
                }
            }
        }

        // 1) 기본 TMP 폰트의 fallback 테이블에 비영구(In-Memory) 폰트가 들어갔다면 제거
        if (persistentTmpFont != null && persistentTmpFont.fallbackFontAssetTable != null)
        {
            for (int i = persistentTmpFont.fallbackFontAssetTable.Count - 1; i >= 0; i--)
            {
                TMP_FontAsset fb = persistentTmpFont.fallbackFontAssetTable[i];
                if (fb == null || !EditorUtility.IsPersistent(fb))
                {
                    persistentTmpFont.fallbackFontAssetTable.RemoveAt(i);
                    EditorUtility.SetDirty(persistentTmpFont);
                }
            }
        }

        // 2) 씬 내 모든 TextMeshProUGUI가 비영구/기존 LiberationSans 대신 설정된 영구 디스크 에셋(LevelUp-Bold SDF)을 참조하도록 정리
        TextMeshProUGUI[] allTmpTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allTmpTexts != null && persistentTmpFont != null)
        {
            for (int i = 0; i < allTmpTexts.Length; i++)
            {
                TextMeshProUGUI tmp = allTmpTexts[i];
                if (tmp == null) continue;

                bool changed = false;
                if (tmp.font == null || !EditorUtility.IsPersistent(tmp.font) || tmp.font.name.StartsWith("LiberationSans"))
                {
                    tmp.font = persistentTmpFont;
                    changed = true;
                }
                if (tmp.fontSharedMaterial == null || !EditorUtility.IsPersistent(tmp.fontSharedMaterial) || (tmp.font == persistentTmpFont && tmp.fontSharedMaterial != persistentTmpFont.material))
                {
                    tmp.fontSharedMaterial = persistentTmpFont.material;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(tmp);
                    sceneModified = true;
                }
            }
        }

        // 3) 씬 내 모든 Image 컴포넌트 중 디스크 에셋이 아닌 인메모리 프로시저럴 Sprite 참조를 null로 비움 (런타임 Awake에서 자동 생성됨)
        Image[] allImages = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allImages != null)
        {
            for (int i = 0; i < allImages.Length; i++)
            {
                Image img = allImages[i];
                if (img == null) continue;

                if (img.sprite != null && !EditorUtility.IsPersistent(img.sprite))
                {
                    img.sprite = null;
                    EditorUtility.SetDirty(img);
                    sceneModified = true;
                }
            }
        }
    }

    private static List<SelectableUnit> SetupSelectableChildren(
        Transform root,
        bool isPlayer,
        Sprite featherSprite,
        Sprite atkSkillSprite,
        Sprite defSkillSprite,
        Sprite critSkillSprite,
        ref bool modified)
    {
        List<SelectableUnit> list = new List<SelectableUnit>();
        if (root == null) return list;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            if (child.GetComponent<SpriteRenderer>() == null) continue;

            BoxCollider2D boxCol = child.GetComponent<BoxCollider2D>();
            if (boxCol == null)
            {
                boxCol = child.gameObject.AddComponent<BoxCollider2D>();
                modified = true;
            }
            if (boxCol.size.x > 7.2f)
            {
                boxCol.size = new Vector2(6.6f, 6.6f);
                modified = true;
            }

            SelectableUnit unit = child.GetComponent<SelectableUnit>();
            if (unit == null)
            {
                unit = child.gameObject.AddComponent<SelectableUnit>();
                modified = true;
            }

            int idx = list.Count;
            SerializedObject uSo = new SerializedObject(unit);
            if (uSo.FindProperty("isPlayerUnit").boolValue != isPlayer)
            {
                uSo.FindProperty("isPlayerUnit").boolValue = isPlayer;
                modified = true;
            }
            if (uSo.FindProperty("unitIndex").intValue != idx)
            {
                uSo.FindProperty("unitIndex").intValue = idx;
                modified = true;
            }

            // 아직 능력치가 초기화되지 않은 경우 규칙에 맞는 초기 능력치를 세팅합니다.
            if (!uSo.FindProperty("statsInitialized").boolValue)
            {
                float defaultHp = isPlayer ? 250f : (idx == 0 ? 50f : (idx == 1 ? 100f : 500f));
                float defaultMinAtk = isPlayer ? 5f : 10f;
                float defaultMaxAtk = isPlayer ? 5f : 20f;
                int defaultBuff = isPlayer ? 0 : (idx == 0 ? 5 : (idx == 1 ? 2 : 1));
                bool incBuff = !isPlayer;

                uSo.FindProperty("statsInitialized").boolValue = true;
                uSo.FindProperty("maxHp").floatValue = defaultHp;
                uSo.FindProperty("currentHp").floatValue = defaultHp;
                uSo.FindProperty("minAttack").floatValue = defaultMinAtk;
                uSo.FindProperty("maxAttack").floatValue = defaultMaxAtk;
                uSo.FindProperty("buffCount").intValue = defaultBuff;
                uSo.FindProperty("incrementBuffEveryTurn").boolValue = incBuff;
                modified = true;
            }

            uSo.ApplyModifiedPropertiesWithoutUndo();

            if (unit.StatusUI == null)
            {
                unit.EnsureStatusUI(featherSprite, atkSkillSprite, defSkillSprite, critSkillSprite);
                unit.RefreshStatusUI();
                modified = true;
            }

            if (isPlayer)
            {
                if (unit.SkillUI == null)
                {
                    unit.EnsureSkillUI(atkSkillSprite, defSkillSprite, critSkillSprite);
                    unit.RefreshSkillUI();
                    modified = true;
                }
                if (unit.SkillUI != null)
                {
                    SerializedObject uSoSkill = new SerializedObject(unit);
                    if (uSoSkill.FindProperty("skillUI").objectReferenceValue != unit.SkillUI)
                    {
                        uSoSkill.FindProperty("skillUI").objectReferenceValue = unit.SkillUI;
                        uSoSkill.ApplyModifiedPropertiesWithoutUndo();
                        modified = true;
                    }
                }
            }

            list.Add(unit);
        }

        return list;
    }

    private static void SetObjRef(SerializedObject so, string propName, Object val, ref bool modified)
    {
        SerializedProperty p = so.FindProperty(propName);
        if (p != null && p.objectReferenceValue != val)
        {
            p.objectReferenceValue = val;
            modified = true;
        }
    }

    private static void AssignUnitList(SerializedProperty arrayProp, List<SelectableUnit> units, ref bool modified)
    {
        if (arrayProp == null || units == null) return;
        if (arrayProp.arraySize != units.Count)
        {
            arrayProp.arraySize = units.Count;
            modified = true;
        }
        for (int i = 0; i < units.Count; i++)
        {
            if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue != units[i])
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
                modified = true;
            }
        }
    }

    private static Sprite LoadSpriteWithFallback(string primaryPath, string fallbackPath)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(primaryPath);
        if (s == null)
            s = AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
        return s;
    }

    private static Sprite EnsureSpriteImported(string assetPath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(fullPath)) return null;

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (loaded == null)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite sp)
                    return sp;
            }
        }
        return loaded;
    }

    private static void AssignSpriteArray(SerializedProperty arrayProp, Sprite[] sprites, ref bool modified)
    {
        if (arrayProp == null || sprites == null) return;
        if (arrayProp.arraySize != sprites.Length)
        {
            arrayProp.arraySize = sprites.Length;
            modified = true;
        }
        for (int i = 0; i < sprites.Length; i++)
        {
            if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue != sprites[i])
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                modified = true;
            }
        }
    }

    private static void EnsureEffectTextureExists(string assetPath, System.Func<Texture2D> generator)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(fullPath))
        {
            Texture2D tex = generator();
            byte[] pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static void CreateOrderNumberChild(Transform cardRoot, Sprite defaultNumSprite)
    {
        GameObject numObj = new GameObject("OrderNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        numObj.layer = cardRoot.gameObject.layer;
        numObj.transform.SetParent(cardRoot, false);

        RectTransform numRect = numObj.GetComponent<RectTransform>();
        numRect.anchorMin = new Vector2(0.5f, 0.5f);
        numRect.anchorMax = new Vector2(0.5f, 0.5f);
        numRect.pivot = new Vector2(0.5f, 0.5f);
        numRect.anchoredPosition = Vector2.zero;
        numRect.sizeDelta = new Vector2(68f, 68f);

        Image numImg = numObj.GetComponent<Image>();
        numImg.sprite = defaultNumSprite;
        numImg.raycastTarget = false;
        numImg.preserveAspect = true;
        numObj.SetActive(false);

        Card cardComp = cardRoot.GetComponent<Card>();
        if (cardComp != null)
        {
            SerializedObject cardSo = new SerializedObject(cardComp);
            cardSo.FindProperty("orderNumberImage").objectReferenceValue = numImg;
            cardSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureGoldNumberTextureExists(string assetPath, int number)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(fullPath))
        {
            Texture2D tex = GoldNumberTextureGenerator.CreateGoldNumberTexture(number, 128, 128);
            byte[] pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureAttackBoxTextureExists(string assetPath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(fullPath))
        {
            int width = 760;
            int height = 185;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            int margin = 10;
            int chamfer = 18;
            float lineThickness = 3.2f;
            float glowRange = 10f;

            Color deepBlueGlow = new Color(0.02f, 0.28f, 0.85f, 1f);
            Color neonBlueMid = new Color(0.08f, 0.58f, 1.0f, 1f);
            Color neonCyanCore = new Color(0.55f, 0.92f, 1.0f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = Mathf.Min(x - margin, (width - 1 - margin) - x);
                    int dy = Mathf.Min(y - margin, (height - 1 - margin) - y);

                    float dCorner = (dx + dy - chamfer) * 0.7071f;
                    float signedDist = Mathf.Min(Mathf.Min(dx, dy), dCorner);

                    float distOuter = Mathf.Abs(signedDist);
                    float distInner = Mathf.Abs(signedDist - 7f);

                    Color finalColor = Color.clear;

                    if (distOuter <= lineThickness)
                    {
                        float t = 1f - (distOuter / lineThickness);
                        finalColor = Color.Lerp(neonBlueMid, neonCyanCore, t * t);
                        finalColor.a = 1f;
                    }
                    else if (distInner <= 1.4f)
                    {
                        float t = 1f - (distInner / 1.4f);
                        finalColor = Color.Lerp(deepBlueGlow, neonBlueMid, t);
                        finalColor.a = 0.85f * t;
                    }
                    else
                    {
                        float minDist = Mathf.Min(distOuter - lineThickness, distInner - 1.4f);
                        if (minDist < glowRange)
                        {
                            float glow = 1f - (minDist / glowRange);
                            glow = glow * glow;
                            finalColor = deepBlueGlow;
                            finalColor.a = glow * 0.75f;
                        }
                    }

                    if (signedDist > 9f && (dy < 18 || dy > height - 2 * margin - 18))
                    {
                        for (int slot = 1; slot < 6; slot++)
                        {
                            int slotX = margin + Mathf.RoundToInt((width - 2f * margin) * (slot / 6f));
                            if (Mathf.Abs(x - slotX) <= 1 && dy < 16)
                            {
                                Color tickColor = neonBlueMid;
                                tickColor.a = 0.35f;
                                finalColor = Color.Lerp(finalColor, tickColor, tickColor.a);
                            }
                        }
                    }

                    pixels[y * width + x] = finalColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            byte[] pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    // =========================================================================
    // 22. 밝은 사이버 시티 동적 배경 맵 (Quad + URP 셰이더 + 기하학 네온 파티클) 자동 세팅
    // =========================================================================
    [MenuItem("Tools/Zeometry Slayer/사이버 시티 동적 배경 맵 생성 및 적용")]
    public static void SetupCyberCityBackgroundMenu()
    {
        bool mod = false;
        EnsureCyberCityBackgroundInActiveScene(ref mod, forceRegenerateTextures: true);
        if (mod)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
        Debug.Log("[HandSetupHelper] 사이버 시티 동적 배경 맵(Quad + URP 셰이더 + 기하학 파티클)이 현재 씬에 적용되었습니다!");
    }

    public static void EnsureCyberCityBackgroundInActiveScene(ref bool sceneModified, bool forceRegenerateTextures = false)
    {
        Shader highwayShader = Shader.Find("Custom/URP_CyberCityHighway");
        Shader particleShader = Shader.Find("Custom/URP_NeonParticleAdditive");
        if (highwayShader == null || particleShader == null)
            return;

        EnsureFolderExists("Assets/Resource", "Background");
        EnsureFolderExists("Assets", "Materials");

        bool regenerateUnitShapes = forceRegenerateTextures || !SessionState.GetBool("ZeometrySlayer_GeoParticleUnitStyleV4", false);
        if (regenerateUnitShapes)
        {
            SessionState.SetBool("ZeometrySlayer_GeoParticleUnitStyleV4", true);
        }

        // 1) Player/Enemy 스타일 네온 도형 파티클 4종 PNG 에셋 영구 저장 및 로드 (삼각형, 팔각형, 오각형, 사각형)
        string[] shapePaths =
        {
            "Assets/Resource/Background/fx_geo_particle_triangle.png",
            "Assets/Resource/Background/fx_geo_particle_octagon.png",
            "Assets/Resource/Background/fx_geo_particle_pentagon.png",
            "Assets/Resource/Background/fx_geo_particle_square.png"
        };
        Texture2D[] shapeTextures = new Texture2D[4];
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            shapeTextures[i] = EnsureParticleTextureAsset(
                shapePaths[i],
                () => CyberCityBackgroundController.CreateGeometricParticleTexture(idx, 128),
                regenerateUnitShapes
            );
        }

        // 2) 영구 머티리얼(.mat) 5종 생성/로드
        Material highwayMat = EnsurePersistentMaterial("Assets/Materials/Mat_CyberCityHighway.mat", highwayShader, null, false);
        if (highwayMat != null && highwayMat.HasProperty("_UseCustomTime") && highwayMat.GetFloat("_UseCustomTime") < 0.5f)
        {
            highwayMat.SetFloat("_UseCustomTime", 1f);
            EditorUtility.SetDirty(highwayMat);
            AssetDatabase.SaveAssets();
        }
        string[] particleMatPaths =
        {
            "Assets/Materials/Mat_GeoParticle_Triangle.mat",
            "Assets/Materials/Mat_GeoParticle_Octagon.mat",
            "Assets/Materials/Mat_GeoParticle_Pentagon.mat",
            "Assets/Materials/Mat_GeoParticle_Square.mat"
        };
        Material[] particleMats = new Material[4];
        for (int i = 0; i < 4; i++)
        {
            particleMats[i] = EnsurePersistentMaterial(particleMatPaths[i], particleShader, shapeTextures[i], regenerateUnitShapes);
        }

        // 3) CyberCityBackground 루트 오브젝트 생성/확인
        GameObject rootObj = GameObject.Find("CyberCityBackground");
        if (rootObj == null)
        {
            rootObj = new GameObject("CyberCityBackground");
            sceneModified = true;
        }
        rootObj.transform.position = new Vector3(0f, 0f, 4f);
        rootObj.transform.rotation = Quaternion.identity;
        rootObj.transform.localScale = Vector3.one;

        CyberCityBackgroundController controller = rootObj.GetComponent<CyberCityBackgroundController>();
        if (controller == null)
        {
            controller = rootObj.AddComponent<CyberCityBackgroundController>();
            sceneModified = true;
        }

        // 4) 자식 Quad (URP 셰이더 배경) 생성/설정 (MeshCollider 제거)
        Transform quadTr = rootObj.transform.Find("CyberCityQuad");
        GameObject quadObj;
        if (quadTr == null)
        {
            quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadObj.name = "CyberCityQuad";
            quadObj.transform.SetParent(rootObj.transform, false);
            Collider col = quadObj.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
            sceneModified = true;
        }
        else
        {
            quadObj = quadTr.gameObject;
            Collider col = quadObj.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
                sceneModified = true;
            }
        }

        quadObj.transform.localPosition = Vector3.zero;
        quadObj.transform.localRotation = Quaternion.identity;
        quadObj.transform.localScale = new Vector3(20f, 10.6f, 1f);

        MeshRenderer quadRenderer = quadObj.GetComponent<MeshRenderer>();
        if (quadRenderer != null)
        {
            if (quadRenderer.sharedMaterial != highwayMat)
            {
                quadRenderer.sharedMaterial = highwayMat;
                sceneModified = true;
            }
            if (quadRenderer.sortingOrder != -20)
            {
                quadRenderer.sortingOrder = -20;
                sceneModified = true;
            }
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
        }

        // 5) Player/Enemy 스타일 네온 도형 파티클 4종 (삼각형, 팔각형, 오각형, 사각형) 생성/설정 (구형 다이아몬드 제거)
        Transform oldDiamond = rootObj.transform.Find("GeoParticles_Diamond");
        if (oldDiamond != null)
        {
            Object.DestroyImmediate(oldDiamond.gameObject);
            sceneModified = true;
        }

        string[] particleNames = { "GeoParticles_Triangle", "GeoParticles_Octagon", "GeoParticles_Pentagon", "GeoParticles_Square" };
        ParticleSystem[] particleSystems = new ParticleSystem[4];
        for (int i = 0; i < 4; i++)
        {
            particleSystems[i] = EnsureGeometricParticleSystem(rootObj.transform, particleNames[i], particleMats[i], ref sceneModified);
        }

        // 6) Controller 직렬화 필드 연결 및 연한 네온 파티클 색상 반영
        SerializedObject ctrlSo = new SerializedObject(controller);
        SetObjRef(ctrlSo, "backgroundQuadRenderer", quadRenderer, ref sceneModified);
        SerializedProperty pColProp = ctrlSo.FindProperty("playerParticleColor");
        if (pColProp != null)
        {
            Color targetPCol = new Color(0.58f, 0.97f, 1.0f, 0.34f);
            if (pColProp.colorValue != targetPCol)
            {
                pColProp.colorValue = targetPCol;
                sceneModified = true;
            }
        }
        SerializedProperty eColProp = ctrlSo.FindProperty("enemyParticleColor");
        if (eColProp != null)
        {
            Color targetECol = new Color(1.0f, 0.56f, 0.68f, 0.36f);
            if (eColProp.colorValue != targetECol)
            {
                eColProp.colorValue = targetECol;
                sceneModified = true;
            }
        }
        SerializedProperty psArrayProp = ctrlSo.FindProperty("geometricParticleSystems");
        if (psArrayProp != null)
        {
            if (psArrayProp.arraySize != particleSystems.Length)
            {
                psArrayProp.arraySize = particleSystems.Length;
                sceneModified = true;
            }
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (psArrayProp.GetArrayElementAtIndex(i).objectReferenceValue != particleSystems[i])
                {
                    psArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = particleSystems[i];
                    sceneModified = true;
                }
            }
        }
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // 7) 기존 정적 BackGround 스프라이트는 비활성화 (동적 배경과 겹치지 않도록 처리)
        GameObject oldBg = GameObject.Find("BackGround");
        if (oldBg != null && oldBg.activeSelf)
        {
            oldBg.SetActive(false);
            sceneModified = true;
        }
    }

    private static void EnsureFolderExists(string parentFolder, string newFolderName)
    {
        string combined = $"{parentFolder}/{newFolderName}";
        if (!AssetDatabase.IsValidFolder(combined))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }
    }

    private static Texture2D EnsureParticleTextureAsset(string assetPath, System.Func<Texture2D> generator, bool forceOverwrite = false)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (forceOverwrite || !File.Exists(fullPath))
        {
            Texture2D tex = generator();
            byte[] pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static Material EnsurePersistentMaterial(string matPath, Shader shader, Texture2D mainTex, bool resetGlowIntensity = false)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            if (mainTex != null)
            {
                mat.SetTexture("_MainTex", mainTex);
                mat.SetFloat("_GlowIntensity", 0.85f);
            }
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }
        else
        {
            bool dirty = false;
            if (mat.shader != shader)
            {
                mat.shader = shader;
                dirty = true;
            }
            if (mainTex != null && mat.GetTexture("_MainTex") != mainTex)
            {
                mat.SetTexture("_MainTex", mainTex);
                dirty = true;
            }
            if (mainTex != null && resetGlowIntensity && mat.HasProperty("_GlowIntensity"))
            {
                mat.SetFloat("_GlowIntensity", 0.85f);
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }
        }
        return mat;
    }

    private static ParticleSystem EnsureGeometricParticleSystem(
        Transform parent,
        string childName,
        Material particleMat,
        ref bool sceneModified)
    {
        Transform tr = parent.Find(childName);
        GameObject psObj;
        bool created = false;
        if (tr == null)
        {
            psObj = new GameObject(childName, typeof(ParticleSystem));
            psObj.transform.SetParent(parent, false);
            created = true;
            sceneModified = true;
        }
        else
        {
            psObj = tr.gameObject;
        }

        psObj.transform.localPosition = new Vector3(0f, -5.2f, -1f);
        psObj.transform.localRotation = Quaternion.identity;
        psObj.transform.localScale = Vector3.one;

        ParticleSystem ps = psObj.GetComponent<ParticleSystem>();
        if (created)
        {
            var main = ps.main;
            main.duration = 8f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 8.5f);
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(19f, 1.2f, 0.1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.75f, 1.40f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-60f * Mathf.Deg2Rad, 60f * Mathf.Deg2Rad);
        }

        // 기존 씬 오브젝트에도 Player/Enemy 스타일 크기·밀도·연한 알파값을 일관되게 적용
        var mainAlways = ps.main;
        mainAlways.startColor = new Color(0.58f, 0.97f, 1.0f, 0.34f);
        mainAlways.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.54f);
        mainAlways.maxParticles = 18;

        var emissionAlways = ps.emission;
        emissionAlways.enabled = true;
        emissionAlways.rateOverTime = 1.7f;

        var colOverLife = ps.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.65f, 0.15f),
                new GradientAlphaKey(0.55f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        ParticleSystemRenderer renderer = psObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            if (renderer.sharedMaterial != particleMat)
            {
                renderer.sharedMaterial = particleMat;
                sceneModified = true;
            }
            if (renderer.sortingOrder != -10)
            {
                renderer.sortingOrder = -10;
                sceneModified = true;
            }
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        return ps;
    }
}
