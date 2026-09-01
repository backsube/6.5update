using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 필드에 배치된 '루팅 가능한 상자'를 나타내는 클래스입니다.
/// F키를 꾹 누르면 열리며, 상자마다 다른 크기의 ContainerSection UI가 표시됩니다.
/// </summary>
public class box_defualt : MonoBehaviour, IInteractable
{
    public string boxName = "오래된 보급 상자";

    [Header("상호작용 설정")]
    [Tooltip("상자를 열기 위해 F키를 꾹 눌러야 하는 시간(초)입니다. 상자마다 다르게 설정할 수 있습니다.")]
    public float holdDuration = 2f;

    [Header("UI 설정")]
    [Tooltip("이 상자를 열었을 때 오른쪽에 표시할 컨테이너 섹션 종류.\nTypeA=10슬롯 / TypeB=20슬롯 / TypeC=40슬롯")]
    public ContainerSectionType containerType = ContainerSectionType.TypeA;

    [Tooltip("씬의 UI_InventoryLootCanvas에 붙어있는 InventoryLootUIReferences를 연결하세요.")]
    public InventoryLootUIReferences uiRefs;

    [Tooltip("플레이어에 붙어있는 inventory 스크립트를 연결하세요. 주변 아이템 스캔 및 UI제어에 사용합니다.")]
    public inventory inventoryScript;

    private bool isTargeted = false;
    public bool isOpen = false;
    private float holdProgress = 0f;

    // 런타임에 Instantiate된 ContainerSection 인스턴스 추적
    private GameObject spawnedContainerSection = null;

    [Header("상자 내용물")]
    [Tooltip("상자 안에 들어있는 아이템 목록입니다.")]
    public System.Collections.Generic.List<ItemData> containerItems = new System.Collections.Generic.List<ItemData>();

    // 상호작용한 플레이어의 Transform (주변 아이템 스캔 기준점으로 사용)
    private Transform interactorTransform = null;

    // UI_AreaLootPanel의 원래 위치를 기억해두기 위한 변수
    private Vector2 originalAreaLootPosition;

    [SerializeField]
    private bool isartificial = false;

    [Header("바닥 고정 설정")]
    public LayerMask groundLayer = Physics.DefaultRaycastLayers;
    public float heightOffset = 0.5f;

    // 원형 게이지 설정
    private const float RingRadius = 18f;
    private const int RingSegments = 64;
    private const float RingWidth = 3f;

    private Texture2D whiteTex;
    private GUIStyle textStyle;

    void Start()
    {
        if (isartificial == true)
        {
            Collider coll = GetComponent<Collider>();
            if (coll != null) coll.isTrigger = true;

            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out hit, 10f, groundLayer))
            {
                transform.position = hit.point + Vector3.up * heightOffset;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 18;
        textStyle.fontStyle = FontStyle.Bold;
    }

    private float nextScanTime = 0f;

    /// <summary>
    /// 상자가 열려있는 동안 Tab 키 입력을 감지하여 닫습니다.
    /// </summary>
    void Update()
    {
        if (isOpen)
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                CloseBox();
            }

            // 최적화: Sqrt가 포함된 Distance 대신 sqrMagnitude 사용 (3.5 * 3.5 = 12.25)
            if (interactorTransform != null && (transform.position - interactorTransform.position).sqrMagnitude > 12.25f)
            {
                CloseBox(false); // 거리가 멀어져서 닫힐 때는 인벤토리는 열어둠
                return;
            }

            // inventory.cs는 인벤토리가 직접 열린 상태(isInventoryOpen=true)에서만 스캔하므로,
            // 상자를 열었을 때는 여기서 수동으로 스캔을 갱신해줍니다.
            if (inventoryScript != null && Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + inventoryScript.scanInterval;
                inventoryScript.RefreshNearbyLoot();
            }

            // 상자가 열려있는 동안 NearbyLoot 패널이 켜지면 위치를 -490으로 고정
            if (uiRefs != null && uiRefs.UI_AreaLootPanel != null && uiRefs.UI_AreaLootPanel.activeSelf)
            {
                RepositionNearbyLootPanel();
            }
        }
    }

    /// <summary>
    /// 플레이어가 F키를 충분히 꾹 눌렀을 때 호출됩니다.
    /// 왼쪽 인벤토리 패널을 열고, 오른쪽에 상자 ContainerSection을 생성합니다.
    /// </summary>
    public void Interact(GameObject interactor)
    {
        if (isOpen) return;

        if (uiRefs == null)
        {
            Debug.LogWarning($"[box_defualt:{boxName}] uiRefs가 연결되지 않았습니다. 인스펙터에서 설정해주세요.");
            return;
        }

        isOpen = true;
        holdProgress = 0f;

        // 상자가 열린 동안 주변 아이템의 타겟 UI(글씨, 지시선) 숨김
        item_defualt.suppressUI = true;

        // 왼쪽: 플레이어 인벤토리 패널 열기 (inventory.cs 통해 안전하게 열기)
        if (inventoryScript != null)
        {
            inventoryScript.ForceOpenInventory();
            inventoryScript.ignoreTabInput = true; // 상자가 열려있을 때는 box_defualt가 Tab을 전담함
        }
        else if (uiRefs.UI_PlayerInventoryPanel != null)
        {
            uiRefs.UI_PlayerInventoryPanel.SetActive(true);
        }

        // 플레이어 Transform 저장 (주변 아이템 스캔 기준점)
        interactorTransform = interactor.transform;

        // 오른쪽 상단: ContainerSection을 캔버스 루트에 직접 생성
        SpawnContainerSection();

        // 주변 아이템 스캔: inventory.cs가 플레이어 기준으로 스캔하고 UI_AreaLootPanel을 자동 ON/OFF
        // ForceRefreshNearbyLoot() 호출 전에 X좌표를 미리 저장 (호출 후 X가 바뀔 수 있으므로)
        if (inventoryScript != null)
        {
            if (uiRefs.UI_AreaLootPanel != null)
            {
                RectTransform areaRT = uiRefs.UI_AreaLootPanel.GetComponent<RectTransform>();
                if (areaRT != null) originalAreaLootPosition = areaRT.anchoredPosition;
            }

            inventoryScript.ForceRefreshNearbyLoot();

            if (uiRefs.UI_AreaLootPanel != null && uiRefs.UI_AreaLootPanel.activeSelf)
                RepositionNearbyLootPanel();
        }

        Debug.Log($"[box_defualt:{boxName}] 열림! ContainerType: {containerType}");

        // 인벤토리 스크립트에 상자가 열렸음을 알리고 내부 아이템 배치를 위임
        if (inventoryScript != null)
        {
            inventoryScript.OpenContainer(this, spawnedContainerSection);
        }

        // TODO: 열림 애니메이션 재생
    }

    /// <summary>
    /// 선택한 타입의 ContainerSection 프리팹을 캔버스 루트 바로 아래에 Instantiate하고
    /// RectTransform으로 오른쪽 상단에 고정 배치합니다.
    /// UI_AreaLootPanel과 완전히 독립적으로 동작합니다.
    /// </summary>
    private void SpawnContainerSection()
    {
        // 기존에 열려있던 섹션 제거
        if (spawnedContainerSection != null)
        {
            Destroy(spawnedContainerSection);
            spawnedContainerSection = null;
        }

        // 타입에 맞는 프리팹 선택
        GameObject prefab = null;
        switch (containerType)
        {
            case ContainerSectionType.TypeA: prefab = uiRefs.ContainerSectionTypeAPrefab; break;
            case ContainerSectionType.TypeB: prefab = uiRefs.ContainerSectionTypeBPrefab; break;
            case ContainerSectionType.TypeC: prefab = uiRefs.ContainerSectionTypeCPrefab; break;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[box_defualt:{boxName}] ContainerType '{containerType}'에 해당하는 프리팹이 uiRefs에 연결되지 않았습니다.");
            return;
        }

        // UI_AreaLootPanel과 무관하게 캔버스 루트 바로 아래에 생성
        spawnedContainerSection = Object.Instantiate(prefab, uiRefs.transform);
        spawnedContainerSection.SetActive(true);

        // RectTransform으로 오른쪽 상단에 고정
        RectTransform rt = spawnedContainerSection.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin        = new Vector2(1f, 1f); // 오른쪽 상단 앵커
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f); // 오른쪽 상단 기준점
            rt.anchoredPosition = new Vector2(-90f, -10f);
        }
    }

    /// <summary>
    /// 상자는 인스펙터에서 설정한 holdDuration만큼 꾹 눌러야 열립니다.
    /// </summary>
    public float GetHoldDuration() => holdDuration;

    /// <summary>
    /// PlayerInteractor로부터 현재 F키 진행도(0.0 ~ 1.0)를 전달받아 원형 게이지에 반영합니다.
    /// </summary>
    public void SetHoldProgress(float progress)
    {
        holdProgress = progress;
    }

    /// <summary>
    /// PlayerInteractor에 의해 타겟으로 지정되거나 해제될 때 호출됩니다.
    /// 멀어졌을 때 상자를 자동으로 닫고 UI를 정리합니다.
    /// </summary>
    public void SetTargeted(bool targeted)
    {
        isTargeted = targeted;

        // PlayerInteractor가 다른 대상을 타겟팅하기 위해 타겟을 해제하더라도
        // 바로 닫지 않도록 변경. 대신 Update에서 거리 기반으로 닫습니다.
    }

    /// <summary>
    /// 상자를 닫고 생성된 ContainerSection을 제거하며 UI 패널들을 닫습니다.
    /// </summary>
    private void CloseBox(bool closeInventory = true)
    {
        isOpen = false;
        holdProgress = 0f;

        // 상자가 닫히면 아이템 UI 다시 표시
        item_defualt.suppressUI = false;

        // 생성된 ContainerSection 프리팹 인스턴스 제거
        if (spawnedContainerSection != null)
        {
            if (inventoryScript != null)
            {
                inventoryScript.CloseContainer(this);
            }
            Destroy(spawnedContainerSection);
            spawnedContainerSection = null;
        }

        // inventory.cs를 통해 인벤토리 및 Nearby UI 안전하게 닫기
        if (inventoryScript != null)
        {
            if (closeInventory)
            {
                inventoryScript.ForceCloseInventory();
                // Tab 키 충돌을 막기 위해 1프레임 뒤에 ignoreTabInput을 해제합니다.
                StartCoroutine(ResetIgnoreTabNextFrame());
            }
            else
            {
                // 인벤토리를 유지하는 경우 즉시 권한을 넘겨줌
                inventoryScript.ignoreTabInput = false;
            }
        }
        else if (uiRefs != null && uiRefs.UI_PlayerInventoryPanel != null)
        {
            uiRefs.UI_PlayerInventoryPanel.SetActive(false);
        }

        if (uiRefs != null)
        {
            // 상자가 열릴 때 이동시켰던 UI_AreaLootPanel 닫고 원래 위치로 복원
            if (uiRefs.UI_AreaLootPanel != null)
            {
                uiRefs.UI_AreaLootPanel.SetActive(false);
                RectTransform areaRT = uiRefs.UI_AreaLootPanel.GetComponent<RectTransform>();
                if (areaRT != null)
                    areaRT.anchoredPosition = originalAreaLootPosition;
            }
        }

        Debug.Log($"[box_defualt:{boxName}] 닫힘! (인벤토리 닫힘: {closeInventory})");
        // TODO: 닫힘 애니메이션 재생
    }

    private System.Collections.IEnumerator ResetIgnoreTabNextFrame()
    {
        yield return null; // 1프레임 대기
        if (inventoryScript != null)
        {
            inventoryScript.ignoreTabInput = false;
        }
    }

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// inventory.cs가 이미 켰율던 UI_AreaLootPanel을 Y=-490 위치로 재조정합니다.
    /// 상자가 열린 동안 닫히면 원래 위치로 복원됩니다.
    /// </summary>
    private void RepositionNearbyLootPanel()
    {
        if (uiRefs.UI_AreaLootPanel == null) return;

        RectTransform areaRT = uiRefs.UI_AreaLootPanel.GetComponent<RectTransform>();
        if (areaRT == null) return;

        // X좌표는 저장해둔 원래 X값 그대로, Y좌표만 -490으로 이동
        areaRT.anchoredPosition = new Vector2(originalAreaLootPosition.x, -490f);
    }

    /// <summary>
    /// 상자가 타겟팅되었을 때 화면에 사선 지시선, 이름, F키 원형 게이지를 그립니다.
    /// </summary>
    void OnGUI()
    {
        if (!isTargeted || isOpen || Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z < 0) return;

        Vector2 center = new Vector2(screenPos.x, Screen.height - screenPos.y);
        bool isLeft = center.x < Screen.width / 2f;
        float dirX = isLeft ? 1f : -1f;

        float diagLength = 40f;
        float horizLength = 80f;

        Vector2 p1 = center;
        Vector2 p2 = center + new Vector2(dirX * diagLength, -diagLength);
        Vector2 p3 = p2 + new Vector2(dirX * horizLength, 0);

        // 중심 점
        DrawBox(center - new Vector2(4, 4), new Vector2(8, 8), Color.white);
        DrawLine(p1, p2, 2f, Color.white);
        DrawLine(p2, p3, 2f, Color.white);

        // 원형 게이지 (F키를 누르고 있을 때만 표시)
        if (holdProgress > 0f)
        {
            DrawRadialRing(center, RingRadius, holdProgress);
        }

        // 안내 문구
        string text = boxName + "\n열려면 F (꾹 누르기)";
        Vector2 textSize = textStyle.CalcSize(new GUIContent(text));

        Rect textRect;
        if (isLeft)
        {
            textStyle.alignment = TextAnchor.UpperLeft;
            textRect = new Rect(p2.x, p2.y + 5, textSize.x, textSize.y);
        }
        else
        {
            textStyle.alignment = TextAnchor.UpperRight;
            textRect = new Rect(p3.x, p3.y + 5, textSize.x, textSize.y);
        }

        // 그림자 효과
        Color backupColor = textStyle.normal.textColor;
        textStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(textRect.x + 1, textRect.y + 1, textRect.width, textRect.height), text, textStyle);

        // 본 텍스트
        textStyle.normal.textColor = backupColor;
        GUI.Label(textRect, text, textStyle);
    }

    /// <summary>
    /// 12시 방향에서 시작하여 시계 방향으로 progress만큼 채워지는 원형 고리를 그립니다.
    /// </summary>
    private void DrawRadialRing(Vector2 center, float radius, float progress)
    {
        int totalSegments = Mathf.RoundToInt(RingSegments * progress);
        if (totalSegments < 1) return;

        for (int i = 0; i < totalSegments; i++)
        {
            float angleFrom = Mathf.Lerp(-90f, 270f, (float)i / RingSegments) * Mathf.Deg2Rad;
            float angleTo   = Mathf.Lerp(-90f, 270f, (float)(i + 1) / RingSegments) * Mathf.Deg2Rad;

            Vector2 from = center + new Vector2(Mathf.Cos(angleFrom), Mathf.Sin(angleFrom)) * radius;
            Vector2 to   = center + new Vector2(Mathf.Cos(angleTo),   Mathf.Sin(angleTo))   * radius;

            DrawLine(from, to, RingWidth, Color.white);
        }
    }

    private void DrawLine(Vector2 pointA, Vector2 pointB, float width, Color color)
    {
        Matrix4x4 matrixBackup = GUI.matrix;
        Color colorBackup = GUI.color;
        GUI.color = color;
        float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * 180f / Mathf.PI;
        float length = Vector2.Distance(pointA, pointB);
        GUIUtility.RotateAroundPivot(angle, pointA);
        GUI.DrawTexture(new Rect(pointA.x, pointA.y - width / 2f, length, width), whiteTex);
        GUI.matrix = matrixBackup;
        GUI.color = colorBackup;
    }

    private void DrawBox(Vector2 pos, Vector2 size, Color color)
    {
        Color colorBackup = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(pos.x, pos.y, size.x, size.y), whiteTex);
        GUI.color = colorBackup;
    }

    void OnDestroy()
    {
        if (whiteTex != null) Destroy(whiteTex);
    }
}

/// <summary>
/// 상자를 열었을 때 표시할 ContainerSection UI의 종류를 선택합니다.
/// TypeA = 10슬롯, TypeB = 20슬롯, TypeC = 40슬롯
/// </summary>
public enum ContainerSectionType
{
    TypeA, // 10슬롯 (소형 상자)
    TypeB, // 20슬롯 (중형 상자)
    TypeC  // 40슬롯 (대형 상자)
}
