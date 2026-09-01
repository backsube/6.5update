using UnityEngine;

/// <summary>
/// 필드에 떨어져 있는 일반적인 '아이템'을 나타내는 클래스입니다.
/// 플레이어가 다가가면 UI 선이 표시되며, E키를 눌러 획득할 수 있습니다.
/// </summary>
public class item_defualt : MonoBehaviour, IInteractable
{
    [Header("아이템 설정")]
    [Tooltip("여기에 생성한 ItemData 에셋을 넣어주세요")]
    public ItemData itemData;

    [Header("바닥 고정 설정")]
    public LayerMask groundLayer = Physics.DefaultRaycastLayers;
    public float heightOffset = 0.5f;

    private bool isTargeted = false; // 타겟 여부 상태값
    private float holdProgress = 0f;  // 원형 게이지 진행도 (0.0 ~ 1.0), 아이템은 즉시 획득이라 실질적으로 사용 안 됨

    /// <summary>
    /// 상자가 열려있는 동안 모든 item_defualt의 OnGUI(글씨, 원형 인디케이터)를 숨깁니다.
    /// box_defualt에서 상자를 열 때 true, 닫을 때 false로 설정합니다.
    /// </summary>
    public static bool suppressUI = false;
    private Texture2D whiteTex;
    private GUIStyle textStyle;

    void Start()
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

        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 18;
        textStyle.fontStyle = FontStyle.Bold;
    }

    /// <summary>
    /// 플레이어가 상호작용(F키)했을 때 호출되는 획득 로직입니다.
    /// </summary>
    public void Interact(GameObject interactor)
    {
        string nameToPrint = (itemData != null) ? itemData.itemName : "알 수 없는 아이템";
        Debug.Log(nameToPrint + " 획득 시도!");
        
        if (itemData != null && inventory.Instance != null)
        {
            // 인벤토리에 자동 배치 (빈 공간 탐색)
            if (inventory.Instance.AutoPlace(itemData))
            {
                Debug.Log(nameToPrint + " 인벤토리에 추가됨!");
                // 획득 성공 시 UI(주변 아이템 목록)를 갱신하고 월드에서 오브젝트 삭제
                inventory.Instance.ForceRefreshNearbyLoot();
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("인벤토리에 " + nameToPrint + "을(를) 넣을 공간이 없습니다!");
            }
        }
    }

    /// <summary>
    /// 아이템은 즉시 획득이므로 홀드 시간이 필요 없습니다. 0을 반환합니다.
    /// </summary>
    public float GetHoldDuration() => 0f;

    /// <summary>
    /// 아이템은 원형 게이지가 없지만, 인터페이스 구현을 위해 진행도를 저장합니다.
    /// </summary>
    public void SetHoldProgress(float progress)
    {
        holdProgress = progress;
    }

    /// <summary>
    /// PlayerInteractor에 의해 가장 가까운 타겟으로 지정되거나 해제될 때 호출됩니다.
    /// </summary>
    public void SetTargeted(bool targeted)
    {
        isTargeted = targeted;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// 아이템이 타겟팅되었을 때 화면에 사선 지시선(Line)과 이름을 그리는 UI 로직입니다.
    /// </summary>
    void OnGUI()
    {
        // 상자가 열려있는 동안은 아이템 UI(글씨, 지시선)를 표시하지 않음
        if (!isTargeted || suppressUI || Camera.main == null) return;

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

        DrawBox(center - new Vector2(4, 4), new Vector2(8, 8), Color.white);
        DrawLine(p1, p2, 2f, Color.white);
        DrawLine(p2, p3, 2f, Color.white);

        string displayName = (itemData != null) ? itemData.itemName : "알 수 없는 아이템";
        string text = displayName + "\n획득하려면 F";
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

        Color backupColor = textStyle.normal.textColor;
        textStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(textRect.x + 1, textRect.y + 1, textRect.width, textRect.height), text, textStyle);
        
        textStyle.normal.textColor = backupColor;
        GUI.Label(textRect, text, textStyle);
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
