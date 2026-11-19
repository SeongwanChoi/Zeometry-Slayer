using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로젝트 전반에서 공통으로 사용되는 UI 생성, 한글 폰트 로드, 스프라이트 로드,
/// 월드-캔버스 직교 좌표 변환 및 수치 포맷팅 유틸리티 클래스입니다.
/// </summary>
public static class UIHelper
{
    private static Font cachedKoreanFont;
    private static Sprite cachedSolidWhiteSprite;

    private static readonly string[] KoreanFontCandidates =
    {
        "Malgun Gothic",
        "맑은 고딕",
        "NanumGothic",
        "Gulim",
        "Arial Bold",
        "Arial"
    };

    /// <summary>
    /// Windows/OS 한글 폰트(맑은 고딕 등)를 우선 로드하고, 실패 시 내장 폰트(LegacyRuntime.ttf)를 반환합니다.
    /// </summary>
    public static Font GetOrCreateFont()
    {
        if (cachedKoreanFont != null)
            return cachedKoreanFont;

        cachedKoreanFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 28);

        if (cachedKoreanFont == null)
            cachedKoreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (cachedKoreanFont == null)
            cachedKoreanFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return cachedKoreanFont;
    }

    /// <summary>
    /// 단색 배경이나 체력바 게이지 등에 공용으로 사용할 4x4 흰색 스프라이트를 생성/반환합니다.
    /// </summary>
    public static Sprite GetOrCreateSolidWhiteSprite()
    {
        if (cachedSolidWhiteSprite != null)
            return cachedSolidWhiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16];
        for (int i = 0; i < 16; i++)
            cols[i] = Color.white;

        tex.SetPixels(cols);
        tex.Apply();

        cachedSolidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        cachedSolidWhiteSprite.name = "ui_solid_white";
        return cachedSolidWhiteSprite;
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
    /// 외곽선(Outline)이 포함된 UI Text 오브젝트를 생성하고 초기 스타일을 설정합니다.
    /// </summary>
    public static Text CreateOutlinedText(
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
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
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

        Text txt = obj.GetComponent<Text>();
        txt.font = GetOrCreateFont();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.color = textColor;
        txt.alignment = alignment;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        Outline outline = obj.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = outlineDistance;

        return txt;
    }
}
