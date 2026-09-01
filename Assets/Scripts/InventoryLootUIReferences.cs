// =============================================================================
// InventoryLootUIReferences.cs
// Project Noumenon — UI Layer
//
// PURPOSE:
//   Pure reference script. Attach to UI_InventoryLootCanvas.
//   Exposes serialized handles so gameplay / inventory code can wire
//   into the UI without hard-coded GameObject.Find() calls.
//
// DO NOT add gameplay logic here.
// =============================================================================

using UnityEngine;

/// <summary>
/// Holds serialized references to every major element of the
/// Inventory/Loot UI canvas.  Assign in the Inspector after prefab
/// generation via  Tools → Noumenon → UI → Generate Inventory Loot UI.
/// </summary>
public class InventoryLootUIReferences : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Top-level panels
    // -------------------------------------------------------------------------

    [Header("── Top-Level Panels ──")]
    [Tooltip("The player's own inventory panel (left side).")]
    public GameObject UI_PlayerInventoryPanel;

    [Tooltip("The nearby / area loot panel (right side).")]
    public GameObject UI_AreaLootPanel;

    [Tooltip("Interaction prompt shown in the world (key + action text).")]
    public GameObject UI_InteractionPrompt;

    [Tooltip("Floating drag preview image that follows the pointer.")]
    public GameObject UI_DragPreview;

    [Tooltip("Reserved visual space for the player character model.")]
    public GameObject Area_PlayerPreview;

    // -------------------------------------------------------------------------
    // Grids / dynamic containers
    // -------------------------------------------------------------------------

    [Header("── Grids & Container Roots ──")]
    [Tooltip("GridLayoutGroup parent for the player's inventory slots.")]
    public Transform Grid_PlayerInventory;

    [Tooltip("GridLayoutGroup parent for nearby loot slots.")]
    public Transform Grid_NearbyLoot;

    [Tooltip("Parent transform where ContainerSection prefabs are instantiated at runtime.")]
    public Transform ContainerSectionRoot;

    // -------------------------------------------------------------------------
    // Prefab references (assign the prefab assets, not scene instances)
    // -------------------------------------------------------------------------

    [Header("── Prefab References ──")]
    [Tooltip("UI_SlotPrefab — individual inventory slot.")]
    public GameObject SlotPrefab;

    [Tooltip("UI_ContainerSection_TypeA (10 slots).")]
    public GameObject ContainerSectionTypeAPrefab;

    [Tooltip("UI_ContainerSection_TypeB (20 slots).")]
    public GameObject ContainerSectionTypeBPrefab;

    [Tooltip("UI_ContainerSection_TypeC (40 slots).")]
    public GameObject ContainerSectionTypeCPrefab;
}
