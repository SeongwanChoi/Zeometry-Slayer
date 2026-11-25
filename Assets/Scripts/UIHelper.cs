using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 프로젝트 전반에서 공통으로 사용되는 UI 생성(TextMeshProUGUI 기반), LevelUp-Bold SDF 폰트 로드, 스프라이트 로드,
/// 월드-캔버스 직교 좌표 변환 및 수치 포맷팅 유틸리티 클래스입니다.
/// </summary>
public static class UIHelper
{
    private const string DefaultLevelUpBoldFontPath = "Assets/Resource/Fonts/LevelUp-TTF/LevelUp/ttf/LevelUp-Bold SDF.asset";

    private static Sprite cachedSolidWhiteSprite;

    /// <summary>
    /// TMP Settings에 설정된 기본 영구 TMP_FontAsset(LevelUp-Bold SDF)을 반환합니다.
    /// 미설정 시 에디터에서는 LevelUp-Bold SDF 에셋을 직접 로드하고, 런타임 폴백으로 Resources 기본 폰트를 반환합니다.
    /// </summary>
    public static TMP_FontAsset GetDefaultPersistentTMPFont()
    {
        TMP_FontAsset defaultTmpFont = TMP_Settings.defaultFontAsset;
#if UNITY_EDITOR
        if (defaultTmpFont == null)
        {
            defaultTmpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultLevelUpBoldFontPath);
        }
#endif
        if (defaultTmpFont == null)
        {
            defaultTmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
        return defaultTmpFont;
    }

    /// <summary>
    /// 프로젝트 기본 TMP_FontAsset(LevelUp-Bold SDF)을 반환합니다.
    /// </summary>
    public static TMP_FontAsset GetOrCreateTMPFont()
    {
        return GetDefaultPersistentTMPFont();
    }

    public static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    public static TextAlignmentOptions ConvertTextAnchor(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:    return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:  return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:   return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:   return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Midline;
            case TextAnchor.MiddleRight:  return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft:    return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:  return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:   return TextAlignmentOptions.BottomRight;
            default:                      return TextAlignmentOptions.Midline;
        }
    }

    /// <summary>
    /// 대상 GameObject에 남아있는 레거시 UI.Text / Outline / Shadow를 제거하고 TextMeshProUGUI 컴포넌트를 반환합니다.
    /// </summary>
    public static TextMeshProUGUI GetOrAddTMPText(GameObject obj)
    {
        if (obj == null) return null;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            if (tmp.font == null || tmp.font.name.StartsWith("LiberationSans"))
                tmp.font = GetOrCreateTMPFont();
            return tmp;
        }

        string preservedText = string.Empty;
        float preservedFontSize = 20f;
        FontStyles preservedStyle = FontStyles.Bold;
        Color preservedColor = Color.white;
        TextAlignmentOptions preservedAlign = TextAlignmentOptions.Midline;
        bool preservedRaycast = false;
        bool hadLegacyText = false;

        Shadow legacyShadow = obj.GetComponent<Shadow>();
        if (legacyShadow != null)
            Object.DestroyImmediate(legacyShadow);

        Outline legacyOutline = obj.GetComponent<Outline>();
        if (legacyOutline != null)
            Object.DestroyImmediate(legacyOutline);

        Text legacyText = obj.GetComponent<Text>();
        if (legacyText != null)
        {
            hadLegacyText = true;
            preservedText = legacyText.text;
            preservedFontSize = legacyText.fontSize;
            preservedStyle = ConvertFontStyle(legacyText.fontStyle);
            preservedColor = legacyText.color;
            preservedAlign = ConvertTextAnchor(legacyText.alignment);
            preservedRaycast = legacyText.raycastTarget;
            Object.DestroyImmediate(legacyText);
        }

        tmp = obj.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = obj.AddComponent<TextMeshProUGUI>();

        tmp.font = GetOrCreateTMPFont();
        if (hadLegacyText)
        {
            tmp.text = preservedText;
            tmp.fontSize = preservedFontSize;
            tmp.fontStyle = preservedStyle;
            tmp.color = preservedColor;
            tmp.alignment = preservedAlign;
            tmp.raycastTarget = preservedRaycast;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        return tmp;
    }

    public static TextMeshProUGUI FindOrConvertTMPText(Transform parent, string childName)
    {
        if (parent == null) return null;
        Transform tr = parent.Find(childName);
        if (tr == null) return null;
        return GetOrAddTMPText(tr.gameObject);
    }

    /// <summary>
    /// 단색 배경이나 체력바 게이지 등에 공용으로 사용할 4x4 흰색 스프라이트를 Assets/Resource/UI/ui_solid_white.png에 저장하고 반환합니다.
    /// </summary>
    public static Sprite GetOrCreateSolidWhiteSprite()
    {
        if (cachedSolidWhiteSprite != null)
            return cachedSolidWhiteSprite;

        cachedSolidWhiteSprite = GetOrSaveProceduralSprite(
            "Assets/Resource/UI/ui_solid_white.png",
            "ui_solid_white",
            Vector4.zero,
            () =>
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color[] cols = new Color[16];
                for (int i = 0; i < 16; i++)
                    cols[i] = Color.white;

                tex.SetPixels(cols);
                tex.Apply();
                return tex;
            }
        );

        return cachedSolidWhiteSprite;
    }

    /// <summary>
    /// 코드(C#)로 생성한 프로시저럴 텍스처를 Assets/Resource/UI 폴더 내에 실제 .png 파일로 영구 저장 및 Sprite로 임포트하여 반환합니다.
    /// (Edit Mode에서도 흰색 네모가 아닌 실제 UI 이미지가 그대로 보이고, 씬 파일에 인메모리 텍스처가 내장되지 않도록 보장합니다.)
    /// </summary>
    public static Sprite GetOrSaveProceduralSprite(
        string relativeAssetPath,
        string spriteName,
        Vector4 border,
        System.Func<Texture2D> textureGenerator,
        Vector2? customPivot = null)
    {
        Vector2 pivot = customPivot ?? new Vector2(0.5f, 0.5f);

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(relativeAssetPath))
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativeAssetPath);
            string dirPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (!File.Exists(fullPath) && textureGenerator != null)
            {
                Texture2D generatedTex = textureGenerator();
                if (generatedTex != null)
                {
                    byte[] pngBytes = generatedTex.EncodeToPNG();
                    Object.DestroyImmediate(generatedTex);
                    File.WriteAllBytes(fullPath, pngBytes);
                    UnityEditor.AssetDatabase.ImportAsset(relativeAssetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                }
            }

            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(relativeAssetPath) as UnityEditor.TextureImporter;
            if (importer == null && File.Exists(fullPath))
            {
                UnityEditor.AssetDatabase.ImportAsset(relativeAssetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                importer = UnityEditor.AssetImporter.GetAtPath(relativeAssetPath) as UnityEditor.TextureImporter;
            }

            if (importer != null)
            {
                bool needsReimport = false;
                if (importer.textureType != UnityEditor.TextureImporterType.Sprite)
                {
                    importer.textureType = UnityEditor.TextureImporterType.Sprite;
                    needsReimport = true;
                }
                if (importer.spriteImportMode != UnityEditor.SpriteImportMode.Single)
                {
                    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                    needsReimport = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    needsReimport = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    needsReimport = true;
                }
                if ((importer.spriteBorder - border).sqrMagnitude > 0.01f)
                {
                    importer.spriteBorder = border;
                    needsReimport = true;
                }

                if (needsReimport)
                {
                    importer.SaveAndReimport();
                }
            }

            Sprite persistentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativeAssetPath);
            if (persistentSprite == null)
            {
                Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativeAssetPath);
                if (subAssets != null)
                {
                    for (int i = 0; i < subAssets.Length; i++)
                    {
                        if (subAssets[i] is Sprite subSp)
                        {
                            persistentSprite = subSp;
                            break;
                        }
                    }
                }
            }

            if (persistentSprite != null)
                return persistentSprite;
        }
#endif

        // 런타임 폴백 (씬 직렬화 참조가 누락된 경우 디스크 또는 생성기로부터 생성)
        if (!string.IsNullOrEmpty(relativeAssetPath))
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativeAssetPath);
            if (File.Exists(fullPath))
            {
                byte[] fileBytes = File.ReadAllBytes(fullPath);
                Texture2D diskTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (diskTex.LoadImage(fileBytes))
                {
                    diskTex.wrapMode = TextureWrapMode.Clamp;
                    diskTex.filterMode = FilterMode.Bilinear;
                    Sprite diskSp = Sprite.Create(diskTex, new Rect(0, 0, diskTex.width, diskTex.height), pivot, 100f, 0, SpriteMeshType.FullRect, border);
                    diskSp.name = !string.IsNullOrEmpty(spriteName) ? spriteName : Path.GetFileNameWithoutExtension(relativeAssetPath);
                    return diskSp;
                }
            }
        }

        if (textureGenerator != null)
        {
            Texture2D runtimeTex = textureGenerator();
            if (runtimeTex != null)
            {
                Sprite runtimeSp = Sprite.Create(runtimeTex, new Rect(0, 0, runtimeTex.width, runtimeTex.height), pivot, 100f, 0, SpriteMeshType.FullRect, border);
                runtimeSp.name = !string.IsNullOrEmpty(spriteName) ? spriteName : "procedural_sprite";
                return runtimeSp;
            }
        }

        return null;
    }

    /// <summary>
    /// 에디터의 AssetDatabase 또는 런타임 디스크 경로로부터 스프라이트를 로드합니다.
    /// </summary>
    public static Sprite LoadSpriteFromAssetOrDisk(string relativeAssetPath, string spriteName = null, Sprite fallbackSprite = null)
    {
        if (string.IsNullOrEmpty(relativeAssetPath))
            return fallbackSprite;

#if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativeAssetPath);
        if (editorSprite != null)
            return editorSprite;

        Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativeAssetPath);
        if (subAssets != null)
        {
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite subSp)
                    return subSp;
            }
        }
#endif
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativeAssetPath);
        if (File.Exists(fullPath))
        {
            byte[] fileBytes = File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(fileBytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sp.name = !string.IsNullOrEmpty(spriteName) ? spriteName : Path.GetFileNameWithoutExtension(relativeAssetPath);
                return sp;
            }
        }

        return fallbackSprite;
    }

    /// <summary>
    /// 실수 수치를 정수일 때는 소수점 없이, 소수부가 있을 때는 소수 첫째 자리("0.#")까지 깔끔하게 포맷팅합니다.
    /// </summary>
    public static string FormatCompactNumber(float value)
    {
        return Mathf.Approximately(value % 1f, 0f)
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#");
    }

    /// <summary>
    /// 월드 좌표를 기준으로 대상 UI RectTransform의 위치를 에디터/플레이 모드 모두 동일하게 직교 투영 동기화합니다.
    /// </summary>
    public static void SyncRectTransformToWorldPosition(
        RectTransform targetRect,
        Canvas parentCanvas,
        RectTransform canvasRect,
        Vector3 worldPos)
    {
        if (targetRect == null || parentCanvas == null || canvasRect == null)
            return;

        Camera worldCam = (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay && parentCanvas.worldCamera != null)
            ? parentCanvas.worldCamera
            : Camera.main;

        if (worldCam == null)
            return;

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

            float localX = (worldPos.x - camPos.x) * pixelsPerWorldUnit;
            float localY = (worldPos.y - camPos.y) * pixelsPerWorldUnit;

            targetRect.anchoredPosition = new Vector2(localX, localY);
            targetRect.localPosition = new Vector3(localX, localY, 0f);
            return;
        }

        Camera uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCam;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCam, worldPos);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCam, out Vector2 localPt))
        {
            targetRect.anchoredPosition = localPt;
            targetRect.localPosition = new Vector3(localPt.x, localPt.y, 0f);
        }
    }

    /// <summary>
    /// RectTransform을 중앙 기준(Anchor/Pivot 0.5, 0.5)으로 정렬하고 크기 및 좌표를 설정합니다.
    /// </summary>
    public static void ConfigureCenteredRect(RectTransform rt, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;
        rt.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);
    }

    /// <summary>
    /// TextMeshProUGUI 텍스트 오브젝트를 생성하고 초기 스타일을 설정합니다.
    /// </summary>
    public static TextMeshProUGUI CreateOutlinedText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Color textColor,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        TextAnchor alignment,
        Color outlineColor,
        Vector2 outlineDistance,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null,
        Vector2? pivot = null)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        int uiLayer = LayerMask.NameToLayer("UI");
        obj.layer = uiLayer >= 0 ? uiLayer : (parent != null ? parent.gameObject.layer : 0);
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        rt.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;

        if (!anchorMin.HasValue && !anchorMax.HasValue && !pivot.HasValue)
        {
            rt.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);
        }

        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        txt.font = GetOrCreateTMPFont();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = ConvertFontStyle(fontStyle);
        txt.color = textColor;
        txt.alignment = ConvertTextAnchor(alignment);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.raycastTarget = false;

        return txt;
    }

    /// <summary>
    /// 클릭 가능한 UI 오브젝트에 IPointerEnterHandler / IPointerExitHandler 기반 호버 확대 컴포넌트를 부착합니다.
    /// </summary>
    public static UIHoverScaleEffect EnsureHoverScale(GameObject targetObj, float hoverScale = 1.12f)
    {
        if (targetObj == null) return null;

        UIHoverScaleEffect effect = targetObj.GetComponent<UIHoverScaleEffect>();
        if (effect == null)
            effect = targetObj.AddComponent<UIHoverScaleEffect>();

        effect.Configure(hoverScale);
        return effect;
    }
}
