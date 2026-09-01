using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 완전 자급자족 드래그&드롭 인벤토리 테스트.
/// 빈 씬에 빈 GameObject 하나 만들고 이 스크립트만 붙이면 전부 자동 생성됩니다.
/// </summary>
public class DragDropTest : MonoBehaviour
{
    // ── 설정 ────────────────────────────────────────────
    private const int   INV_COLS   = 6;
    private const int   INV_ROWS   = 5;  // 30칸
    private const float CELL       = 72f;
    private const float GAP        = 6f;
    private const float PAD        = 10f;

    // ── 런타임 ─────────────────────────────────────────
    private Canvas      canvas;
    private GameObject  dragPreview;
    private Image       dragPreviewImg;
    private RectTransform dragPreviewRT;

    private RectTransform invGridRT;   // 플레이어 인벤토리 그리드
    private RectTransform lootGridRT;  // Nearby Loot 그리드

    // 인벤토리 데이터 (null = 빈 칸)
    private string[,] invData = new string[INV_COLS, INV_ROWS];

    // 현재 드래그 중인 슬롯 정보
    private DragSlot activeDrag = null;

    // ────────────────────────────────────────────────────
    class DragSlot
    {
        public string  itemName;
        public Color   itemColor;
        public int     w, h;           // 아이템 칸 수
        public int     srcCol, srcRow; // -1,-1 이면 Nearby 출처
        public GameObject slotGO;
        public Image   iconImg;
        // 아이템 안에서 마우스가 잡은 위치 (그리드 셀 단위)
        public int grabOffsetCol;
        public int grabOffsetRow;
    }

    // Nearby 아이템 목록
    class LootItem { public string name; public Color color; public int w, h; }
    private List<LootItem> lootItems = new List<LootItem>
    {
        new LootItem { name="Pistol",  color=new Color(.9f,.6f,.2f), w=1, h=2 },
        new LootItem { name="MedKit",  color=new Color(.2f,.8f,.3f), w=2, h=1 },
        new LootItem { name="Ammo",    color=new Color(.8f,.8f,.2f), w=1, h=1 },
        new LootItem { name="Rifle",   color=new Color(.6f,.3f,.9f), w=1, h=3 },
        new LootItem { name="Food",    color=new Color(.9f,.4f,.4f), w=1, h=1 },
        new LootItem { name="Armor",   color=new Color(.3f,.6f,.9f), w=2, h=2 },
    };

    // ────────────────────────────────────────────────────
    void Start() => BuildUI();

    void BuildUI()
    {
        // ── EventSystem ──────────────────────────────────
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // ── Canvas ───────────────────────────────────────
        var canvasGO = new GameObject("Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 배경 ─────────────────────────────────────────
        var bg = MakeImage(canvasGO, "BG", new Color(.08f,.10f,.14f,1f));
        Stretch(bg.GetComponent<RectTransform>());

        // ── 왼쪽: 플레이어 인벤토리 패널 ─────────────────
        var leftPanel = MakePanel(canvasGO, "InvPanel",
            new Vector2(550, 700), new Vector2(.5f,.5f), new Vector2(-400, 0));
        MakeLabel(leftPanel, "INVENTORY", 24, new Vector2(0,1), new Vector2(1,1),
                  new Vector2(0,-40), new Vector2(0,-10));

        var invGrid = new GameObject("InvGrid");
        invGrid.transform.SetParent(leftPanel.transform, false);
        invGridRT = invGrid.AddComponent<RectTransform>();
        invGridRT.anchorMin        = new Vector2(0,1);
        invGridRT.anchorMax        = new Vector2(0,1);
        invGridRT.pivot            = new Vector2(0,1);
        invGridRT.anchoredPosition = new Vector2(PAD, -50);
        invGridRT.sizeDelta        = new Vector2(INV_COLS*(CELL+GAP)-GAP, INV_ROWS*(CELL+GAP)-GAP);

        // 배경 칸 (UpperLeft 기준)
        for (int r = 0; r < INV_ROWS; r++)
        {
            for (int c = 0; c < INV_COLS; c++)
            {
                var cell = MakeImage(invGrid, $"Cell_{c}_{r}", new Color(.14f,.18f,.24f,1f));
                var rt   = cell.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0,1);
                rt.anchorMax = new Vector2(0,1);
                rt.pivot     = new Vector2(0,1);
                rt.sizeDelta = new Vector2(CELL, CELL);
                rt.anchoredPosition = new Vector2(c*(CELL+GAP), -r*(CELL+GAP));
            }
        }
        invGridRT = invGrid.GetComponent<RectTransform>(); // 재확인

        // ── 오른쪽: Nearby Loot 패널 ─────────────────────
        var rightPanel = MakePanel(canvasGO, "LootPanel",
            new Vector2(400, 700), new Vector2(.5f,.5f), new Vector2(400, 0));
        MakeLabel(rightPanel, "NEARBY LOOT", 24, new Vector2(0,1), new Vector2(1,1),
                  new Vector2(0,-40), new Vector2(0,-10));

        var lootGrid = new GameObject("LootGrid");
        lootGrid.transform.SetParent(rightPanel.transform, false);
        lootGridRT = lootGrid.AddComponent<RectTransform>();
        lootGridRT.anchorMin        = new Vector2(0,1);
        lootGridRT.anchorMax        = new Vector2(0,1);
        lootGridRT.pivot            = new Vector2(0,1);
        lootGridRT.anchoredPosition = new Vector2(PAD, -55);
        lootGridRT.sizeDelta        = new Vector2(360, 600);

        // Loot 아이템 슬롯 생성
        float ly = 0;
        foreach (var item in lootItems)
        {
            float slotW = item.w * CELL + (item.w-1)*GAP;
            float slotH = item.h * CELL + (item.h-1)*GAP;

            var slotGO = MakeImage(lootGrid, $"Loot_{item.name}",
                new Color(item.color.r*.4f, item.color.g*.4f, item.color.b*.4f, 1f));
            var slotRT  = slotGO.GetComponent<RectTransform>();
            slotRT.anchorMin        = new Vector2(0,1);
            slotRT.anchorMax        = new Vector2(0,1);
            slotRT.pivot            = new Vector2(0,1);
            slotRT.sizeDelta        = new Vector2(slotW, slotH);
            slotRT.anchoredPosition = new Vector2(0, -ly);

            // 색상 블록 (아이콘 대용)
            var colorBlock = MakeImage(slotGO, "Color", item.color);
            var cbRT = colorBlock.GetComponent<RectTransform>();
            cbRT.anchorMin = new Vector2(.05f,.05f);
            cbRT.anchorMax = new Vector2(.95f,.95f);
            cbRT.offsetMin = Vector2.zero;
            cbRT.offsetMax = Vector2.zero;

            // 이름 라벨
            MakeLabel(slotGO, item.name, 14,
                      new Vector2(0,0), new Vector2(1,1),
                      new Vector2(4,4), new Vector2(-4,-4));

            // 드래그 이벤트 연결
            var trigger = slotGO.AddComponent<EventTrigger>();
            var lootRef = new LootItem
                { name=item.name, color=item.color, w=item.w, h=item.h };
            var slotImgRef = colorBlock.GetComponent<Image>();
            AddEventTrigger(trigger, EventTriggerType.BeginDrag,
                (data) => OnLootBeginDrag((PointerEventData)data, lootRef, slotGO, slotImgRef));
            AddEventTrigger(trigger, EventTriggerType.Drag,
                (data) => OnDrag((PointerEventData)data));
            AddEventTrigger(trigger, EventTriggerType.EndDrag,
                (data) => OnLootEndDrag((PointerEventData)data));

            ly += slotH + GAP;
        }

        // ── 드래그 프리뷰 ────────────────────────────────
        dragPreview = new GameObject("DragPreview");
        dragPreview.transform.SetParent(canvasGO.transform, false);
        dragPreviewImg = dragPreview.AddComponent<Image>();
        dragPreviewImg.raycastTarget = false;
        dragPreview.GetComponent<Image>().color = new Color(1,1,1,.8f);
        dragPreviewRT = dragPreview.GetComponent<RectTransform>();
        dragPreviewRT.pivot = new Vector2(.5f,.5f);
        // CanvasGroup으로 Raycast 완전 차단 해제
        var cg = dragPreview.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;
        dragPreview.SetActive(false);

        // ── 안내 텍스트 ──────────────────────────────────
        MakeLabel(canvasGO, "오른쪽 아이템을 왼쪽 인벤토리로 드래그해서 넣어보세요!", 18,
                  new Vector2(.5f,0), new Vector2(.5f,0),
                  new Vector2(-400, 20), new Vector2(400, 50));
    }

    // ────────────────────────────────────────────────────
    // Loot → 드래그 시작
    void OnLootBeginDrag(PointerEventData e, LootItem item, GameObject slotGO, Image iconImg)
    {
        // 아이템의 좌상단 로컬 좌표 계산
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            lootGridRT, e.position, e.pressEventCamera, out Vector2 mouseInGrid);
        var slotRT = slotGO.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            lootGridRT, e.position, e.pressEventCamera, out Vector2 mouseLocal);

        // 슬롯 자체의 anchoredPosition이 좌상단 기준
        Vector2 slotTopLeft = slotRT.anchoredPosition; // anchor=(0,1), pivot=(0,1)
        float grabPixelX = mouseLocal.x - slotTopLeft.x;
        float grabPixelY = -(mouseLocal.y - slotTopLeft.y); // y는 아래로 양수
        int grabCol = Mathf.Clamp(Mathf.FloorToInt(grabPixelX / (CELL + GAP)), 0, item.w - 1);
        int grabRow = Mathf.Clamp(Mathf.FloorToInt(grabPixelY / (CELL + GAP)), 0, item.h - 1);

        activeDrag = new DragSlot
        {
            itemName = item.name,
            itemColor = item.color,
            w = item.w, h = item.h,
            srcCol = -1, srcRow = -1,
            slotGO = slotGO,
            iconImg = iconImg,
            grabOffsetCol = grabCol,
            grabOffsetRow = grabRow
        };

        iconImg.color = new Color(item.color.r, item.color.g, item.color.b, .35f);
        dragPreviewImg.color = new Color(item.color.r, item.color.g, item.color.b, .8f);
        dragPreviewRT.sizeDelta = new Vector2(
            item.w * CELL + (item.w-1)*GAP,
            item.h * CELL + (item.h-1)*GAP);
        dragPreview.SetActive(true);
        dragPreview.transform.SetAsLastSibling();

        // 프리뷰 pivot을 잡은 위치 비율로 설정 → 마우스가 잡은 칸을 정확히 따라감
        float pivotX = (grabCol * (CELL+GAP) + CELL*0.5f) / (item.w * CELL + (item.w-1)*GAP);
        float pivotY = 1f - (grabRow * (CELL+GAP) + CELL*0.5f) / (item.h * CELL + (item.h-1)*GAP);
        dragPreviewRT.pivot = new Vector2(pivotX, pivotY);

        MovePreview(e);
    }

    // 인벤토리 슬롯 → 드래그 시작
    void OnInvBeginDrag(PointerEventData e, string name, Color col, int w, int h,
                        int srcCol, int srcRow, GameObject slotGO, Image iconImg)
    {
        // 그리드 데이터에서 제거
        for (int r = srcRow; r < srcRow+h; r++)
            for (int c = srcCol; c < srcCol+w; c++)
                invData[c, r] = null;

        // 잡은 위치 계산
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            invGridRT, e.position, e.pressEventCamera, out Vector2 mouseLocal);
        // 슬롯 좌상단은 anchoredPosition (anchor=0,1 pivot=0,1 기준)
        float grabPixelX = mouseLocal.x - srcCol * (CELL+GAP);
        float grabPixelY = -(mouseLocal.y + srcRow * (CELL+GAP)); // y 아래로 양수
        int grabCol = Mathf.Clamp(Mathf.FloorToInt(grabPixelX / (CELL+GAP)), 0, w-1);
        int grabRow = Mathf.Clamp(Mathf.FloorToInt(grabPixelY / (CELL+GAP)), 0, h-1);

        activeDrag = new DragSlot
        {
            itemName = name,
            itemColor = col,
            w = w, h = h,
            srcCol = srcCol, srcRow = srcRow,
            slotGO = slotGO,
            iconImg = iconImg,
            grabOffsetCol = grabCol,
            grabOffsetRow = grabRow
        };

        iconImg.color = new Color(col.r, col.g, col.b, .35f);
        dragPreviewImg.color  = new Color(col.r, col.g, col.b, .8f);
        dragPreviewRT.sizeDelta = new Vector2(w*CELL+(w-1)*GAP, h*CELL+(h-1)*GAP);

        float pivotX = (grabCol * (CELL+GAP) + CELL*0.5f) / (w * CELL + (w-1)*GAP);
        float pivotY = 1f - (grabRow * (CELL+GAP) + CELL*0.5f) / (h * CELL + (h-1)*GAP);
        dragPreviewRT.pivot = new Vector2(pivotX, pivotY);

        dragPreview.SetActive(true);
        dragPreview.transform.SetAsLastSibling();
        MovePreview(e);
    }

    void OnDrag(PointerEventData e) => MovePreview(e);

    void OnLootEndDrag(PointerEventData e)
    {
        dragPreview.SetActive(false);
        if (activeDrag == null) return;

        var drag = activeDrag;
        activeDrag = null;

        if (TryDropOnInventory(e, drag))
        {
            // 성공: Loot 슬롯 숨기기
            drag.slotGO.SetActive(false);
        }
        else
        {
            // 실패: 원본 복원
            drag.iconImg.color = drag.itemColor;
        }
    }

    void OnInvEndDrag(PointerEventData e)
    {
        dragPreview.SetActive(false);
        if (activeDrag == null) return;

        var drag = activeDrag;
        activeDrag = null;

        if (TryDropOnInventory(e, drag))
        {
            // 성공: 기존 UI 슬롯 제거 (새로 생성됨)
            Destroy(drag.slotGO);
        }
        else
        {
            // 실패: 원래 위치 복원
            for (int r = drag.srcRow; r < drag.srcRow+drag.h; r++)
                for (int c = drag.srcCol; c < drag.srcCol+drag.w; c++)
                    invData[c,r] = drag.itemName;
            drag.iconImg.color = drag.itemColor;
        }
    }

    // ── 드롭 처리 ────────────────────────────────────────
    bool TryDropOnInventory(PointerEventData e, DragSlot drag)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            invGridRT, e.position, e.pressEventCamera, out Vector2 lp);

        // 그리드 전체 픽셀 크기
        float gridW = INV_COLS * CELL + (INV_COLS - 1) * GAP;
        float gridH = INV_ROWS * CELL + (INV_ROWS - 1) * GAP;

        // 넉넉한 허용 범위: 그리드 바깥 1칸(CELL+GAP) 이상 벗어나면 취소
        float margin = CELL + GAP;
        if (lp.x < -margin || lp.x > gridW + margin ||
            lp.y > margin   || lp.y < -(gridH + margin))
        {
            Debug.Log("[DragDrop] 인벤토리 영역 밖");
            return false;
        }

        // invGridRT anchor=(0,1) pivot=(0,1) → x 오른쪽 양수, y 아래로 음수
        int col = Mathf.RoundToInt(lp.x / (CELL + GAP)) - drag.grabOffsetCol;
        int row = Mathf.RoundToInt(-lp.y / (CELL + GAP)) - drag.grabOffsetRow;

        // 그리드 경계 안으로 클램프 (가장자리 가까이 드롭해도 자동 스냅)
        col = Mathf.Clamp(col, 0, INV_COLS - drag.w);
        row = Mathf.Clamp(row, 0, INV_ROWS - drag.h);


        Debug.Log($"[DragDrop] 드롭 위치 col={col} row={row}  (lp={lp})");

        // 범위 체크
        if (col < 0 || row < 0 || col+drag.w > INV_COLS || row+drag.h > INV_ROWS)
        {
            Debug.Log($"[DragDrop] 범위 초과");
            return false;
        }

        // 겹침 체크
        for (int r = row; r < row+drag.h; r++)
            for (int c = col; c < col+drag.w; c++)
                if (invData[c,r] != null) { Debug.Log($"[DragDrop] [{c},{r}] 이미 차있음: {invData[c,r]}"); return false; }

        // 배치
        for (int r = row; r < row+drag.h; r++)
            for (int c = col; c < col+drag.w; c++)
                invData[c,r] = drag.itemName;

        SpawnInvSlot(drag, col, row);
        Debug.Log($"[DragDrop] {drag.itemName} → 인벤토리 ({col},{row}) 성공!");
        return true;
    }

    void SpawnInvSlot(DragSlot drag, int col, int row)
    {
        float slotW = drag.w * CELL + (drag.w-1)*GAP;
        float slotH = drag.h * CELL + (drag.h-1)*GAP;

        var go = MakeImage(invGridRT.gameObject, $"InvItem_{drag.itemName}",
                           new Color(drag.itemColor.r*.4f, drag.itemColor.g*.4f, drag.itemColor.b*.4f, 1f));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0,1);
        rt.anchorMax        = new Vector2(0,1);
        rt.pivot            = new Vector2(0,1);
        rt.sizeDelta        = new Vector2(slotW, slotH);
        rt.anchoredPosition = new Vector2(col*(CELL+GAP), -row*(CELL+GAP));

        var colorBlock = MakeImage(go, "Color", drag.itemColor);
        var cbRT = colorBlock.GetComponent<RectTransform>();
        cbRT.anchorMin = new Vector2(.05f,.05f);
        cbRT.anchorMax = new Vector2(.95f,.95f);
        cbRT.offsetMin = Vector2.zero;
        cbRT.offsetMax = Vector2.zero;

        MakeLabel(go, drag.itemName, 12,
                  new Vector2(0,0), new Vector2(1,1),
                  new Vector2(4,4), new Vector2(-4,-4));

        // 인벤토리 내 재이동 지원
        var trigger = go.AddComponent<EventTrigger>();
        var imgRef  = colorBlock.GetComponent<Image>();
        int capCol=col, capRow=row;
        AddEventTrigger(trigger, EventTriggerType.BeginDrag,
            (data) => OnInvBeginDrag((PointerEventData)data,
                drag.itemName, drag.itemColor, drag.w, drag.h, capCol, capRow, go, imgRef));
        AddEventTrigger(trigger, EventTriggerType.Drag,
            (data) => OnDrag((PointerEventData)data));
        AddEventTrigger(trigger, EventTriggerType.EndDrag,
            (data) => OnInvEndDrag((PointerEventData)data));
    }

    // ── 유틸 ─────────────────────────────────────────────
    void MovePreview(PointerEventData e)
    {
        var canvasRT = canvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, e.position, e.pressEventCamera, out Vector2 lp);
        dragPreviewRT.anchoredPosition = lp;
    }

    static GameObject MakeImage(GameObject parent, string name, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static GameObject MakePanel(GameObject parent, string name, Vector2 size,
                                 Vector2 anchor, Vector2 apos)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(.11f,.14f,.20f,1f);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot     = new Vector2(.5f,.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = apos;
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void MakeLabel(GameObject parent, string text, float fs,
                           Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(parent.transform, false);
        var t  = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = fs;
        t.color     = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = oMin; rt.offsetMax = oMax;
    }

    static void AddEventTrigger(EventTrigger trigger, EventTriggerType type,
                                 UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }
}
