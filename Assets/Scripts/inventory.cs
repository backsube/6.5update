using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class inventory : MonoBehaviour
{
    public static inventory Instance { get; private set; }

    [Header("인벤토리 UI 연결")]
    [Tooltip("씬에 생성된 UI_InventoryLootCanvas의 InventoryLootUIReferences를 연결하세요.")]
    public InventoryLootUIReferences uiRefs;

    [Header("스캔 설정")]
    [Tooltip("플레이어 Transform을 넣으세요. 비워두면 이 스크립트가 붙은 오브젝트 기준으로 스캔합니다.")]
    public Transform scanCenter;

    [Tooltip("주변 루팅 가능 아이템 탐색 반경")]
    public float scanRadius = 3f;

    [Tooltip("아이템 오브젝트 또는 Collider가 속한 레이어")]
    public LayerMask itemLayer;

    [Tooltip("인벤토리가 열려 있는 동안 주변 아이템을 몇 초마다 검사할지 결정합니다. 0이면 매 프레임 검사합니다.")]
    [Min(0f)]
    public float scanInterval = 0.1f;

    [Tooltip("한 번에 감지할 수 있는 최대 Collider 수")]
    [Min(1)]
    public int maxScanResults = 64;

    [Header("디버그")]
    public bool debugLogs = false;
    public bool drawGizmos = true;

    public bool isInventoryOpen = false;
    public bool ignoreTabInput = false;

    // =========================================================================
    // 그리드 설정 (Nearby Loot)
    // =========================================================================
    private int   NEARBY_GRID_WIDTH = 4;
    private float NEARBY_CELL_X     = 60f;
    private float NEARBY_CELL_Y     = 60f;
    private float NEARBY_SPACE_X    = 2f;
    private float NEARBY_SPACE_Y    = 2f;
    private float NEARBY_PAD_L      = 0f;
    private float NEARBY_PAD_T      = 0f;

    // =========================================================================
    // 그리드 설정 (Player Inventory)
    // =========================================================================
    private int   PLAYER_GRID_WIDTH  = 8;
    private int   PLAYER_GRID_HEIGHT = 6;
    private float PLAYER_CELL_X      = 60f;
    private float PLAYER_CELL_Y      = 60f;
    private float PLAYER_SPACE_X     = 2f;
    private float PLAYER_SPACE_Y     = 2f;
    private float PLAYER_PAD_L       = 0f;
    private float PLAYER_PAD_T       = 0f;
    // LowerLeft/LowerRight로 채워지는 기존 UI에 대응
    private bool  PLAYER_GRID_FILLS_FROM_BOTTOM = false;

    private ItemData[,] playerGrid;

    // =========================================================================
    // 그리드 설정 (Container)
    // =========================================================================
    private box_defualt currentBox;
    private int CONTAINER_GRID_WIDTH = 9;
    private int CONTAINER_GRID_HEIGHT = 2;
    private ItemData[,] containerGrid;
    private Transform currentContainerGridRoot;
    private float CONTAINER_CELL_X = 72f;
    private float CONTAINER_CELL_Y = 72f;
    private float CONTAINER_SPACE_X = 6f;
    private float CONTAINER_SPACE_Y = 6f;
    // =========================================================================
    // 런타임 슬롯 목록
    // =========================================================================
    private readonly List<UISlotView> spawnedNearbySlots  = new List<UISlotView>();
    private readonly List<UISlotView> spawnedPlayerSlots  = new List<UISlotView>();
    private readonly List<UISlotView> spawnedContainerSlots = new List<UISlotView>();

    // =========================================================================
    // 스캔 관련
    // =========================================================================
    private float nextScanTime = 0f;
    private Collider[] scanBuffer;
    private readonly List<item_defualt> currentNearbyItems  = new List<item_defualt>();
    private readonly List<item_defualt> previousNearbyItems = new List<item_defualt>();
    private readonly HashSet<item_defualt> uniqueItemSet    = new HashSet<item_defualt>();

    // =========================================================================
    // 드래그 상태
    // =========================================================================
    private UISlotView draggingSlot  = null;
    private int        grabOffsetCol = 0;  // 아이템 안에서 잡은 열 위치
    private int        grabOffsetRow = 0;  // 아이템 안에서 잡은 행 위치

    // =========================================================================
    // Unity 생명주기
    // =========================================================================
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        ConfigureUnity6EventSystem();
        scanBuffer = new Collider[maxScanResults];
    }

    private void ConfigureUnity6EventSystem()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
            legacyModule.enabled = false;

        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        inputSystemModule.AssignDefaultActions();
        inputSystemModule.enabled = true;
    }

    void Start()
    {
        InitializeUI();
        ReadGridSettings();
        playerGrid = new ItemData[PLAYER_GRID_WIDTH, PLAYER_GRID_HEIGHT];
    }

    void Update()
    {
        HandleInput();

        if (!isInventoryOpen) return;

        if (ShouldScanNow())
            RefreshNearbyLoot();
    }

    // =========================================================================
    // 입력 처리
    // =========================================================================
    private void HandleInput()
    {
        if (ignoreTabInput) return;
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleInventory();
    }

    private bool ShouldScanNow()
    {
        if (scanInterval <= 0f) return true;
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            return true;
        }
        return false;
    }

    // =========================================================================
    // 인벤토리 열기 / 닫기
    // =========================================================================
    public void ForceOpenInventory()
    {
        isInventoryOpen = true;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        if (uiRefs == null) return;
        SetPanelActive(uiRefs.UI_PlayerInventoryPanel, true);
        ForceRefreshNearbyLoot();
    }

    public void ForceCloseInventory()
    {
        isInventoryOpen  = false;
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (uiRefs == null) return;
        SetPanelActive(uiRefs.UI_PlayerInventoryPanel, false);
        SetPanelActive(uiRefs.UI_AreaLootPanel, false);
        ClearNearbySlots();
        previousNearbyItems.Clear();
    }

    private void ToggleInventory()
    {
        if (isInventoryOpen) ForceCloseInventory();
        else                 ForceOpenInventory();
    }

    // =========================================================================
    // UI 초기화 & GridLayoutGroup 설정 읽기
    // =========================================================================
    private void InitializeUI()
    {
        if (uiRefs == null)
        {
            Debug.LogError("[inventory] uiRefs가 연결되지 않았습니다.");
            return;
        }
        uiRefs.gameObject.SetActive(true);
        DisableBackpackTitleRaycast();
        SetPanelActive(uiRefs.UI_PlayerInventoryPanel, false);
        SetPanelActive(uiRefs.UI_AreaLootPanel, false);
        if (uiRefs.UI_DragPreview != null)
            uiRefs.UI_DragPreview.SetActive(false);
    }

    private void DisableBackpackTitleRaycast()
    {
        if (uiRefs == null || uiRefs.UI_PlayerInventoryPanel == null) return;

        foreach (TMP_Text text in uiRefs.UI_PlayerInventoryPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "Text_BackpackTitle" || text.text == "BACKPACK")
            {
                text.raycastTarget = false;
                CanvasGroup canvasGroup = text.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = text.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }
    }

    /// <summary>GridLayoutGroup에서 셀 크기·패딩·칸 수를 한 번만 읽어둡니다.</summary>
    private void ReadGridSettings()
    {
        if (uiRefs == null) return;

        // ── Nearby Loot 그리드 ──
        if (uiRefs.Grid_NearbyLoot != null)
        {
            var lg = uiRefs.Grid_NearbyLoot.GetComponent<GridLayoutGroup>();
            if (lg != null)
            {
                NEARBY_CELL_X  = lg.cellSize.x;
                NEARBY_CELL_Y  = lg.cellSize.y;
                NEARBY_SPACE_X = lg.spacing.x;
                NEARBY_SPACE_Y = lg.spacing.y;
                NEARBY_PAD_L   = lg.padding.left;
                NEARBY_PAD_T   = lg.padding.top;
                if (lg.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                    NEARBY_GRID_WIDTH = lg.constraintCount;
            }
        }

        // ── Player Inventory 그리드 ──
        if (uiRefs.Grid_PlayerInventory != null)
        {
            var lg = uiRefs.Grid_PlayerInventory.GetComponent<GridLayoutGroup>();
            if (lg != null)
            {
                PLAYER_CELL_X  = lg.cellSize.x;
                PLAYER_CELL_Y  = lg.cellSize.y;
                PLAYER_SPACE_X = lg.spacing.x;
                PLAYER_SPACE_Y = lg.spacing.y;
                PLAYER_PAD_L   = lg.padding.left;
                PLAYER_PAD_T   = lg.padding.top;
                if (lg.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                {
                    PLAYER_GRID_WIDTH  = lg.constraintCount;
                    if (PLAYER_GRID_WIDTH <= 0) PLAYER_GRID_WIDTH = 8;
                    PLAYER_GRID_HEIGHT = 48 / PLAYER_GRID_WIDTH;
                }
                // 기존 UI가 아래에서 위로 채워지는 방식인지 잠시 기록
                PLAYER_GRID_FILLS_FROM_BOTTOM =
                    (lg.startCorner == GridLayoutGroup.Corner.LowerLeft ||
                     lg.startCorner == GridLayoutGroup.Corner.LowerRight);
            }
        }
        Debug.Log($"[inventory] 그리드 설정 완료: {PLAYER_GRID_WIDTH}x{PLAYER_GRID_HEIGHT}, 아래에서위로={PLAYER_GRID_FILLS_FROM_BOTTOM}, 셀크기={PLAYER_CELL_X}x{PLAYER_CELL_Y}");
    }

    // =========================================================================
    // Nearby Loot 스캔 & 그리기
    // =========================================================================
    public void ForceRefreshNearbyLoot()
    {
        nextScanTime = Time.time; // 즉시 스캔
        RefreshNearbyLoot(forceRedraw: true);
    }

    public void RefreshNearbyLoot(bool forceRedraw = false)
    {
        if (uiRefs == null || !isInventoryOpen) return;
        
        if (itemLayer.value == 0)
        {
            SetPanelActive(uiRefs.UI_AreaLootPanel, false);
            return;
        }

        ScanNearbyItems();

        bool hasLoot         = currentNearbyItems.Count > 0;
        bool listChanged     = forceRedraw || HasNearbyItemListChanged();

        SetPanelActive(uiRefs.UI_AreaLootPanel, hasLoot);

        if (listChanged)
        {
            DrawNearbyLootSlots();
            SaveCurrentListAsPrevious();
        }
    }

    private void ScanNearbyItems()
    {
        currentNearbyItems.Clear();
        uniqueItemSet.Clear();

        Vector3 center = scanCenter != null ? scanCenter.position : transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(center, scanRadius, scanBuffer, itemLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            if (scanBuffer[i] == null) continue;
            var item = scanBuffer[i].GetComponentInParent<item_defualt>();
            if (item == null || item.itemData == null) continue;
            if (uniqueItemSet.Add(item)) currentNearbyItems.Add(item);
        }

        currentNearbyItems.Sort((a, b) => a.GetEntityId().CompareTo(b.GetEntityId()));
    }

    private bool HasNearbyItemListChanged()
    {
        if (currentNearbyItems.Count != previousNearbyItems.Count) return true;
        for (int i = 0; i < currentNearbyItems.Count; i++)
            if (currentNearbyItems[i] != previousNearbyItems[i]) return true;
        return false;
    }

    private void SaveCurrentListAsPrevious()
    {
        previousNearbyItems.Clear();
        previousNearbyItems.AddRange(currentNearbyItems);
    }

    // =========================================================================
    // Nearby Loot 슬롯 생성 (2D 팩킹)
    // =========================================================================
    private void DrawNearbyLootSlots()
    {
        ClearNearbySlots();

        if (uiRefs.SlotPrefab == null)
        {
            Debug.LogError("[inventory] SlotPrefab이 연결되지 않았습니다.");
            return;
        }

        bool[,] grid = new bool[NEARBY_GRID_WIDTH, 100];

        foreach (var item in currentNearbyItems)
        {
            if (item == null || item.itemData == null) continue;
            int iW = Mathf.Clamp(item.itemData.width,  1, NEARBY_GRID_WIDTH);
            int iH = Mathf.Max(1, item.itemData.height);

            bool placed = false;
            for (int y = 0; y < 99 - iH && !placed; y++)
            {
                for (int x = 0; x <= NEARBY_GRID_WIDTH - iW && !placed; x++)
                {
                    if (CanFit(grid, x, y, iW, iH))
                    {
                        Fill(grid, x, y, iW, iH);
                        SpawnNearbySlot(item, x, y, iW, iH);
                        placed = true;
                    }
                }
            }
        }
    }

    private bool CanFit(bool[,] g, int sx, int sy, int w, int h)
    {
        for (int y = sy; y < sy + h; y++)
            for (int x = sx; x < sx + w; x++)
                if (g[x, y]) return false;
        return true;
    }

    private void Fill(bool[,] g, int sx, int sy, int w, int h)
    {
        for (int y = sy; y < sy + h; y++)
            for (int x = sx; x < sx + w; x++)
                g[x, y] = true;
    }

    private void SpawnNearbySlot(item_defualt item, int gx, int gy, int w, int h)
    {
        var go = Instantiate(uiRefs.SlotPrefab, uiRefs.Grid_NearbyLoot);

        // GridLayoutGroup이 있어도 수동 배치 사용
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var sv = go.GetComponent<UISlotView>();
        if (sv == null) return;

        sv.currentItemData = item.itemData;
        sv.sourceItem      = item;
        sv.gridX           = -1; // Nearby는 플레이어 그리드 좌표 없음
        sv.gridY           = -1;
        ApplySlotVisuals(sv, item.itemData);
        spawnedNearbySlots.Add(sv);

        // RectTransform 설정
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(
            w * NEARBY_CELL_X + (w - 1) * NEARBY_SPACE_X,
            h * NEARBY_CELL_Y + (h - 1) * NEARBY_SPACE_Y);
        rt.anchoredPosition = new Vector2(
             NEARBY_PAD_L + gx * (NEARBY_CELL_X + NEARBY_SPACE_X),
            -(NEARBY_PAD_T + gy * (NEARBY_CELL_Y + NEARBY_SPACE_Y)));
    }

    private void ClearNearbySlots()
    {
        foreach (var sv in spawnedNearbySlots)
            if (sv != null) Destroy(sv.gameObject);
        spawnedNearbySlots.Clear();
    }

    // =========================================================================
    // Player Inventory 슬롯 생성
    // =========================================================================
    private void SpawnPlayerSlot(ItemData data, int gx, int gy)
    {
        var go = Instantiate(uiRefs.SlotPrefab, uiRefs.Grid_PlayerInventory);

        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var sv = go.GetComponent<UISlotView>();
        if (sv == null) return;

        sv.currentItemData = data;
        sv.sourceItem      = null;
        sv.gridX           = gx;
        sv.gridY           = gy;
        ApplySlotVisuals(sv, data);
        spawnedPlayerSlots.Add(sv);

        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(
            w * PLAYER_CELL_X + (w - 1) * PLAYER_SPACE_X,
            h * PLAYER_CELL_Y + (h - 1) * PLAYER_SPACE_Y);
        SetSlotAnchoredPos(rt, gx, gy);
    }

    /// <summary>플레이어 인벤토리 그리드 좌표 → anchoredPosition 변환
    /// (기존 UI가 LowerLeft로 시작하는 경우 row를 뒤집어서 적용)</summary>
    private void SetSlotAnchoredPos(RectTransform rt, int gx, int gy)
    {
        UISlotView sv = rt.GetComponent<UISlotView>();
        int itemH = (sv != null && sv.currentItemData != null) ? Mathf.Max(1, sv.currentItemData.height) : 1;

        // fills_from_bottom이면 gy=0 슬롯이 맨 아래(visualRow 5)에 위치함.
        // 아이템이 여러 칸(itemH > 1)일 경우 위쪽(visualRow가 작은 쪽)으로 확장되므로 최상단 행을 기준으로 앵커를 잡음.
        int visualRow = PLAYER_GRID_FILLS_FROM_BOTTOM ? (PLAYER_GRID_HEIGHT - gy - itemH) : gy;
        
        rt.anchoredPosition = new Vector2(
             PLAYER_PAD_L + gx  * (PLAYER_CELL_X + PLAYER_SPACE_X),
            -(PLAYER_PAD_T + visualRow * (PLAYER_CELL_Y + PLAYER_SPACE_Y)));
    }

    // =========================================================================
    // Container Inventory 로직
    // =========================================================================
    public void OpenContainer(box_defualt box, GameObject spawnedUI)
    {
        currentBox = box;
        
        // Container 크기 설정
        int slotCount = box.containerType == ContainerSectionType.TypeA ? 10 :
                        box.containerType == ContainerSectionType.TypeB ? 20 : 40;
        CONTAINER_GRID_HEIGHT = Mathf.CeilToInt((float)slotCount / CONTAINER_GRID_WIDTH);
        containerGrid = new ItemData[CONTAINER_GRID_WIDTH, CONTAINER_GRID_HEIGHT];
        spawnedContainerSlots.Clear();

        // spawnedUI에서 Grid_ContainerSlots 찾기
        Transform[] children = spawnedUI.GetComponentsInChildren<Transform>(true);
        foreach (var t in children)
        {
            if (t.name == "Grid_ContainerSlots")
            {
                currentContainerGridRoot = t;
                break;
            }
        }

        if (currentContainerGridRoot == null) return;

        // 아이템 자동 배치
        foreach (var item in box.containerItems)
        {
            if (item != null) TryPlaceContainer(item);
        }
    }

    public void CloseContainer(box_defualt box)
    {
        if (currentBox != box) return;
        currentBox = null;
        containerGrid = null;
        currentContainerGridRoot = null;
        spawnedContainerSlots.Clear();
    }

    private bool TryPlaceContainer(ItemData data)
    {
        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);
        
        for (int y = 0; y <= CONTAINER_GRID_HEIGHT - h; y++)
        {
            for (int x = 0; x <= CONTAINER_GRID_WIDTH - w; x++)
            {
                if (CanFitContainer(x, y, w, h))
                {
                    FillContainer(data, x, y, w, h);
                    SpawnContainerSlot(data, x, y);
                    return true;
                }
            }
        }
        return false;
    }

    private bool CanFitContainer(int sx, int sy, int w, int h)
    {
        for (int y = sy; y < sy + h; y++)
            for (int x = sx; x < sx + w; x++)
                if (containerGrid[x, y] != null) return false;
        return true;
    }

    private void FillContainer(ItemData data, int sx, int sy, int w, int h)
    {
        for (int y = sy; y < sy + h; y++)
            for (int x = sx; x < sx + w; x++)
                containerGrid[x, y] = data;
    }

    private void RemoveFromContainerGrid(ItemData data, int sx, int sy)
    {
        if (data == null || containerGrid == null) return;
        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);
        for (int y = sy; y < sy + h && y < CONTAINER_GRID_HEIGHT; y++)
            for (int x = sx; x < sx + w && x < CONTAINER_GRID_WIDTH; x++)
                containerGrid[x, y] = null;
    }

    private void SpawnContainerSlot(ItemData data, int gx, int gy)
    {
        var go = Instantiate(uiRefs.SlotPrefab, currentContainerGridRoot);

        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var sv = go.GetComponent<UISlotView>();
        if (sv == null) return;

        sv.currentItemData = data;
        sv.sourceItem      = null;
        sv.gridX           = gx;
        sv.gridY           = gy;
        // container slot 식별을 위해 gridY에 특수 플래그를 쓸 수는 없으니 isContainer 플래그를 추가하거나, 
        // 부모를 보고 판별. (드래그 시 처리를 위해)
        
        ApplySlotVisuals(sv, data);
        spawnedContainerSlots.Add(sv);

        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(
            w * CONTAINER_CELL_X + (w - 1) * CONTAINER_SPACE_X,
            h * CONTAINER_CELL_Y + (h - 1) * CONTAINER_SPACE_Y);
        
        rt.anchoredPosition = new Vector2(
             gx * (CONTAINER_CELL_X + CONTAINER_SPACE_X),
            -gy * (CONTAINER_CELL_Y + CONTAINER_SPACE_Y));
    }

    // =========================================================================
    // 공통 비주얼
    // =========================================================================
    private void ApplySlotVisuals(UISlotView sv, ItemData data)
    {
        if (sv.Image_ItemIcon != null)
        {
            sv.Image_ItemIcon.sprite = data.icon;
            sv.Image_ItemIcon.color  = data.icon != null ? Color.white : new Color(1,1,1,0);
        }
        if (sv.Text_Amount != null)
            sv.Text_Amount.text = data.itemName;
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }

    // =========================================================================
    // 플레이어 그리드 데이터 조작
    // =========================================================================
    private bool TryPlace(ItemData data, int sx, int sy)
    {
        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);
        if (sx < 0 || sy < 0 || sx + w > PLAYER_GRID_WIDTH || sy + h > PLAYER_GRID_HEIGHT)
            return false;
        for (int y = sy; y < sy + h; y++)
            for (int x = sx; x < sx + w; x++)
                if (playerGrid[x, y] != null) return false;
        for (int y = sy; y < sy + h; y++)
            for (int x = sx; x < sx + w; x++)
                playerGrid[x, y] = data;
        return true;
    }

    private void RemoveFromGrid(ItemData data, int sx, int sy)
    {
        if (data == null) return;
        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);
        for (int y = sy; y < sy + h && y < PLAYER_GRID_HEIGHT; y++)
            for (int x = sx; x < sx + w && x < PLAYER_GRID_WIDTH; x++)
                playerGrid[x, y] = null;
    }

    // =========================================================================
    // 자동 획득 (Auto Place)
    // =========================================================================
    public bool AutoPlace(ItemData data)
    {
        if (data == null) return false;

        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);

        if (PLAYER_GRID_FILLS_FROM_BOTTOM)
        {
            // 시각적으로 위에서 아래로 차례대로 빈 공간을 탐색
            // y가 가장 큰 쪽이 화면 상단이므로 topY를 큰 값에서 줄여나감
            for (int topY = PLAYER_GRID_HEIGHT - 1; topY >= h - 1; topY--)
            {
                int gy = topY - h + 1; // 아이템의 배치 원점(데이터 상의 Bottom-Left)
                for (int gx = 0; gx <= PLAYER_GRID_WIDTH - w; gx++)
                {
                    if (TryPlace(data, gx, gy))
                    {
                        SpawnPlayerSlot(data, gx, gy);
                        return true;
                    }
                }
            }
        }
        else
        {
            // 일반적인 좌상단 기준 탑다운 탐색
            for (int gy = 0; gy <= PLAYER_GRID_HEIGHT - h; gy++)
            {
                for (int gx = 0; gx <= PLAYER_GRID_WIDTH - w; gx++)
                {
                    if (TryPlace(data, gx, gy))
                    {
                        SpawnPlayerSlot(data, gx, gy);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private enum SlotSource { Nearby, Player, Container }

    private SlotSource GetSlotSource(UISlotView slot)
    {
        if (currentContainerGridRoot != null && slot.transform.parent == currentContainerGridRoot)
            return SlotSource.Container;
        if (uiRefs.Grid_PlayerInventory != null && slot.transform.parent == uiRefs.Grid_PlayerInventory)
            return SlotSource.Player;
        return SlotSource.Nearby;
    }

    // =========================================================================
    // Drag & Drop — 슬롯에서 호출됨 (UISlotView → inventory)
    // =========================================================================
    public void OnSlotBeginDrag(UISlotView slot, UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (slot.currentItemData == null) return;
        draggingSlot = slot;

        // 잡은 위치(grab offset) 계산 ─────────────────────────────────────────
        grabOffsetCol = 0;
        grabOffsetRow = 0;
        
        SlotSource source = GetSlotSource(slot);
        Transform refGrid = source == SlotSource.Player ? uiRefs.Grid_PlayerInventory :
                            source == SlotSource.Container ? currentContainerGridRoot : uiRefs.Grid_NearbyLoot;

        if (refGrid != null)
        {
            var refRT = refGrid.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                refRT, eventData.position, eventData.pressEventCamera, out Vector2 mouseLocal);

            var slotRT = slot.GetComponent<RectTransform>();
            Vector2 slotAnchor = slotRT.anchoredPosition;

            float cellW = source == SlotSource.Container ? (CONTAINER_CELL_X + CONTAINER_SPACE_X) :
                          source == SlotSource.Player ? (PLAYER_CELL_X + PLAYER_SPACE_X) : (NEARBY_CELL_X + NEARBY_SPACE_X);
            float cellH = source == SlotSource.Container ? (CONTAINER_CELL_Y + CONTAINER_SPACE_Y) :
                          source == SlotSource.Player ? (PLAYER_CELL_Y + PLAYER_SPACE_Y) : (NEARBY_CELL_Y + NEARBY_SPACE_Y);

            float grabPixX = mouseLocal.x - slotAnchor.x;
            float grabPixY = -(mouseLocal.y - slotAnchor.y);

            int itemW = Mathf.Max(1, slot.currentItemData.width);
            int itemH = Mathf.Max(1, slot.currentItemData.height);
            grabOffsetCol = Mathf.Clamp(Mathf.FloorToInt(grabPixX / cellW), 0, itemW - 1);
            grabOffsetRow = Mathf.Clamp(Mathf.FloorToInt(grabPixY / cellH), 0, itemH - 1);
        }

        // 반투명 처리
        if (slot.Image_ItemIcon != null)
            slot.Image_ItemIcon.color = new Color(1f, 1f, 1f, 0.35f);

        if (uiRefs.UI_DragPreview == null)
        {
            Debug.LogWarning("[inventory] UI_DragPreview가 인스펙터에 연결되지 않았습니다!");
            return;
        }

        uiRefs.UI_DragPreview.SetActive(true);
        uiRefs.UI_DragPreview.transform.SetAsLastSibling();

        var cg = uiRefs.UI_DragPreview.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = uiRefs.UI_DragPreview.AddComponent<CanvasGroup>();
        
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        var img = uiRefs.UI_DragPreview.GetComponent<Image>();
        if (img != null && slot.Image_ItemIcon != null)
        {
            img.sprite = slot.Image_ItemIcon.sprite;
            // 아이콘이 없으면 투명하게 처리, 있으면 원래 보이던 대로 출력
            if (img.sprite == null)
                img.color = new Color(1f, 1f, 1f, 0f);
            else
                img.color = new Color(1f, 1f, 1f, 0.8f);
        }

        var rt     = uiRefs.UI_DragPreview.GetComponent<RectTransform>();
        var slotRt = slot.GetComponent<RectTransform>();
        if (rt != null && slotRt != null)
        {
            rt.sizeDelta = slotRt.sizeDelta;
            
            // Canvas 기준 로컬 좌표(Center 기준)와 맞추기 위해 Anchor를 Center(0.5, 0.5)로 변경
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);

            // 프리뷰 pivot을 잡은 위치 비율로 설정 → 마우스가 잡은 칸이 커서에 딱 붙어서 따라감
            int itemW = Mathf.Max(1, slot.currentItemData.width);
            int itemH = Mathf.Max(1, slot.currentItemData.height);
            float cW = source == SlotSource.Container ? CONTAINER_CELL_X :
                       source == SlotSource.Player ? PLAYER_CELL_X : NEARBY_CELL_X;
            float cH = source == SlotSource.Container ? CONTAINER_CELL_Y :
                       source == SlotSource.Player ? PLAYER_CELL_Y : NEARBY_CELL_Y;
            float sW = source == SlotSource.Container ? CONTAINER_SPACE_X :
                       source == SlotSource.Player ? PLAYER_SPACE_X : NEARBY_SPACE_X;
            float sH = source == SlotSource.Container ? CONTAINER_SPACE_Y :
                       source == SlotSource.Player ? PLAYER_SPACE_Y : NEARBY_SPACE_Y;
            float totalW = itemW * cW + (itemW - 1) * sW;
            float totalH = itemH * cH + (itemH - 1) * sH;

            float pivotX = (grabOffsetCol * (cW + sW) + cW * 0.5f) / totalW;
            float pivotY = 1f - (grabOffsetRow * (cH + sH) + cH * 0.5f) / totalH;
            rt.pivot = new Vector2(pivotX, pivotY);
        }

        MovePreviewToMouse(eventData);
    }

    public void OnSlotDrag(UISlotView slot, UnityEngine.EventSystems.PointerEventData eventData)
    {
        MovePreviewToMouse(eventData);
    }

    public void OnSlotEndDrag(UISlotView slot, UnityEngine.EventSystems.PointerEventData eventData)
    {
        // 아이콘 투명도 복구
        if (slot.Image_ItemIcon != null)
            slot.Image_ItemIcon.color = Color.white;

        // 프리뷰 숨김
        if (uiRefs.UI_DragPreview != null)
            uiRefs.UI_DragPreview.SetActive(false);

        draggingSlot = null;

        HandleDrop(slot, eventData);
    }

    private void MovePreviewToMouse(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (uiRefs.UI_DragPreview == null || !uiRefs.UI_DragPreview.activeSelf) return;
        var rt = uiRefs.UI_DragPreview.GetComponent<RectTransform>();
        if (rt == null || rt.parent == null) return;

        // 부모(Canvas) 기준으로 로컬 좌표를 구해서 바로 localPosition에 적용
        // (Anchor 설정과 무관하게 정확히 마우스 위치로 이동함)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)rt.parent, eventData.position, eventData.pressEventCamera, out Vector2 lp);
        rt.localPosition = lp;
    }

    // =========================================================================
    // 드롭 처리
    // =========================================================================
    private void HandleDrop(UISlotView slot, UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (slot == null || slot.currentItemData == null) return;
        
        SlotSource source = GetSlotSource(slot);
        
        // 드롭 대상이 어디인지 판별 (컨테이너 위인지 플레이어 인벤토리 위인지)
        bool droppedOnContainer = false;
        if (currentContainerGridRoot != null && RectTransformUtility.RectangleContainsScreenPoint(
            currentContainerGridRoot.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera))
        {
            droppedOnContainer = true;
        }

        if (droppedOnContainer)
        {
            HandleDropOnContainer(slot, eventData, source);
        }
        else
        {
            HandleDropOnPlayer(slot, eventData, source);
        }
    }

    private void HandleDropOnPlayer(UISlotView slot, UnityEngine.EventSystems.PointerEventData eventData, SlotSource source)
    {
        if (uiRefs.Grid_PlayerInventory == null) return;
        var gridRT = uiRefs.Grid_PlayerInventory.GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRT, eventData.position, eventData.pressEventCamera, out Vector2 localPos);

        Rect rect    = gridRT.rect;
        float margin = PLAYER_CELL_X + PLAYER_SPACE_X;
        if (localPos.x < rect.xMin - margin || localPos.x > rect.xMax + margin ||
            localPos.y > rect.yMax + margin  || localPos.y < rect.yMin - margin)
        {
            CancelDrop(slot, source);
            return;
        }

        Vector2 topLeft = new Vector2(rect.xMin, rect.yMax);
        float offsetX = localPos.x - topLeft.x;
        float offsetY = topLeft.y  - localPos.y;

        int itemW = Mathf.Max(1, slot.currentItemData.width);
        int itemH = Mathf.Max(1, slot.currentItemData.height);

        int col = Mathf.FloorToInt((offsetX - PLAYER_PAD_L) / (PLAYER_CELL_X + PLAYER_SPACE_X)) - grabOffsetCol;
        int row_from_top = Mathf.FloorToInt((offsetY - PLAYER_PAD_T) / (PLAYER_CELL_Y + PLAYER_SPACE_Y)) - grabOffsetRow;
        int row = row_from_top;
        if (PLAYER_GRID_FILLS_FROM_BOTTOM)
            row = PLAYER_GRID_HEIGHT - row_from_top - itemH;

        if (col < -1 || col > PLAYER_GRID_WIDTH - itemW + 1 ||
            row < -1 || row > PLAYER_GRID_HEIGHT - itemH + 1)
        {
            CancelDrop(slot, source);
            return;
        }

        col = Mathf.Clamp(col, 0, PLAYER_GRID_WIDTH  - itemW);
        row = Mathf.Clamp(row, 0, PLAYER_GRID_HEIGHT - itemH);

        // 이동 시도 전에 기존 위치 비우기
        if (source == SlotSource.Player)
            RemoveFromGrid(slot.currentItemData, slot.gridX, slot.gridY);
        else if (source == SlotSource.Container)
            RemoveFromContainerGrid(slot.currentItemData, slot.gridX, slot.gridY);

        if (TryPlace(slot.currentItemData, col, row))
        {
            if (source == SlotSource.Player)
            {
                slot.gridX = col;
                slot.gridY = row;
                SetSlotAnchoredPos(slot.GetComponent<RectTransform>(), col, row);
            }
            else
            {
                if (source == SlotSource.Nearby && slot.sourceItem != null)
                {
                    Destroy(slot.sourceItem.gameObject);
                    slot.sourceItem = null;
                }
                else if (source == SlotSource.Container && currentBox != null)
                {
                    currentBox.containerItems.Remove(slot.currentItemData);
                    Destroy(slot.gameObject);
                }
                SpawnPlayerSlot(slot.currentItemData, col, row);
                if (source == SlotSource.Nearby) ForceRefreshNearbyLoot();
            }
        }
        else
        {
            CancelDrop(slot, source);
        }
    }

    private void HandleDropOnContainer(UISlotView slot, UnityEngine.EventSystems.PointerEventData eventData, SlotSource source)
    {
        var gridRT = currentContainerGridRoot.GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRT, eventData.position, eventData.pressEventCamera, out Vector2 localPos);

        Rect rect = gridRT.rect;
        Vector2 topLeft = new Vector2(rect.xMin, rect.yMax);
        float offsetX = localPos.x - topLeft.x;
        float offsetY = topLeft.y  - localPos.y;

        int itemW = Mathf.Max(1, slot.currentItemData.width);
        int itemH = Mathf.Max(1, slot.currentItemData.height);

        int col = Mathf.FloorToInt(offsetX / (CONTAINER_CELL_X + CONTAINER_SPACE_X)) - grabOffsetCol;
        int row = Mathf.FloorToInt(offsetY / (CONTAINER_CELL_Y + CONTAINER_SPACE_Y)) - grabOffsetRow;

        if (col < 0 || col > CONTAINER_GRID_WIDTH - itemW ||
            row < 0 || row > CONTAINER_GRID_HEIGHT - itemH)
        {
            CancelDrop(slot, source);
            return;
        }

        if (source == SlotSource.Container)
            RemoveFromContainerGrid(slot.currentItemData, slot.gridX, slot.gridY);
        else if (source == SlotSource.Player)
            RemoveFromGrid(slot.currentItemData, slot.gridX, slot.gridY);

        if (TryPlaceContainerInternal(slot.currentItemData, col, row))
        {
            if (source == SlotSource.Container)
            {
                slot.gridX = col;
                slot.gridY = row;
                var rt = slot.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(
                    col * (CONTAINER_CELL_X + CONTAINER_SPACE_X),
                   -row * (CONTAINER_CELL_Y + CONTAINER_SPACE_Y));
            }
            else
            {
                if (source == SlotSource.Nearby && slot.sourceItem != null)
                {
                    Destroy(slot.sourceItem.gameObject);
                    slot.sourceItem = null;
                }
                else if (source == SlotSource.Player)
                {
                    Destroy(slot.gameObject);
                }
                if (currentBox != null && !currentBox.containerItems.Contains(slot.currentItemData))
                    currentBox.containerItems.Add(slot.currentItemData);
                SpawnContainerSlot(slot.currentItemData, col, row);
                if (source == SlotSource.Nearby) ForceRefreshNearbyLoot();
            }
        }
        else
        {
            CancelDrop(slot, source);
        }
    }

    private bool TryPlaceContainerInternal(ItemData data, int col, int row)
    {
        int w = Mathf.Max(1, data.width);
        int h = Mathf.Max(1, data.height);
        if (CanFitContainer(col, row, w, h))
        {
            FillContainer(data, col, row, w, h);
            return true;
        }
        return false;
    }

    private void CancelDrop(UISlotView slot, SlotSource source)
    {
        if (source == SlotSource.Player)
            TryPlace(slot.currentItemData, slot.gridX, slot.gridY);
        else if (source == SlotSource.Container)
            TryPlaceContainerInternal(slot.currentItemData, slot.gridX, slot.gridY);
    }


    // =========================================================================
    // Gizmos
    // =========================================================================
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Vector3 c = scanCenter != null ? scanCenter.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(c, scanRadius);
    }

    // =========================================================================
    // (구버전 호환) CacheNearbySlots
    // =========================================================================
    public void CacheNearbySlots() { /* 이제 ReadGridSettings()에서 처리 */ }
}
