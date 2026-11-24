using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// 슬레이 더 스파이어 스타일의 Spline 기반 타겟팅 곡선 화살표 및 적 타겟 표시 UI (PDF 5페이지 명세):
/// - Unity 공식 Splines 패키지(UnityEngine.Splines)의 Spline / BezierKnot / SplineUtility.Evaluate를 사용하여
///   화면 하단 중앙에 위치한 사용 대기 카드에서 마우스 포인터까지 부드러운 곡선 화살표를 실시간으로 그립니다.
/// - 마우스 포인터가 살아있는 Enemy 오브젝트 위에 Hover되면 해당 적을 대상으로 지정하고,
///   적 주변 네 모서리에 슬더스 스타일의 황금 코너 브래킷(┏ ┓ ┗ ┛) 타겟 UI를 표기합니다.
/// </summary>
public class SplineTargetingArrow : Singleton<SplineTargetingArrow>
{
    [Header("스플라인 화살표 설정")]
    [SerializeField] private int nodeCount = 16;
    [SerializeField] private float minNodeScale = 0.42f;
    [SerializeField] private float maxNodeScale = 0.96f;
    [SerializeField] private float curveArcHeight = 240f;

    [Header("색상 설정")]
    [SerializeField] private Color normalArrowColor = new Color(0.88f, 0.14f, 0.18f, 0.96f);
    [SerializeField] private Color lockedEnemyArrowColor = new Color(1.0f, 0.26f, 0.22f, 1.0f);

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private RectTransform arrowRootRect;

    private Spline spline;
    private readonly List<RectTransform> nodeRects = new List<RectTransform>();
    private readonly List<Image> nodeImages = new List<Image>();
    private RectTransform headRect;
    private Image headImage;

    // 적 타겟팅 4방향 황금 코너 브래킷 UI
    private RectTransform targetBracketRoot;
    private readonly RectTransform[] cornerBrackets = new RectTransform[4]; // 0:TL, 1:TR, 2:BL, 3:BR
    private SelectableUnit currentHoveredEnemy;
    private bool isArrowActive = false;

    private static Sprite cachedNodeSprite;
    private static Sprite cachedHeadSprite;
    private static Sprite cachedCornerBracketSprite;

    public SelectableUnit CurrentHoveredEnemy => currentHoveredEnemy;
    public bool IsArrowActive => isArrowActive;

    protected override void Awake()
    {
        base.Awake();
        InitializeSplineAndUI();
        HideArrow();
    }

    private void InitializeSplineAndUI()
    {
        if (spline == null)
        {
            spline = new Spline();
            spline.Add(new BezierKnot());
            spline.Add(new BezierKnot());
        }

        if (parentCanvas == null)
            parentCanvas = Object.FindFirstObjectByType<Canvas>();
        if (parentCanvas == null) return;

        canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (arrowRootRect == null)
        {
            Transform existing = parentCanvas.transform.Find("SplineTargetingArrowUI");
            if (existing != null)
            {
                arrowRootRect = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject rootObj = new GameObject("SplineTargetingArrowUI", typeof(RectTransform));
                rootObj.layer = LayerMask.NameToLayer("UI");
                rootObj.transform.SetParent(parentCanvas.transform, false);

                arrowRootRect = rootObj.GetComponent<RectTransform>();
                arrowRootRect.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRootRect.anchorMax = new Vector2(0.5f, 0.5f);
                arrowRootRect.pivot = new Vector2(0.5f, 0.5f);
                arrowRootRect.anchoredPosition = Vector2.zero;
                arrowRootRect.sizeDelta = Vector2.zero;
                arrowRootRect.localScale = Vector3.one;
            }
        }

        // 1) 적 타겟 표시용 4방향 코너 브래킷 생성
        if (targetBracketRoot == null)
        {
            Transform tbTr = arrowRootRect.Find("EnemyTargetBrackets");
            if (tbTr == null)
            {
                GameObject tbObj = new GameObject("EnemyTargetBrackets", typeof(RectTransform));
                tbObj.layer = LayerMask.NameToLayer("UI");
                tbObj.transform.SetParent(arrowRootRect, false);
                targetBracketRoot = tbObj.GetComponent<RectTransform>();
                UIHelper.ConfigureCenteredRect(targetBracketRoot, Vector2.zero, new Vector2(210f, 210f));
            }
            else
            {
                targetBracketRoot = tbTr.GetComponent<RectTransform>();
            }

            Sprite cornerSp = GetOrCreateCornerBracketSprite();
            Vector2[] cornerAnchors =
            {
                new Vector2(0f, 1f), // TL
                new Vector2(1f, 1f), // TR
                new Vector2(0f, 0f), // BL
                new Vector2(1f, 0f)  // BR
            };
            float[] cornerRotations = { 0f, -90f, 90f, 180f };

            for (int i = 0; i < 4; i++)
            {
                string cName = $"Corner_{i}";
                Transform cTr = targetBracketRoot.Find(cName);
                if (cTr == null)
                {
                    GameObject cObj = new GameObject(cName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    cObj.layer = LayerMask.NameToLayer("UI");
                    cObj.transform.SetParent(targetBracketRoot, false);

                    RectTransform rt = cObj.GetComponent<RectTransform>();
                    rt.anchorMin = cornerAnchors[i];
                    rt.anchorMax = cornerAnchors[i];
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(36f, 36f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, cornerRotations[i]);

                    Image img = cObj.GetComponent<Image>();
                    img.sprite = cornerSp;
                    img.color = new Color(1.0f, 0.85f, 0.28f, 0.96f);
                    img.raycastTarget = false;

                    cornerBrackets[i] = rt;
                }
                else
                {
                    cornerBrackets[i] = cTr.GetComponent<RectTransform>();
                }
            }
        }

        // 2) 몸통 마디(Node) 오브젝트 풀 생성
        if (nodeRects.Count < nodeCount)
        {
            nodeRects.Clear();
            nodeImages.Clear();

            Sprite nodeSp = GetOrCreateArrowNodeSprite();
            for (int i = 0; i < nodeCount; i++)
            {
                string nName = $"ArrowNode_{i}";
                Transform nTr = arrowRootRect.Find(nName);
                GameObject nObj;
                if (nTr == null)
                {
                    nObj = new GameObject(nName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    nObj.layer = LayerMask.NameToLayer("UI");
                    nObj.transform.SetParent(arrowRootRect, false);
                }
                else
                {
                    nObj = nTr.gameObject;
                }

                RectTransform rt = nObj.GetComponent<RectTransform>();
                UIHelper.ConfigureCenteredRect(rt, Vector2.zero, new Vector2(46f, 38f));

                Image img = nObj.GetComponent<Image>();
                img.sprite = nodeSp;
                img.color = normalArrowColor;
                img.raycastTarget = false;

                nodeRects.Add(rt);
                nodeImages.Add(img);
            }
        }

        // 3) 화살표 머리(Head) 오브젝트 생성
        if (headRect == null)
        {
            Transform hTr = arrowRootRect.Find("ArrowHead");
            GameObject hObj;
            if (hTr == null)
            {
                hObj = new GameObject("ArrowHead", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hObj.layer = LayerMask.NameToLayer("UI");
                hObj.transform.SetParent(arrowRootRect, false);
            }
            else
            {
                hObj = hTr.gameObject;
            }

            headRect = hObj.GetComponent<RectTransform>();
            UIHelper.ConfigureCenteredRect(headRect, Vector2.zero, new Vector2(96f, 92f));

            headImage = hObj.GetComponent<Image>();
            headImage.sprite = GetOrCreateArrowHeadSprite();
            headImage.color = normalArrowColor;
            headImage.raycastTarget = false;
        }
    }

    /// <summary>
    /// 카드를 잡고 PlayerHand 밖으로 나왔을 때 매 프레임 호출되어
    /// Spline 화살표 궤적과 Enemy 오브젝트 Hover 타겟팅 UI 및 적 HP 감소 프리뷰를 실시간 갱신합니다.
    /// </summary>
    public void UpdateTargetingArrow(Vector2 startCanvasLocalPos, Vector2 mouseScreenPos, Card activeCard = null)
    {
        InitializeSplineAndUI();
        if (arrowRootRect == null || canvasRect == null) return;

        if (!isArrowActive)
        {
            isArrowActive = true;
            arrowRootRect.gameObject.SetActive(true);
            arrowRootRect.SetAsLastSibling();
        }

        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouseScreenPos, uiCam, out Vector2 endCanvasLocalPos);

        // 1) 마우스 포인터 아래에 살아있는 Enemy 오브젝트가 있는지 감지
        SelectableUnit newHoveredEnemy = DetectHoveredEnemy(mouseScreenPos);
        if (currentHoveredEnemy != null && currentHoveredEnemy != newHoveredEnemy)
        {
            currentHoveredEnemy.ClearDamagePreview();
        }
        currentHoveredEnemy = newHoveredEnemy;
        UpdateEnemyTargetBrackets(currentHoveredEnemy, activeCard);

        // 2) UnityEngine.Splines의 BezierKnot 2개(시작점: 카드 상단, 끝점: 마우스 위치) 및 아치형 탄젠트 설정
        float dx = endCanvasLocalPos.x - startCanvasLocalPos.x;
        float dy = endCanvasLocalPos.y - startCanvasLocalPos.y;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float dynamicArc = Mathf.Clamp(dist * 0.42f, 110f, curveArcHeight);

        float3 p0 = new float3(startCanvasLocalPos.x, startCanvasLocalPos.y, 0f);
        float3 p1 = new float3(endCanvasLocalPos.x, endCanvasLocalPos.y, 0f);

        // 시작 탄젠트(Out): 위쪽과 마우스 반대/중간 쪽으로 부드럽게 솟아오르는 아치 형성
        float3 startTangentOut = new float3(-dx * 0.15f, dynamicArc, 0f);
        // 끝 탄젠트(In): 위쪽에서 마우스 지점으로 꽂히듯 진입
        float3 endTangentIn = new float3(-dx * 0.22f, dynamicArc * 0.55f, 0f);

        spline.SetKnot(0, new BezierKnot(p0, float3.zero, startTangentOut));
        spline.SetKnot(1, new BezierKnot(p1, endTangentIn, float3.zero));

        Color activeCol = currentHoveredEnemy != null ? lockedEnemyArrowColor : normalArrowColor;

        // 3) SplineUtility.Evaluate를 사용하여 마디(Nodes) 위치, 접선 회전, 점진적 스케일 일괄 적용
        for (int i = 0; i < nodeRects.Count; i++)
        {
            float t = (float)(i + 1) / (nodeRects.Count + 1);
            SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tangent, out _);

            RectTransform nodeRt = nodeRects[i];
            nodeRt.anchoredPosition = new Vector2(pos.x, pos.y);
            nodeRt.localPosition = new Vector3(pos.x, pos.y, 0f);

            Vector3 tanVec = new Vector3(tangent.x, tangent.y, 0f);
            if (tanVec.sqrMagnitude > 0.0001f)
            {
                nodeRt.localRotation = Quaternion.LookRotation(Vector3.forward, tanVec);
            }

            float scale = Mathf.Lerp(minNodeScale, maxNodeScale, t);
            nodeRt.localScale = Vector3.one * scale;

            if (nodeImages[i] != null)
                nodeImages[i].color = activeCol;
        }

        // 4) 화살표 머리(Head) 위치 및 회전 (t = 1.0f)
        if (headRect != null)
        {
            SplineUtility.Evaluate(spline, 0.98f, out _, out float3 headTangent, out _);
            headRect.anchoredPosition = endCanvasLocalPos;
            headRect.localPosition = new Vector3(endCanvasLocalPos.x, endCanvasLocalPos.y, 0f);

            Vector3 hTanVec = new Vector3(headTangent.x, headTangent.y, 0f);
            if (hTanVec.sqrMagnitude > 0.0001f)
            {
                headRect.localRotation = Quaternion.LookRotation(Vector3.forward, hTanVec);
            }

            float headScale = currentHoveredEnemy != null
                ? 1.08f + Mathf.Sin(Time.unscaledTime * 12f) * 0.05f
                : 1.0f;
            headRect.localScale = Vector3.one * headScale;

            if (headImage != null)
                headImage.color = activeCol;
        }
    }

    public void HideArrow()
    {
        if (currentHoveredEnemy != null)
        {
            currentHoveredEnemy.ClearDamagePreview();
        }

        isArrowActive = false;
        currentHoveredEnemy = null;

        if (targetBracketRoot != null)
            targetBracketRoot.gameObject.SetActive(false);

        if (arrowRootRect != null)
            arrowRootRect.gameObject.SetActive(false);
    }

    private SelectableUnit DetectHoveredEnemy(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        float distToPlane = Mathf.Abs(cam.transform.position.z);
        Vector3 worldPt3D = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distToPlane));
        Vector2 worldPt2D = new Vector2(worldPt3D.x, worldPt3D.y);

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPt2D);
        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                SelectableUnit unit = hits[i].GetComponent<SelectableUnit>() ?? hits[i].GetComponentInParent<SelectableUnit>();
                if (unit != null && !unit.IsPlayerUnit && !unit.IsDead)
                {
                    return unit;
                }
            }
        }

        return null;
    }

    private void UpdateEnemyTargetBrackets(SelectableUnit targetEnemy, Card activeCard = null)
    {
        if (targetBracketRoot == null) return;

        if (targetEnemy == null || targetEnemy.IsDead)
        {
            targetBracketRoot.gameObject.SetActive(false);
            return;
        }

        targetBracketRoot.gameObject.SetActive(true);
        UIHelper.SyncRectTransformToWorldPosition(
            targetBracketRoot,
            parentCanvas,
            canvasRect,
            targetEnemy.WorldBasePosition
        );

        // 숨쉬듯 살짝 움직이는 브래킷 애니메이션
        float pulse = Mathf.Sin(Time.unscaledTime * 8f) * 8f;
        float boxSize = 185f + pulse;
        targetBracketRoot.sizeDelta = new Vector2(boxSize, boxSize);

        // 적 HP바에 오른쪽에서부터 줄어들 예상 피해량(짙은 붉은색 게이지 + 수치 텍스트) 표시
        float previewDamage = activeCard != null ? activeCard.CalculatePreviewDamageAgainst(targetEnemy) : 0f;
        targetEnemy.SetDamagePreview(previewDamage);
    }

    // =========================================================================
    // 슬더스 스타일 화살표 마디 / 화살표 머리 / 코너 브래킷 텍스처 생성기
    // =========================================================================
    private static Sprite GetOrCreateArrowNodeSprite()
    {
        if (cachedNodeSprite != null) return cachedNodeSprite;

        int w = 64;
        int h = 56;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color darkBorder = new Color(0.24f, 0.02f, 0.04f, 0.98f);
        Color deepRed = new Color(0.68f, 0.08f, 0.12f, 1f);
        Color brightRed = new Color(0.95f, 0.22f, 0.24f, 1f);

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1); // 0(bottom) ~ 1(top)
            for (int x = 0; x < w; x++)
            {
                float nx = ((float)x / (w - 1) - 0.5f) * 2f; // -1 ~ 1
                float absX = Mathf.Abs(nx);

                // 위쪽으로 볼록한 화살 비늘(Chevron Scale) 형태
                float archY = ny + absX * 0.42f;
                bool inside = (archY >= 0.22f && archY <= 0.92f && absX <= 0.86f);

                Color c = Color.clear;
                if (inside)
                {
                    bool isBorder = (archY < 0.30f || archY > 0.84f || absX > 0.78f);
                    if (isBorder)
                    {
                        c = darkBorder;
                    }
                    else
                    {
                        float centerHighlight = Mathf.Clamp01(1f - absX * 1.15f);
                        c = Color.Lerp(deepRed, brightRed, centerHighlight);
                    }
                }
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedNodeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedNodeSprite.name = "sts_arrow_node";
        return cachedNodeSprite;
    }

    private static Sprite GetOrCreateArrowHeadSprite()
    {
        if (cachedHeadSprite != null) return cachedHeadSprite;

        int w = 96;
        int h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color darkBorder = new Color(0.22f, 0.02f, 0.03f, 0.98f);
        Color sideCrimson = new Color(0.72f, 0.08f, 0.12f, 1f);
        Color centerScarlet = new Color(0.98f, 0.25f, 0.25f, 1f);

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1); // 0(base) ~ 1(tip)
            for (int x = 0; x < w; x++)
            {
                float nx = ((float)x / (w - 1) - 0.5f) * 2f; // -1 ~ 1
                float absX = Mathf.Abs(nx);

                // 위쪽(ny=0.95)이 뾰족하고 아래쪽이 살짝 오목하게 들어간 슬더스 화살촉 모양
                float maxHalfWidth = (0.95f - ny) * 0.92f;
                float bottomConcave = 0.12f + absX * 0.14f;

                if (ny <= 0.95f && ny >= bottomConcave && absX <= maxHalfWidth)
                {
                    float edgeDist = Mathf.Min(maxHalfWidth - absX, ny - bottomConcave);
                    if (edgeDist < 0.055f)
                    {
                        pixels[y * w + x] = darkBorder;
                    }
                    else
                    {
                        float ridge = Mathf.Clamp01(1f - (absX / Mathf.Max(0.01f, maxHalfWidth)));
                        pixels[y * w + x] = Color.Lerp(sideCrimson, centerScarlet, ridge * 0.85f);
                    }
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedHeadSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.65f), 100f);
        cachedHeadSprite.name = "sts_arrow_head";
        return cachedHeadSprite;
    }

    private static Sprite GetOrCreateCornerBracketSprite()
    {
        if (cachedCornerBracketSprite != null) return cachedCornerBracketSprite;

        int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color outlineCol = new Color(0.18f, 0.08f, 0.01f, 0.98f);
        Color goldCol = new Color(1.0f, 0.86f, 0.28f, 1.0f);

        // 좌상단(┏) 모양 코너 브래킷 생성
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int topY = (size - 1) - y;
                bool inHorizontalBar = (topY >= 2 && topY <= 9 && x >= 2 && x <= 32);
                bool inVerticalBar = (x >= 2 && x <= 9 && topY >= 2 && topY <= 32);

                if (inHorizontalBar || inVerticalBar)
                {
                    bool isRim = (topY <= 3 || x <= 3 || (inHorizontalBar && topY >= 8 && x > 9) || (inVerticalBar && x >= 8 && topY > 9));
                    pixels[y * size + x] = isRim ? outlineCol : goldCol;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCornerBracketSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        cachedCornerBracketSprite.name = "sts_target_corner";
        return cachedCornerBracketSprite;
    }
}
