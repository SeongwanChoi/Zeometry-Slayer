using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1) 화면 좌측 상단에 현재 턴을 확인할 수 있는 UI 이미지 표시
/// 2) 턴 시작 시 화면 가운데 왼쪽에서 등장 -> 화면 정중앙에 일정 시간(Inspector에서 조정 가능한 변수) 머무름 -> 오른쪽으로 지나가며 사라지는 배너 연출
/// </summary>
public class TurnUIController : MonoBehaviour
{
    [Header("1. 좌측 상단 현재 턴 표시 UI")]
    [SerializeField] private Image topLeftTurnImage;
    [SerializeField] private Sprite playerTurnStatusSprite;
    [SerializeField] private Sprite attackPhaseStatusSprite;
    [SerializeField] private Sprite enemyTurnStatusSprite;

    [Header("2. 턴 시작 시 중앙 배너 UI 및 이미지")]
    [SerializeField] private RectTransform centerBannerRect;
    [SerializeField] private Image centerBannerImage;
    [SerializeField] private Sprite playerTurnBannerSprite;
    [SerializeField] private Sprite attackPhaseBannerSprite;
    [SerializeField] private Sprite enemyTurnBannerSprite;

    [Header("3. 턴 배너 연출 시간 설정 (Inspector 조정 가능)")]
    [Tooltip("턴 시작 배너가 화면 정중앙에 머무르는 일정 시간 (초) - Inspector에서 조정 가능")]
    [SerializeField] private float centerHoldDuration = 1.0f;

    [Tooltip("화면 왼쪽 밖에서 정중앙으로 들어오는 이동 시간 (초)")]
    [SerializeField] private float slideInDuration = 0.35f;

    [Tooltip("정중앙에서 화면 오른쪽 밖으로 빠져나가는 이동 시간 (초)")]
    [SerializeField] private float slideOutDuration = 0.35f;

    [Header("4. 배너 이동 좌표 설정 (화면 중앙 기준)")]
    [SerializeField] private float startLeftX = -1500f;
    [SerializeField] private float centerX = 0f;
    [SerializeField] private float exitRightX = 1500f;
    [SerializeField] private float bannerY = 0f;

    private Coroutine bannerCoroutine;

    public float CenterHoldDuration
    {
        get => centerHoldDuration;
        set => centerHoldDuration = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        EnsureUIElements();
    }

    /// <summary>
    /// 좌측 상단 현재 턴 UI 이미지를 즉시 갱신합니다.
    /// </summary>
    public void UpdateTopLeftStatus(ETurn turn)
    {
        EnsureUIElements();
        if (topLeftTurnImage == null) return;

        Sprite targetSprite = GetStatusSprite(turn);
        if (targetSprite != null)
        {
            topLeftTurnImage.sprite = targetSprite;
            topLeftTurnImage.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 좌측 상단 UI를 갱신하고, 화면 왼쪽 -> 중앙(holdDuration 대기) -> 오른쪽으로 사라지는 턴 시작 배너 애니메이션을 재생합니다.
    /// </summary>
    public IEnumerator PlayTurnTransitionRoutine(ETurn turn, float overrideHoldDuration = -1f)
    {
        EnsureUIElements();
        UpdateTopLeftStatus(turn);

        if (centerBannerRect == null || centerBannerImage == null)
            yield break;

        Sprite bannerSprite = GetBannerSprite(turn);
        if (bannerSprite != null)
            centerBannerImage.sprite = bannerSprite;

        if (bannerCoroutine != null)
            StopCoroutine(bannerCoroutine);

        float holdTime = overrideHoldDuration >= 0f ? overrideHoldDuration : centerHoldDuration;
        yield return StartCoroutine(CoSlideBannerLeftToRight(holdTime));
    }

    private IEnumerator CoSlideBannerLeftToRight(float holdTime)
    {
        centerBannerRect.gameObject.SetActive(true);
        centerBannerRect.SetAsLastSibling();

        // 1단계: 화면 가운데 왼쪽(startLeftX)에서 출발하여 정중앙(centerX)으로 이동
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slideInDuration));
            // 부드러운 감속 곡선 (EaseOutCubic)
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float curX = Mathf.Lerp(startLeftX, centerX, eased);
            centerBannerRect.anchoredPosition = new Vector2(curX, bannerY);
            yield return null;
        }

        centerBannerRect.anchoredPosition = new Vector2(centerX, bannerY);

        // 2단계: 화면 정중앙에서 일정 시간(holdTime) 동안 대기
        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        // 3단계: 정중앙(centerX)에서 오른쪽(exitRightX)으로 지나가며 사라짐
        elapsed = 0f;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slideOutDuration));
            // 부드러운 가속 곡선 (EaseInCubic)
            float eased = t * t * t;
            float curX = Mathf.Lerp(centerX, exitRightX, eased);
            centerBannerRect.anchoredPosition = new Vector2(curX, bannerY);
            yield return null;
        }

        centerBannerRect.anchoredPosition = new Vector2(exitRightX, bannerY);
        centerBannerRect.gameObject.SetActive(false);
        bannerCoroutine = null;
    }

    private Sprite GetStatusSprite(ETurn turn)
    {
        switch (turn)
        {
            case ETurn.PLAYER: return playerTurnStatusSprite;
            case ETurn.ATTACK: return attackPhaseStatusSprite != null ? attackPhaseStatusSprite : playerTurnStatusSprite;
            case ETurn.ENEMY:  return enemyTurnStatusSprite;
            default:           return playerTurnStatusSprite;
        }
    }

    private Sprite GetBannerSprite(ETurn turn)
    {
        switch (turn)
        {
            case ETurn.PLAYER: return playerTurnBannerSprite;
            case ETurn.ATTACK: return attackPhaseBannerSprite != null ? attackPhaseBannerSprite : playerTurnBannerSprite;
            case ETurn.ENEMY:  return enemyTurnBannerSprite;
            default:           return playerTurnBannerSprite;
        }
    }

    public void EnsureUIElements()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 1. 좌측 상단 현재 턴 표시 이미지 자동 연결/생성
        if (topLeftTurnImage == null)
        {
            Transform existingStatus = canvas.transform.Find("TurnStatusUI");
            if (existingStatus != null)
            {
                topLeftTurnImage = existingStatus.GetComponent<Image>();
            }
            else
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

                topLeftTurnImage = statusObj.GetComponent<Image>();
                topLeftTurnImage.raycastTarget = false;
                topLeftTurnImage.preserveAspect = true;
            }
        }

        // 2. 화면 중앙 슬라이딩 배너 이미지 자동 연결/생성
        if (centerBannerRect == null || centerBannerImage == null)
        {
            Transform existingBanner = canvas.transform.Find("TurnBannerUI");
            if (existingBanner != null)
            {
                centerBannerRect = existingBanner.GetComponent<RectTransform>();
                centerBannerImage = existingBanner.GetComponent<Image>();
            }
            else
            {
                GameObject bannerObj = new GameObject("TurnBannerUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bannerObj.layer = LayerMask.NameToLayer("UI");
                bannerObj.transform.SetParent(canvas.transform, false);

                centerBannerRect = bannerObj.GetComponent<RectTransform>();
                centerBannerRect.anchorMin = new Vector2(0.5f, 0.5f);
                centerBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
                centerBannerRect.pivot = new Vector2(0.5f, 0.5f);
                centerBannerRect.anchoredPosition = new Vector2(startLeftX, bannerY);
                centerBannerRect.sizeDelta = new Vector2(900f, 160f);

                centerBannerImage = bannerObj.GetComponent<Image>();
                centerBannerImage.raycastTarget = false;
                centerBannerImage.preserveAspect = true;
                bannerObj.SetActive(false);
            }
        }
    }
}
