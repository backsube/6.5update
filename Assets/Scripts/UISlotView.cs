// =============================================================================
// UISlotView.cs
// Project Noumenon — UI Layer
//
// PURPOSE:
//   Pure reference script. Attach to UI_SlotPrefab.
//   Exposes serialized handles for each visual element of a single
//   inventory slot so gameplay / inventory code can update visuals
//   without hard-coded child lookups.
//
// DO NOT add item logic here.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Holds serialized references to all visual sub-elements of a single
/// inventory/loot slot.
/// Assign in the Inspector (auto-wired by the Editor generator).
/// </summary>
public class UISlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("── Item Info ──")]
    [Tooltip("이 슬롯이 담고 있는 현재 아이템 데이터입니다.")]
    public ItemData currentItemData;

    [Tooltip("이 슬롯이 맵에 있는 실제 아이템을 가리킬 경우 그 참조입니다. (Nearby Loot 용)")]
    public item_defualt sourceItem;

    [Tooltip("플레이어 인벤토리에 속한 경우, 시작 X 좌표")]
    public int gridX = -1;

    [Tooltip("플레이어 인벤토리에 속한 경우, 시작 Y 좌표")]
    public int gridY = -1;
    // -------------------------------------------------------------------------
    // Visual elements
    // -------------------------------------------------------------------------

    [Header("── Slot Images ──")]
    [Tooltip("Background image of the slot frame.")]
    public Image Image_SlotBackground;

    [Tooltip("Icon image for the item currently in this slot. " +
             "Should be transparent / inactive when slot is empty.")]
    public Image Image_ItemIcon;

    [Header("── Text ──")]
    [Tooltip("Stack-count or quantity label. Empty string when slot is empty.")]
    public TMP_Text Text_Amount;

    [Header("── State Overlays (inactive by default) ──")]
    [Tooltip("Outline overlay shown on pointer hover.")]
    public GameObject Image_HoverOutline;

    [Tooltip("Outline overlay shown when this slot is selected / active.")]
    public GameObject Image_SelectedOutline;

    [Tooltip("Highlight overlay shown while an item is being dragged over this slot.")]
    public GameObject Image_DragHighlight;

    void Awake()
    {
        ConfigureRaycastTargets();
    }

    private void ConfigureRaycastTargets()
    {
        // 슬롯 전체의 배경 하나만 포인터 입력을 받게 한다.
        // 아이콘 중앙의 TMP 텍스트나 투명 오버레이가 첫 슬롯의 드래그를
        // 가로채지 않도록 모든 장식 Graphic은 레이캐스트에서 제외한다.
        if (Image_SlotBackground != null)
            Image_SlotBackground.raycastTarget = true;
        if (Image_ItemIcon != null)
            Image_ItemIcon.raycastTarget = false;
        if (Text_Amount != null)
            Text_Amount.raycastTarget = false;

        SetOverlayRaycastTarget(Image_HoverOutline, false);
        SetOverlayRaycastTarget(Image_SelectedOutline, false);
        SetOverlayRaycastTarget(Image_DragHighlight, false);
    }

    private static void SetOverlayRaycastTarget(GameObject overlay, bool enabled)
    {
        if (overlay == null) return;
        Graphic graphic = overlay.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = enabled;
    }

    // -------------------------------------------------------------------------
    // Tooltip Events
    // -------------------------------------------------------------------------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentItemData != null && UITooltipManager.Instance != null)
        {
            string tooltipText = $"{currentItemData.itemName}\n<size=12>무게: {currentItemData.weight}kg | 가치: {currentItemData.price}¤</size>";
            UITooltipManager.Instance.ShowTooltip(tooltipText);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UITooltipManager.Instance != null)
        {
            UITooltipManager.Instance.HideTooltip();
        }
    }

    // -------------------------------------------------------------------------
    // Drag & Drop Events
    // -------------------------------------------------------------------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"[UISlotView] OnBeginDrag 발화! currentItemData={currentItemData?.itemName ?? "NULL"}, GO={gameObject.name}");
        if (currentItemData == null) return;
        inventory.Instance?.OnSlotBeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (currentItemData == null) return;
        inventory.Instance?.OnSlotDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (currentItemData == null) return;
        inventory.Instance?.OnSlotEndDrag(this, eventData);
    }
}
