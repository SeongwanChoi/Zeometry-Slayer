using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("[HandSetupHelper] 깃털 제곱 버프 UI / 좌하단 코스트 UI / 우하단 스타 UI / Buster·Arts·Quick 세팅이 완료되었습니다!");
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

            unit.EnsureStatusUI(featherSprite, atkSkillSprite, defSkillSprite, critSkillSprite);
            unit.RefreshStatusUI();
            if (unit.StatusUI != null)
            {
                Vector3 prevPos = unit.StatusUI.transform.localPosition;
                unit.StatusUI.UpdatePosition();
                if ((unit.StatusUI.transform.localPosition - prevPos).sqrMagnitude > 0.01f)
                {
                    EditorUtility.SetDirty(unit.StatusUI.transform);
                    modified = true;
                }
            }

            if (isPlayer)
            {
                unit.EnsureSkillUI(atkSkillSprite, defSkillSprite, critSkillSprite);
                unit.RefreshSkillUI();
                if (unit.SkillUI != null)
                {
                    SerializedObject uSoSkill = new SerializedObject(unit);
                    if (uSoSkill.FindProperty("skillUI").objectReferenceValue != unit.SkillUI)
                    {
                        uSoSkill.FindProperty("skillUI").objectReferenceValue = unit.SkillUI;
                        uSoSkill.ApplyModifiedPropertiesWithoutUndo();
                        modified = true;
                    }

                    Vector3 prevSkillPos = unit.SkillUI.transform.localPosition;
                    unit.SkillUI.UpdatePosition();
                    if ((unit.SkillUI.transform.localPosition - prevSkillPos).sqrMagnitude > 0.01f)
                    {
                        EditorUtility.SetDirty(unit.SkillUI.transform);
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
}
