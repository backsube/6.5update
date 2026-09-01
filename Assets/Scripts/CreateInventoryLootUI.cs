// =============================================================================
// CreateInventoryLootUI.cs
// Project Noumenon — Editor Tools
//
// PURPOSE:
//   Generates all UI Prefabs and a test scene for the Inventory / Loot UI.
//   Run via:  Tools → Noumenon → UI → Generate Inventory Loot UI
//
// WHAT IT CREATES:
//   Assets/_ProjectNoumenon/UI/Prefabs/
//     • UI_InventoryLootCanvas.prefab
//     • UI_SlotPrefab.prefab
//     • UI_ContainerSection_TypeA.prefab  (10 slots)
//     • UI_ContainerSection_TypeB.prefab  (20 slots)
//     • UI_ContainerSection_TypeC.prefab  (40 slots)
//   Assets/_ProjectNoumenon/UI/Scenes/
//     • UI_TestScene.unity
//
// LAYOUT (1920×1080 reference):
//   Left panel  — UI_PlayerInventoryPanel  (820 × 960)
//     ├─ BG_PlayerInventory          (stretch fill, dark bluish-grey)
//     ├─ Text_Title_PlayerInventory
//     ├─ Area_Equipment              (top-right: 2-col × 3-row grid of 72×72 slots)
//     │    ├─ Text_EquipmentTitle
//     │    └─ Grid_EquipmentSlots    (GridLayoutGroup 2 cols)
//     │         └─ Slot_Weapon … Slot_Accessory2
//     ├─ Area_BodyStatus_Dummy       (below equipment: 3-col × 2-row grid)
//     │    ├─ Text_BodyStatusTitle
//     │    └─ Grid_BodyStatusSlots   (GridLayoutGroup 3 cols)
//     │         └─ Body_Head … Body_RightLeg
//     ├─ Area_PlayerPreview          (centre-right column, 240 × 520)
//     │    ├─ BG_PlayerPreview
//     │    ├─ Image_PlayerSilhouette
//     │    └─ Text_PlayerPreviewLabel
//     └─ Grid_PlayerInventory        (bottom strip: 30 slots, 6-col, 72×72)
//          └─ Slot_Player_00 … Slot_Player_29
//
//   Right panel — UI_AreaLootPanel  (820 × 960)
//     ├─ BG_AreaLoot                 (stretch fill)
//     ├─ Text_Title_AreaLoot
//     ├─ Text_Subtitle_Nearby
//     ├─ Grid_NearbyLoot             (24 slots, 6-col, 72×72)
//     │    └─ Slot_Nearby_00 … Slot_Nearby_23
//     └─ ContainerSectionRoot        (runtime VLG parent)
//
// RULES:
//   - Does NOT modify gameplay scripts or existing scenes.
//   - Does NOT implement item-transfer logic.
//   - Overwrites prefabs if they already exist (idempotent re-generation).
// =============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class CreateInventoryLootUI
{
    // ─────────────────────────────────────────────────────────────────────────
    // Paths
    // ─────────────────────────────────────────────────────────────────────────
    private const string PrefabRoot = "Assets/_ProjectNoumenon/UI/Prefabs";
    private const string SceneRoot  = "Assets/_ProjectNoumenon/UI/Scenes";
    private const string ScenePath  = SceneRoot + "/UI_TestScene.unity";

    // ─────────────────────────────────────────────────────────────────────────
    // Shared colours (dark bluish-grey palette)
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly Color ColPanelBg      = new Color(0.08f, 0.10f, 0.14f, 0.97f);
    private static readonly Color ColPanelBgDeep  = new Color(0.05f, 0.07f, 0.10f, 1.00f);
    private static readonly Color ColSectionBg    = new Color(0.10f, 0.13f, 0.18f, 0.90f);
    private static readonly Color ColSlotBg       = new Color(0.14f, 0.17f, 0.23f, 1.00f);
    private static readonly Color ColSlotIcon     = new Color(1f, 1f, 1f, 0f);
    private static readonly Color ColPreviewBg    = new Color(0.07f, 0.10f, 0.16f, 0.85f);
    private static readonly Color ColPreviewSilh  = new Color(0.20f, 0.25f, 0.35f, 0.60f);
    private static readonly Color ColBodySlot     = new Color(0.18f, 0.14f, 0.20f, 1.00f);
    private static readonly Color ColTitleText    = new Color(0.85f, 0.88f, 1.00f, 1.00f);
    private static readonly Color ColSubText      = new Color(0.60f, 0.65f, 0.75f, 1.00f);
    private static readonly Color ColEquipTitle   = new Color(0.80f, 0.85f, 0.70f, 1.00f);

    // ─────────────────────────────────────────────────────────────────────────
    // Menu entry
    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Noumenon/UI/Generate Inventory Loot UI")]
    public static void GenerateAll()
    {
        EnsureFolders();

        // Build leaf prefabs first so they can be referenced later
        GameObject slotPrefabAsset      = CreateSlotPrefab();
        GameObject containerTypeAPrefab = CreateContainerSection("TypeA", 10);
        GameObject containerTypeBPrefab = CreateContainerSection("TypeB", 20);
        GameObject containerTypeCPrefab = CreateContainerSection("TypeC", 40);

        // Build the main canvas prefab
        CreateMainCanvasPrefab(slotPrefabAsset,
                               containerTypeAPrefab,
                               containerTypeBPrefab,
                               containerTypeCPrefab);

        // Create (or refresh) the test scene
        CreateTestScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Noumenon UI] All UI prefabs and test scene generated successfully.");
        EditorUtility.DisplayDialog(
            "Noumenon UI Generator",
            "Prefabs and test scene generated!\n\n" +
            "Prefabs → Assets/_ProjectNoumenon/UI/Prefabs/\n" +
            "Scene   → Assets/_ProjectNoumenon/UI/Scenes/UI_TestScene.unity\n\n" +
            "Grid_PlayerInventory : 30 slots (6-col, 72×72)\n" +
            "Grid_NearbyLoot      : 24 slots (6-col, 72×72)\n" +
            "Area_PlayerPreview   : 240×520 reserved in left panel",
            "OK");
    }

    // =========================================================================
    // FOLDER SETUP
    // =========================================================================
    private static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/_ProjectNoumenon",
            "Assets/_ProjectNoumenon/UI",
            "Assets/_ProjectNoumenon/UI/Prefabs",
            "Assets/_ProjectNoumenon/UI/Sprites",
            "Assets/_ProjectNoumenon/UI/Scripts",
            "Assets/_ProjectNoumenon/UI/Editor",
            "Assets/_ProjectNoumenon/UI/Scenes",
        };

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string name   = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    // =========================================================================
    // SLOT PREFAB  (UI_SlotPrefab — 72×72)
    // =========================================================================
    /// <summary>
    /// UI_SlotPrefab  (72×72 square)
    /// Children:
    ///   Image_SlotBackground  — opaque dark panel
    ///   Image_ItemIcon        — transparent / inactive
    ///   Text_Amount           — empty TMP label
    ///   Image_HoverOutline    — inactive
    ///   Image_SelectedOutline — inactive
    ///   Image_DragHighlight   — inactive
    /// </summary>
    private static GameObject CreateSlotPrefab()
    {
        string prefabPath = PrefabRoot + "/UI_SlotPrefab.prefab";

        GameObject root = new GameObject("UI_SlotPrefab");
        RectTransform rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(72, 72);

        BuildSlotChildren(root, ColSlotBg);

        UISlotView slotView = root.AddComponent<UISlotView>();
        WireSlotView(slotView, root);

        GameObject savedPrefab = SavePrefab(root, prefabPath);
        GameObject.DestroyImmediate(root);
        return savedPrefab;
    }

    // =========================================================================
    // CONTAINER SECTION PREFABS  (TypeA / TypeB / TypeC)
    // =========================================================================
    /// <summary>
    /// Creates one of the three container section prefab types.
    /// typeSuffix = "TypeA" | "TypeB" | "TypeC"
    /// slotCount  = 10 | 20 | 40
    /// </summary>
    private static GameObject CreateContainerSection(string typeSuffix, int slotCount)
    {
        string prefabPath = $"{PrefabRoot}/UI_ContainerSection_{typeSuffix}.prefab";

        // ── root ──────────────────────────────────────────────────────────────
        GameObject root = new GameObject($"UI_ContainerSection_{typeSuffix}");
        RectTransform rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(740, 280);

        VerticalLayoutGroup vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment        = TextAnchor.UpperLeft;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        ContentSizeFitter csf = root.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── BG_Container_TypeX ────────────────────────────────────────────────
        GameObject bg = CreateUIImage(root, $"BG_Container_{typeSuffix}", ColSectionBg);
        StretchFill(bg.GetComponent<RectTransform>());
        bg.transform.SetAsFirstSibling();

        // ── Text_ContainerName ────────────────────────────────────────────────
        GameObject labelGO = new GameObject("Text_ContainerName");
        labelGO.transform.SetParent(root.transform, false);
        TMP_Text label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text      = $"Container ({typeSuffix})";
        label.fontSize  = 18;
        label.fontStyle = FontStyles.Bold;
        label.color     = ColEquipTitle;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(0, 28);

        // ── Grid_ContainerSlots ───────────────────────────────────────────────
        GameObject gridGO = new GameObject("Grid_ContainerSlots");
        gridGO.transform.SetParent(root.transform, false);
        // AddComponent<RectTransform> FIRST — plain new GameObject() has none.
        RectTransform gridRT = gridGO.AddComponent<RectTransform>();
        gridRT.sizeDelta = new Vector2(0, 0);

        GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(72, 72);
        grid.spacing         = new Vector2(6, 6);
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;

        ContentSizeFitter gridCSF = gridGO.AddComponent<ContentSizeFitter>();
        gridCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        gridCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── Placeholder slots (full slot structure) ───────────────────────────
        string slotPrefix = typeSuffix == "TypeA" ? "Slot_TypeA_"
                          : typeSuffix == "TypeB" ? "Slot_TypeB_"
                                                  : "Slot_TypeC_";
        for (int i = 0; i < slotCount; i++)
            CreateInlineSlot(gridGO, $"{slotPrefix}{i:D2}", ColSlotBg, 72);

        // ── Save prefab ───────────────────────────────────────────────────────
        GameObject savedPrefab = SavePrefab(root, prefabPath);
        GameObject.DestroyImmediate(root);
        return savedPrefab;
    }

    // =========================================================================
    // MAIN CANVAS PREFAB
    // =========================================================================
    private static void CreateMainCanvasPrefab(
        GameObject slotPrefabAsset,
        GameObject containerTypeAPrefabAsset,
        GameObject containerTypeBPrefabAsset,
        GameObject containerTypeCPrefabAsset)
    {
        string prefabPath = PrefabRoot + "/UI_InventoryLootCanvas.prefab";

        // ── Canvas root ───────────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("UI_InventoryLootCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── UI_InteractionPrompt ──────────────────────────────────────────────
        // Bottom-left corner, 320×80
        GameObject prompt = CreatePanel(canvasGO, "UI_InteractionPrompt",
            new Vector2(320, 80),
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(50, 50));
        prompt.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.88f);

        GameObject bgPrompt = CreateUIImage(prompt, "BG_Prompt",
            new Color(0.08f, 0.10f, 0.14f, 0.92f));
        StretchFill(bgPrompt.GetComponent<RectTransform>());

        CreateTMPLabel(prompt, "Text_Key", "[Tab]", 20, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(8, 0), new Vector2(0, 0));

        CreateTMPLabel(prompt, "Text_Action", "Open Inventory", 16, TextAlignmentOptions.MidlineLeft,
            new Vector2(0.32f, 0f), new Vector2(1f, 1f), new Vector2(0, 0), new Vector2(-8, 0));

        // ── UI_PlayerInventoryPanel (LEFT, 820 × 960) ─────────────────────────
        // anchor/pivot = left-centre; anchoredPos centres it 410 px from left edge
        GameObject playerPanel = CreatePanel(canvasGO, "UI_PlayerInventoryPanel",
            new Vector2(820, 960),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(30 + 410, 0));
        playerPanel.GetComponent<Image>().color = ColPanelBg;

        GameObject bgPlayer = CreateUIImage(playerPanel, "BG_PlayerInventory", ColPanelBgDeep);
        StretchFill(bgPlayer.GetComponent<RectTransform>());

        // Title — anchored top-left, 32 px from top
        CreateTMPLabel(playerPanel, "Text_Title_PlayerInventory", "INVENTORY",
            28, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16, -56), new Vector2(-16, 0));

        // ── Separator line under title ─────────────────────────────────────
        GameObject sepPlayer = CreateUIImage(playerPanel, "Sep_PlayerTitle",
            new Color(0.25f, 0.30f, 0.45f, 0.60f));
        RectTransform sepPRT = sepPlayer.GetComponent<RectTransform>();
        sepPRT.anchorMin = new Vector2(0f, 1f); sepPRT.anchorMax = new Vector2(1f, 1f);
        sepPRT.pivot     = new Vector2(0.5f, 1f);
        sepPRT.sizeDelta = new Vector2(-24, 2);
        sepPRT.anchoredPosition = new Vector2(0, -62);

        // ──────────────────────────────────────────────────────────────────────
        // Layout guide (all y values are anchoredPosition from top of 960-px panel,
        // with anchor=(0,1) / pivot=(0,1)):
        //
        //   y=  -70   Area_Equipment       180 × 240   (top-right, x=560)
        //   y= -320   Area_BodyStatus_Dummy 180 × 240   (below equipment, x=560)
        //   y=  -70   Area_PlayerPreview   240 × 520   (centre-right, x=300)
        //   y= -600   Grid_PlayerInventory (bottom strip, x=16, full width)
        // ──────────────────────────────────────────────────────────────────────

        // ── Area_PlayerPreview  (240 × 520, centre of right half) ─────────────
        // x=300 puts left edge at 300px → right edge at 540px (panel is 820 wide)
        GameObject areaPreview = new GameObject("Area_PlayerPreview");
        areaPreview.transform.SetParent(playerPanel.transform, false);
        RectTransform previewRT = areaPreview.AddComponent<RectTransform>();
        previewRT.anchorMin = new Vector2(0f, 1f);
        previewRT.anchorMax = new Vector2(0f, 1f);
        previewRT.pivot     = new Vector2(0f, 1f);
        previewRT.sizeDelta = new Vector2(240, 520);
        previewRT.anchoredPosition = new Vector2(300, -70);

        // BG_PlayerPreview
        GameObject bgPreview = CreateUIImage(areaPreview, "BG_PlayerPreview", ColPreviewBg);
        StretchFill(bgPreview.GetComponent<RectTransform>());

        // Image_PlayerSilhouette — 80×200, centred
        GameObject silhGO = CreateUIImage(areaPreview, "Image_PlayerSilhouette", ColPreviewSilh);
        RectTransform silhRT = silhGO.GetComponent<RectTransform>();
        silhRT.anchorMin = new Vector2(0.5f, 0.5f);
        silhRT.anchorMax = new Vector2(0.5f, 0.5f);
        silhRT.pivot     = new Vector2(0.5f, 0.5f);
        silhRT.sizeDelta = new Vector2(80, 200);
        silhRT.anchoredPosition = new Vector2(0, 20);

        // Text_PlayerPreviewLabel — bottom strip of the preview box
        CreateTMPLabel(areaPreview, "Text_PlayerPreviewLabel", "PLAYER PREVIEW",
            13, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0, 8), new Vector2(0, 28));

        // ── Area_Equipment  (top-right, 200 × 240, 2-col × 3-row) ───────────
        // Placed in the right strip beside Area_PlayerPreview: x=555
        GameObject areaEquip = new GameObject("Area_Equipment");
        areaEquip.transform.SetParent(playerPanel.transform, false);
        RectTransform equipRT = areaEquip.AddComponent<RectTransform>();
        equipRT.anchorMin = new Vector2(0f, 1f);
        equipRT.anchorMax = new Vector2(0f, 1f);
        equipRT.pivot     = new Vector2(0f, 1f);
        equipRT.sizeDelta = new Vector2(200, 260);
        equipRT.anchoredPosition = new Vector2(556, -70);

        // BG
        GameObject bgEquip = CreateUIImage(areaEquip, "BG_Equipment", ColSectionBg);
        StretchFill(bgEquip.GetComponent<RectTransform>());

        // Title
        CreateTMPLabel(areaEquip, "Text_EquipmentTitle", "EQUIPMENT",
            14, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(8, -28), new Vector2(-8, 0));

        // Grid_EquipmentSlots  — 2 columns × 3 rows
        GameObject gridEquip = new GameObject("Grid_EquipmentSlots");
        gridEquip.transform.SetParent(areaEquip.transform, false);
        RectTransform gridEquipRT = gridEquip.AddComponent<RectTransform>();
        gridEquipRT.anchorMin = new Vector2(0f, 1f);
        gridEquipRT.anchorMax = new Vector2(1f, 1f);
        gridEquipRT.pivot     = new Vector2(0.5f, 1f);
        gridEquipRT.sizeDelta = new Vector2(0, 0);
        gridEquipRT.anchoredPosition = new Vector2(0, -36);

        GridLayoutGroup gridEquipLG = gridEquip.AddComponent<GridLayoutGroup>();
        gridEquipLG.cellSize        = new Vector2(72, 72);
        gridEquipLG.spacing         = new Vector2(8, 8);
        gridEquipLG.padding         = new RectOffset(8, 8, 0, 8);
        gridEquipLG.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        gridEquipLG.startAxis       = GridLayoutGroup.Axis.Horizontal;
        gridEquipLG.childAlignment  = TextAnchor.UpperLeft;
        gridEquipLG.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridEquipLG.constraintCount = 2;

        ContentSizeFitter equipCSF = gridEquip.AddComponent<ContentSizeFitter>();
        equipCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        equipCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        string[] equipSlotNames = { "Slot_Weapon", "Slot_Armor", "Slot_Bag",
                                    "Slot_Head",   "Slot_Accessory1", "Slot_Accessory2" };
        foreach (string sn in equipSlotNames)
            CreateInlineSlot(gridEquip, sn, ColSlotBg, 72);

        // ── Area_BodyStatus_Dummy  (below equipment, 200 × 200) ──────────────
        GameObject areaBody = new GameObject("Area_BodyStatus_Dummy");
        areaBody.transform.SetParent(playerPanel.transform, false);
        RectTransform bodyRT = areaBody.AddComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 1f);
        bodyRT.anchorMax = new Vector2(0f, 1f);
        bodyRT.pivot     = new Vector2(0f, 1f);
        bodyRT.sizeDelta = new Vector2(200, 200);
        bodyRT.anchoredPosition = new Vector2(556, -342);  // 70 + 260 + 12 gap

        // BG
        GameObject bgBody = CreateUIImage(areaBody, "BG_BodyStatus", ColSectionBg);
        StretchFill(bgBody.GetComponent<RectTransform>());

        // Title
        CreateTMPLabel(areaBody, "Text_BodyStatusTitle", "BODY STATUS",
            14, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(8, -28), new Vector2(-8, 0));

        // Grid_BodyStatusSlots  — 3 columns × 2 rows
        GameObject gridBody = new GameObject("Grid_BodyStatusSlots");
        gridBody.transform.SetParent(areaBody.transform, false);
        RectTransform gridBodyRT = gridBody.AddComponent<RectTransform>();
        gridBodyRT.anchorMin = new Vector2(0f, 1f);
        gridBodyRT.anchorMax = new Vector2(1f, 1f);
        gridBodyRT.pivot     = new Vector2(0.5f, 1f);
        gridBodyRT.sizeDelta = new Vector2(0, 0);
        gridBodyRT.anchoredPosition = new Vector2(0, -36);

        GridLayoutGroup gridBodyLG = gridBody.AddComponent<GridLayoutGroup>();
        gridBodyLG.cellSize        = new Vector2(64, 64);
        gridBodyLG.spacing         = new Vector2(6, 6);
        gridBodyLG.padding         = new RectOffset(6, 6, 0, 6);
        gridBodyLG.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        gridBodyLG.startAxis       = GridLayoutGroup.Axis.Horizontal;
        gridBodyLG.childAlignment  = TextAnchor.UpperLeft;
        gridBodyLG.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridBodyLG.constraintCount = 3;

        ContentSizeFitter bodyCSF = gridBody.AddComponent<ContentSizeFitter>();
        bodyCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        bodyCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        string[] bodyPartNames = { "Body_Head", "Body_Torso", "Body_LeftArm",
                                   "Body_RightArm", "Body_LeftLeg", "Body_RightLeg" };
        foreach (string bn in bodyPartNames)
            CreateInlineSlot(gridBody, bn, ColBodySlot, 64);

        // ── Grid_PlayerInventory  (bottom strip, 30 slots, 6-col) ─────────────
        // 좌상단(UpperLeft) 기준으로 일관되게 설정
        GameObject gridPlayer = new GameObject("Grid_PlayerInventory");
        gridPlayer.transform.SetParent(playerPanel.transform, false);
        RectTransform gridPlayerRT = gridPlayer.AddComponent<RectTransform>();
        gridPlayerRT.anchorMin        = new Vector2(0f, 1f);
        gridPlayerRT.anchorMax        = new Vector2(0f, 1f);
        gridPlayerRT.pivot            = new Vector2(0f, 1f);
        gridPlayerRT.anchoredPosition = new Vector2(16, -600);  // 패널 상단에서 600px 아래
        gridPlayerRT.sizeDelta        = new Vector2(550, 0);    // width fits 6 cols of 72 + spacing

        GridLayoutGroup gridPLG = gridPlayer.AddComponent<GridLayoutGroup>();
        gridPLG.cellSize        = new Vector2(72, 72);
        gridPLG.spacing         = new Vector2(8, 8);
        gridPLG.padding         = new RectOffset(0, 0, 0, 0);
        gridPLG.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        gridPLG.startAxis       = GridLayoutGroup.Axis.Horizontal;
        gridPLG.childAlignment  = TextAnchor.UpperLeft;
        gridPLG.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridPLG.constraintCount = 6;

        ContentSizeFitter playerCSF = gridPlayer.AddComponent<ContentSizeFitter>();
        playerCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        playerCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 30 visible player inventory slots
        for (int i = 0; i < 30; i++)
            CreateInlineSlot(gridPlayer, $"Slot_Player_{i:D2}", ColSlotBg, 72);

        // ── Label above Grid_PlayerInventory ──────────────────────────────────
        CreateTMPLabel(playerPanel, "Text_BackpackTitle", "BACKPACK",
            15, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(16, 426 + 12), new Vector2(200, 426 + 32));
        // (5 rows × (72+8) − 8 = 392, +2 rows=? just label it simply)

        // ═══════════════════════════════════════════════════════════════════════
        // UI_AreaLootPanel (RIGHT, 820 × 960)
        // ═══════════════════════════════════════════════════════════════════════
        // anchor/pivot = right-centre
        GameObject lootPanel = CreatePanel(canvasGO, "UI_AreaLootPanel",
            new Vector2(820, 960),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-(30 + 410), 0));
        lootPanel.GetComponent<Image>().color = ColPanelBg;

        GameObject bgLoot = CreateUIImage(lootPanel, "BG_AreaLoot", ColPanelBgDeep);
        StretchFill(bgLoot.GetComponent<RectTransform>());

        // Title
        CreateTMPLabel(lootPanel, "Text_Title_AreaLoot", "NEARBY LOOT",
            28, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16, -56), new Vector2(-16, 0));

        // Separator
        GameObject sepLoot = CreateUIImage(lootPanel, "Sep_LootTitle",
            new Color(0.25f, 0.30f, 0.45f, 0.60f));
        RectTransform sepLRT = sepLoot.GetComponent<RectTransform>();
        sepLRT.anchorMin = new Vector2(0f, 1f); sepLRT.anchorMax = new Vector2(1f, 1f);
        sepLRT.pivot     = new Vector2(0.5f, 1f);
        sepLRT.sizeDelta = new Vector2(-24, 2);
        sepLRT.anchoredPosition = new Vector2(0, -62);

        // Subtitle
        CreateTMPLabel(lootPanel, "Text_Subtitle_Nearby", "Items within range",
            14, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16, -80), new Vector2(-16, -56));

        // ── Grid_NearbyLoot  (24 slots, 6-col, 72×72, anchored top) ──────────
        GameObject gridNearby = new GameObject("Grid_NearbyLoot");
        gridNearby.transform.SetParent(lootPanel.transform, false);
        RectTransform gridNearbyRT = gridNearby.AddComponent<RectTransform>();
        gridNearbyRT.anchorMin        = new Vector2(0f, 1f);
        gridNearbyRT.anchorMax        = new Vector2(1f, 1f);
        gridNearbyRT.pivot            = new Vector2(0.5f, 1f);
        gridNearbyRT.anchoredPosition = new Vector2(0, -106);
        gridNearbyRT.sizeDelta        = new Vector2(-32, 0);   // height driven by CSF

        GridLayoutGroup gridNLG = gridNearby.AddComponent<GridLayoutGroup>();
        gridNLG.cellSize        = new Vector2(72, 72);
        gridNLG.spacing         = new Vector2(8, 8);
        gridNLG.padding         = new RectOffset(8, 8, 8, 8);
        gridNLG.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        gridNLG.startAxis       = GridLayoutGroup.Axis.Horizontal;
        gridNLG.childAlignment  = TextAnchor.UpperLeft;
        gridNLG.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridNLG.constraintCount = 6;

        ContentSizeFitter nearbyCSF = gridNearby.AddComponent<ContentSizeFitter>();
        nearbyCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        nearbyCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // BG behind slots
        GameObject bgNearby = CreateUIImage(gridNearby, "BG_NearbyLoot", ColSectionBg);
        StretchFill(bgNearby.GetComponent<RectTransform>());
        bgNearby.transform.SetAsFirstSibling();

        // 24 visible nearby loot slots (full slot structure)
        for (int i = 0; i < 24; i++)
            CreateInlineSlot(gridNearby, $"Slot_Nearby_{i:D2}", ColSlotBg, 72);

        // ── ContainerSectionRoot  — runtime VLG parent ────────────────────────
        // Placed below Grid_NearbyLoot (approx y=-106 - 4rows*(72+8) = -106-320=-426)
        // Using a fixed offset; runtime code may want a scroll rect here.
        GameObject containerRoot = new GameObject("ContainerSectionRoot");
        containerRoot.transform.SetParent(lootPanel.transform, false);
        RectTransform containerRootRT = containerRoot.AddComponent<RectTransform>();
        containerRootRT.anchorMin        = new Vector2(0f, 1f);
        containerRootRT.anchorMax        = new Vector2(1f, 1f);
        containerRootRT.pivot            = new Vector2(0.5f, 1f);
        containerRootRT.anchoredPosition = new Vector2(0, -440);
        containerRootRT.sizeDelta        = new Vector2(-16, 0);

        VerticalLayoutGroup vlg2 = containerRoot.AddComponent<VerticalLayoutGroup>();
        vlg2.childAlignment         = TextAnchor.UpperLeft;
        vlg2.childControlWidth      = true;
        vlg2.childControlHeight     = false;
        vlg2.childForceExpandWidth  = true;
        vlg2.childForceExpandHeight = false;
        vlg2.spacing = 8;
        vlg2.padding = new RectOffset(0, 0, 0, 8);

        ContentSizeFitter containerCSF = containerRoot.AddComponent<ContentSizeFitter>();
        containerCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        containerCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── UI_DragPreview ────────────────────────────────────────────────────
        GameObject dragPreview = new GameObject("UI_DragPreview");
        dragPreview.transform.SetParent(canvasGO.transform, false);
        RectTransform dpRT = dragPreview.AddComponent<RectTransform>();
        dpRT.sizeDelta = new Vector2(72, 72);
        dpRT.anchorMin = new Vector2(0f, 1f);
        dpRT.anchorMax = new Vector2(0f, 1f);
        dpRT.pivot     = new Vector2(0.5f, 0.5f);
        Image dpImg = dragPreview.AddComponent<Image>();
        dpImg.color         = new Color(1f, 1f, 1f, 0.75f);
        dpImg.raycastTarget = false;
        dragPreview.SetActive(false);

        // ── Wire InventoryLootUIReferences ────────────────────────────────────
        InventoryLootUIReferences refs = canvasGO.AddComponent<InventoryLootUIReferences>();
        refs.UI_PlayerInventoryPanel       = playerPanel;
        refs.UI_AreaLootPanel              = lootPanel;
        refs.UI_InteractionPrompt          = prompt;
        refs.UI_DragPreview                = dragPreview;
        refs.Area_PlayerPreview            = areaPreview;
        refs.Grid_PlayerInventory          = gridPlayer.transform;
        refs.Grid_NearbyLoot               = gridNearby.transform;
        refs.ContainerSectionRoot          = containerRoot.transform;
        refs.SlotPrefab                    = slotPrefabAsset;
        refs.ContainerSectionTypeAPrefab   = containerTypeAPrefabAsset;
        refs.ContainerSectionTypeBPrefab   = containerTypeBPrefabAsset;
        refs.ContainerSectionTypeCPrefab   = containerTypeCPrefabAsset;

        // ── Save canvas prefab ────────────────────────────────────────────────
        SavePrefab(canvasGO, prefabPath);
        GameObject.DestroyImmediate(canvasGO);
    }

    // =========================================================================
    // TEST SCENE
    // =========================================================================
    private static void CreateTestScene()
    {
        // Ensure the Scenes folder exists on disk before SaveScene writes to it.
        string scenesDir = Path.Combine(Application.dataPath,
            "_ProjectNoumenon/UI/Scenes").Replace('\\', '/');
        if (!Directory.Exists(scenesDir))
            Directory.CreateDirectory(scenesDir);

        // Use Additive mode so the generator never silently destroys the user's
        // current work-in-progress scene.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                NewSceneMode.Additive);

        // Configure the default Main Camera already present in DefaultGameObjects
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == "Main Camera")
            {
                Camera cam = go.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags      = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
                }
            }
        }

        // Add EventSystem — create directly in the additive scene
        GameObject esGO = new GameObject("EventSystem");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(esGO, scene);
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();

        // Instantiate the canvas prefab into the scene
        string canvasPrefabPath = PrefabRoot + "/UI_InventoryLootCanvas.prefab";
        GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(canvasPrefabPath);
        if (canvasPrefab != null)
        {
            GameObject canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                canvasPrefab, scene);
            canvasInstance.name = "UI_InventoryLootCanvas";
        }
        else
        {
            Debug.LogWarning("[Noumenon UI] Canvas prefab not found — scene will be saved without canvas.");
        }

        // Mark dirty then save
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
            Debug.LogError($"[Noumenon UI] SaveScene returned false for path: {ScenePath}");

        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.ImportAsset(ScenePath);

        Debug.Log($"[Noumenon UI] Test scene saved → {ScenePath}  (success={saved})");
    }

    // =========================================================================
    // HELPERS — inline slot builder
    // =========================================================================

    /// <summary>
    /// Builds a full-structure slot child inside <paramref name="parent"/>.
    /// Mirrors the slot structure of UI_SlotPrefab without requiring the prefab asset.
    /// This ensures Grid_NearbyLoot and Grid_PlayerInventory show visible slots.
    /// </summary>
    private static GameObject CreateInlineSlot(
        GameObject parent, string slotName, Color bgColor, float size)
    {
        // root
        GameObject root = new GameObject(slotName);
        root.transform.SetParent(parent.transform, false);
        RectTransform rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(size, size);

        BuildSlotChildren(root, bgColor);
        return root;
    }

    /// <summary>
    /// Adds Image_SlotBackground / Image_ItemIcon / Text_Amount /
    /// Image_HoverOutline / Image_SelectedOutline / Image_DragHighlight
    /// to <paramref name="slotRoot"/>.
    /// </summary>
    private static void BuildSlotChildren(GameObject slotRoot, Color bgColor)
    {
        // Image_SlotBackground
        GameObject bg = CreateUIImage(slotRoot, "Image_SlotBackground", bgColor);
        StretchFill(bg.GetComponent<RectTransform>());

        // Image_ItemIcon (transparent placeholder)
        GameObject icon = CreateUIImage(slotRoot, "Image_ItemIcon", ColSlotIcon);
        RectTransform iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;

        // Text_Amount
        GameObject amtGO = new GameObject("Text_Amount");
        amtGO.transform.SetParent(slotRoot.transform, false);
        TMP_Text amt = amtGO.AddComponent<TextMeshProUGUI>();
        amt.text      = "";
        amt.fontSize  = 12;
        amt.color     = Color.white;
        amt.alignment = TextAlignmentOptions.BottomRight;
        RectTransform amtRT = amtGO.GetComponent<RectTransform>();
        amtRT.anchorMin = Vector2.zero;
        amtRT.anchorMax = Vector2.one;
        amtRT.offsetMin = new Vector2(2, 2);
        amtRT.offsetMax = new Vector2(-2, -2);

        // Image_HoverOutline
        GameObject hover = CreateUIImage(slotRoot, "Image_HoverOutline",
            new Color(1f, 1f, 1f, 0.6f));
        hover.GetComponent<Image>().type = Image.Type.Sliced;
        StretchFill(hover.GetComponent<RectTransform>());
        hover.SetActive(false);

        // Image_SelectedOutline
        GameObject selected = CreateUIImage(slotRoot, "Image_SelectedOutline",
            new Color(0.97f, 0.78f, 0.28f, 0.8f));
        selected.GetComponent<Image>().type = Image.Type.Sliced;
        StretchFill(selected.GetComponent<RectTransform>());
        selected.SetActive(false);

        // Image_DragHighlight
        GameObject drag = CreateUIImage(slotRoot, "Image_DragHighlight",
            new Color(0.4f, 0.85f, 1f, 0.4f));
        StretchFill(drag.GetComponent<RectTransform>());
        drag.SetActive(false);
    }

    /// <summary>Wire a UISlotView component from child names on slotRoot.</summary>
    private static void WireSlotView(UISlotView slotView, GameObject slotRoot)
    {
        slotView.Image_SlotBackground  = slotRoot.transform.Find("Image_SlotBackground")
                                                 ?.GetComponent<Image>();
        slotView.Image_ItemIcon        = slotRoot.transform.Find("Image_ItemIcon")
                                                 ?.GetComponent<Image>();
        GameObject amtGO               = slotRoot.transform.Find("Text_Amount")?.gameObject;
        if (amtGO != null)
            slotView.Text_Amount       = amtGO.GetComponent<TMP_Text>();
        slotView.Image_HoverOutline    = slotRoot.transform.Find("Image_HoverOutline")
                                                 ?.gameObject;
        slotView.Image_SelectedOutline = slotRoot.transform.Find("Image_SelectedOutline")
                                                 ?.gameObject;
        slotView.Image_DragHighlight   = slotRoot.transform.Find("Image_DragHighlight")
                                                 ?.gameObject;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Save a temporary game object as a prefab asset (overwrite-safe).</summary>
    private static GameObject SavePrefab(GameObject go, string path)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        return PrefabUtility.SaveAsPrefabAsset(go, path);
    }

    /// <summary>Create a RectTransform panel with an Image component.</summary>
    private static GameObject CreatePanel(
        GameObject parent,
        string goName,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPos)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = ColPanelBg;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = pivot;
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;
        return go;
    }

    /// <summary>Create a child Image game object.</summary>
    private static GameObject CreateUIImage(GameObject parent, string goName, Color color)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        // AddComponent<Image> automatically adds RectTransform as a dependency
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    /// <summary>Create a TMP label with explicit anchor/pivot/offset control.</summary>
    private static void CreateTMPLabel(
        GameObject parent,
        string goName,
        string defaultText,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        TMP_Text label = go.AddComponent<TextMeshProUGUI>();
        label.text      = defaultText;
        label.fontSize  = fontSize;
        label.color     = ColTitleText;
        label.alignment = alignment;
        label.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    /// <summary>Make a RectTransform fill its parent completely.</summary>
    private static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
