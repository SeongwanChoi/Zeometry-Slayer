using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 클릭 가능한 UI 요소(TurnEndButtonUI, DrawPileUI, DiscardPileUI, QuitGameButton, ResultActionButton 등)에 부착되어
/// 마우스 포인터가 올라왔을 때(OnPointerEnter) 크기가 커지고, 벗어났을 때(OnPointerExit) 원래 크기로 돌아오게 하는 컴포넌트입니다.
/// </summary>
public class UIHoverScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScaleMultiplier = 1.12f;

    private Vector3 baseScale = Vector3.one;
    private bool baseScaleCached = false;
    private bool isHovered = false;

    public float HoverScaleMultiplier => hoverScaleMultiplier;
    public bool IsHovered => isHovered;

    public void Configure(float hoverScale)
    {
        hoverScaleMultiplier = Mathf.Max(1.01f, hoverScale);
        CacheBaseScaleIfNeeded();
    }

    private void CacheBaseScaleIfNeeded()
    {
        if (!baseScaleCached)
        {
            Vector3 cur = transform.localScale;
            baseScale = cur.sqrMagnitude > 0.001f ? cur : Vector3.one;
            baseScaleCached = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CacheBaseScaleIfNeeded();

        Selectable sel = GetComponent<Selectable>();
        if (sel != null && !sel.IsInteractable())
            return;

        isHovered = true;
        transform.localScale = baseScale * hoverScaleMultiplier;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CacheBaseScaleIfNeeded();
        isHovered = false;
        transform.localScale = baseScale;
    }

    private void OnDisable()
    {
        if (baseScaleCached)
        {
            isHovered = false;
            transform.localScale = baseScale;
        }
    }
}
