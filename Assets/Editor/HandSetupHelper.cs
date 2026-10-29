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

    [MenuItem("Tools/Zeometry Slayer/플레이어 손패 & Attack Box 자동 세팅")]
    public static void TrySetupHandAndAttackBox()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool sceneModified = false;

        // 1. 스프라이트 로드 (card_neon_blue_blank)
        Sprite blueCardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resource/card_neon_blue_blank.png");
        if (blueCardSprite == null) return;

        // 2. Attack Box 전용 네온 프레임 이미지(짙은 푸른색 네온 테두리 + 안쪽은 비워둔 형태) 생성 및 로드
        string attackBoxPath = "Assets/Resource/attack_box_neon_blue.png";
        EnsureAttackBoxTextureExists(attackBoxPath);
        Sprite attackBoxSprite = AssetDatabase.LoadAssetAtPath<Sprite>(attackBoxPath);

        // 3. 순서 표시용 금색 숫자 이미지(1~6) PNG 생성 및 로드
        Sprite[] goldNumberSprites = new Sprite[6];
        for (int i = 0; i < 6; i++)
        {
            string numAssetPath = $"Assets/Resource/order_num_gold_{i + 1}.png";
            EnsureGoldNumberTextureExists(numAssetPath, i + 1);
            goldNumberSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(numAssetPath);
        }

        // 4. 프리팹 폴더 및 Card_Neon_Blue 프리팹 생성/업데이트 (OrderNumber 자식 이미지 포함)
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

        // 5. 현재 열린 씬의 Canvas 확인
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 6. PlayerHand 오브젝트 확인 및 세팅 (6장 모두 넣을 수 있도록 기본 장수 6장으로 보장)
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

            SerializedObject so = new SerializedObject(existingHand);
            so.FindProperty("handContainer").objectReferenceValue = handRect;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefabComp;
            so.FindProperty("defaultCardSprite").objectReferenceValue = blueCardSprite;
            so.FindProperty("targetHandCount").intValue = 6; // 6장을 모두 AttackBox에 넣을 수 있도록 기본 6장 세팅
            so.ApplyModifiedPropertiesWithoutUndo();

            sceneModified = true;
        }
        else
        {
            SerializedObject so = new SerializedObject(existingHand);
            SerializedProperty countProp = so.FindProperty("targetHandCount");
            if (countProp != null && countProp.intValue == 5)
            {
                countProp.intValue = 6;
                so.ApplyModifiedPropertiesWithoutUndo();
                sceneModified = true;
            }
        }

        // 7. AttackBox 오브젝트 세팅 및 금색 숫자 스프라이트 배열(1~6) 자동 연결
        AttackBoxManager existingAttackBox = Object.FindFirstObjectByType<AttackBoxManager>();
        if (existingAttackBox == null)
        {
            GameObject boxObj = new GameObject("AttackBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AttackBoxManager));
            boxObj.layer = LayerMask.NameToLayer("UI");
            boxObj.transform.SetParent(canvas.transform, false);
            boxObj.transform.SetAsFirstSibling(); // 손패 카드를 드래그할 때 손패보다 뒤에 그려지도록 배치

            RectTransform boxRect = boxObj.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = new Vector2(0f, 220f);
            boxRect.sizeDelta = new Vector2(760f, 185f);

            Image boxImg = boxObj.GetComponent<Image>();
            boxImg.sprite = attackBoxSprite;
            boxImg.type = Image.Type.Simple;
            boxImg.raycastTarget = true;

            existingAttackBox = boxObj.GetComponent<AttackBoxManager>();
            sceneModified = true;
        }

        if (existingAttackBox != null)
        {
            SerializedObject boxSo = new SerializedObject(existingAttackBox);
            boxSo.FindProperty("boxRectTransform").objectReferenceValue = existingAttackBox.GetComponent<RectTransform>();
            boxSo.FindProperty("boxFrameImage").objectReferenceValue = existingAttackBox.GetComponent<Image>();
            boxSo.FindProperty("requiredCardCount").intValue = 6;

            SerializedProperty spritesProp = boxSo.FindProperty("orderNumberSprites");
            if (spritesProp != null)
            {
                spritesProp.arraySize = 6;
                for (int i = 0; i < 6; i++)
                {
                    if (spritesProp.GetArrayElementAtIndex(i).objectReferenceValue != goldNumberSprites[i])
                    {
                        spritesProp.GetArrayElementAtIndex(i).objectReferenceValue = goldNumberSprites[i];
                        sceneModified = true;
                    }
                }
            }

            boxSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("[HandSetupHelper] AttackBox 및 금색 순서 숫자 이미지(1~6) 세팅이 완료되었습니다!");
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

    /// <summary>
    /// 금색 순서 숫자 이미지(1~6) PNG 파일을 생성하고 Sprite로 임포트합니다.
    /// </summary>
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

    /// <summary>
    /// 맵 분위기에 맞는 짙은 푸른색 네온 테두리 + 안쪽이 비워진(투명) Attack Box PNG 이미지를 생성합니다.
    /// </summary>
    private static void EnsureAttackBoxTextureExists(string assetPath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(fullPath))
        {
            int width = 760;
            int height = 185;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            int margin = 10;          // 바깥 네온 광원 여백
            int chamfer = 18;         // 모서리 대각선 깎임(기하학적 네온 스타일)
            float lineThickness = 3.2f;
            float glowRange = 10f;

            // 짙은 푸른색 네온 컬러 & 하이라이트 코어
            Color deepBlueGlow = new Color(0.02f, 0.28f, 0.85f, 1f);
            Color neonBlueMid = new Color(0.08f, 0.58f, 1.0f, 1f);
            Color neonCyanCore = new Color(0.55f, 0.92f, 1.0f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = Mathf.Min(x - margin, (width - 1 - margin) - x);
                    int dy = Mathf.Min(y - margin, (height - 1 - margin) - y);

                    // 대각선 챔퍼(모서리 깎기) 거리 계산
                    float dCorner = (dx + dy - chamfer) * 0.7071f;
                    float signedDist = Mathf.Min(Mathf.Min(dx, dy), dCorner);

                    // 두 줄 네온 라인 효과 (바깥 메인 테두리: signedDist == 0, 안쪽 보조 얇은 테두리: signedDist == 7)
                    float distOuter = Mathf.Abs(signedDist);
                    float distInner = Mathf.Abs(signedDist - 7f);

                    Color finalColor = Color.clear; // 안쪽은 비워둔 투명 형태

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

                    // 안쪽 영역 중 아주 미세한 슬롯 가이드 눈금 (6칸 구분선 힌트 - 상하단 끝부분에만 살짝 표시하고 중앙은 완전히 비움)
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
