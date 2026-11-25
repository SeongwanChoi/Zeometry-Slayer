using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 개별 스킬 아이콘 버튼의 PointerEnter(호버 확대) / PointerExit(원래 크기 복귀) / PointerDown(축소) / PointerUp(스킬 확인창 표시 또는 쿨타임 진동) 이벤트를 전달합니다.
/// </summary>
public class SkillIconPointerReceiver : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    public const float HoverScale = 1.18f;

    private PlayerSkillUI ownerUI;
    private ESkillType skillType;
    private RectTransform targetRect;
    private bool isPointerInside = false;
    private bool isPressed = false;

    public void Configure(PlayerSkillUI ui, ESkillType type, RectTransform rect)
    {
        ownerUI = ui;
        skillType = type;
        targetRect = rect;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        if (!isPressed && targetRect != null)
        {
            targetRect.localScale = Vector3.one * HoverScale;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        if (targetRect != null)
        {
            targetRect.localScale = Vector3.one;
        }
    }

    private void OnDisable()
    {
        isPointerInside = false;
        isPressed = false;
        if (targetRect != null)
        {
            targetRect.localScale = Vector3.one;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        isPressed = true;
        isPointerInside = true;
        if (ownerUI != null)
            ownerUI.HandleSkillPointerDown(skillType, targetRect);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!isPressed) return;
        isPressed = false;

        if (ownerUI != null)
            ownerUI.HandleSkillPointerUp(skillType, targetRect, isPointerInside);
    }
}
