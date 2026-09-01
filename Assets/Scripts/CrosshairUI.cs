using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 크로스헤어 UI 관리 (마우스 추적 + 애니메이션)
/// - 빈 오브젝트에 이 스크립트만 추가하면 자동으로 Canvas + 크로스헤어 생성
/// - 마우스 위치를 따라 움직임
/// - 조준 상태에 따라 크기/색상 변경
/// - 4방향 십자 형태
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("Normal State (Hip Fire)")]
    [SerializeField] private float normalGap = 10f;
    [SerializeField] private float normalLineLength = 20f;
    [SerializeField] private float normalLineThickness = 2f;

    [Header("ADS State (Aiming)")]
    [SerializeField] private float adsGap = 5f;
    [SerializeField] private float adsLineLength = 15f;
    [SerializeField] private float adsLineThickness = 1.5f;

    [Header("Animation")]
    [SerializeField] private float transitionSpeed = 10f;

    [Header("Color Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color adsColor = Color.green;
    [SerializeField] private bool showCenterDot = true;
    [SerializeField] private float centerDotSize = 4f;

    private bool isAiming = false;
    private float currentGap;
    private float currentLineLength;
    private float currentLineThickness;
    private Color currentColor;

    [Header("UI References")]
    [SerializeField] private RectTransform crosshairRoot;
    [SerializeField] private RectTransform topLine;
    [SerializeField] private RectTransform bottomLine;
    [SerializeField] private RectTransform leftLine;
    [SerializeField] private RectTransform rightLine;
    [SerializeField] private RectTransform centerDot;

    void Start()
    {
        // 마우스 커서 숨기기
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        // 초기 값 설정
        currentGap = normalGap;
        currentLineLength = normalLineLength;
        currentLineThickness = normalLineThickness;
        currentColor = normalColor;

        // 초기 크로스헤어 설정
        UpdateCrosshair();
    }

#if UNITY_EDITOR
    [ContextMenu("Generate UI in Hierarchy")]
    void GenerateUI()
    {
        // 기존에 생성된 크로스헤어가 있다면 제거
        if (crosshairRoot != null)
        {
            UnityEditor.Undo.DestroyObjectImmediate(crosshairRoot.gameObject);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("CrosshairCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        }

        GameObject rootObj = new GameObject("CrosshairRoot");
        rootObj.transform.SetParent(canvas.transform, false);
        crosshairRoot = rootObj.AddComponent<RectTransform>();
        crosshairRoot.sizeDelta = Vector2.zero;

        topLine = CreateLine("TopLine", rootObj.transform);
        bottomLine = CreateLine("BottomLine", rootObj.transform);
        leftLine = CreateLine("LeftLine", rootObj.transform);
        rightLine = CreateLine("RightLine", rootObj.transform);

        centerDot = CreateLine("CenterDot", rootObj.transform);
        centerDot.sizeDelta = new Vector2(centerDotSize, centerDotSize);
        centerDot.anchoredPosition = Vector2.zero;
        if (!showCenterDot) centerDot.gameObject.SetActive(false);

        UnityEditor.Undo.RegisterCreatedObjectUndo(rootObj, "Create Crosshair UI");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    RectTransform CreateLine(string name, Transform parent)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(parent, false);
        RectTransform rt = lineObj.AddComponent<RectTransform>();
        Image img = lineObj.AddComponent<Image>();
        img.color = normalColor;
        img.raycastTarget = false;
        return rt;
    }
#endif

    void Update()
    {
        bool invOpen = inventory.Instance != null && inventory.Instance.isInventoryOpen;

        if (crosshairRoot != null)
        {
            // 인벤토리가 열려있으면 크로스헤어(조준점) 숨기기
            crosshairRoot.gameObject.SetActive(!invOpen);
        }

        if (invOpen)
        {
            // 인벤토리가 열려있을 때는 마우스가 OS 커서 역할을 하므로 업데이트 중지
            return;
        }

        // ESC 키로 커서 표시/숨김 토글
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleCursor();
        }

        // 우클릭 누르고 있으면 조준 모드
        if (Mouse.current != null)
            isAiming = Mouse.current.rightButton.isPressed;

        // 마우스 위치로 크로스헤어 이동
        if (crosshairRoot != null)
        {
            if (Mouse.current != null)
                crosshairRoot.position = Mouse.current.position.ReadValue();
        }

        // 목표 값으로 부드럽게 전환
        float targetGap = isAiming ? adsGap : normalGap;
        float targetLength = isAiming ? adsLineLength : normalLineLength;
        float targetThickness = isAiming ? adsLineThickness : normalLineThickness;
        Color targetColor = isAiming ? adsColor : normalColor;

        currentGap = Mathf.Lerp(currentGap, targetGap, Time.deltaTime * transitionSpeed);
        currentLineLength = Mathf.Lerp(currentLineLength, targetLength, Time.deltaTime * transitionSpeed);
        currentLineThickness = Mathf.Lerp(currentLineThickness, targetThickness, Time.deltaTime * transitionSpeed);
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);

        // 크로스헤어 업데이트
        UpdateCrosshair();
    }

    void UpdateCrosshair()
    {
        // 위쪽 선
        if (topLine != null)
        {
            topLine.sizeDelta = new Vector2(currentLineThickness, currentLineLength);
            topLine.anchoredPosition = new Vector2(0, currentGap + currentLineLength / 2f);
            Image img = topLine.GetComponent<Image>();
            if (img != null) img.color = currentColor;
        }

        // 아래쪽 선
        if (bottomLine != null)
        {
            bottomLine.sizeDelta = new Vector2(currentLineThickness, currentLineLength);
            bottomLine.anchoredPosition = new Vector2(0, -(currentGap + currentLineLength / 2f));
            Image img = bottomLine.GetComponent<Image>();
            if (img != null) img.color = currentColor;
        }

        // 왼쪽 선
        if (leftLine != null)
        {
            leftLine.sizeDelta = new Vector2(currentLineLength, currentLineThickness);
            leftLine.anchoredPosition = new Vector2(-(currentGap + currentLineLength / 2f), 0);
            Image img = leftLine.GetComponent<Image>();
            if (img != null) img.color = currentColor;
        }

        // 오른쪽 선
        if (rightLine != null)
        {
            rightLine.sizeDelta = new Vector2(currentLineLength, currentLineThickness);
            rightLine.anchoredPosition = new Vector2(currentGap + currentLineLength / 2f, 0);
            Image img = rightLine.GetComponent<Image>();
            if (img != null) img.color = currentColor;
        }

        // 중앙점 업데이트
        if (centerDot != null)
        {
            Image dotImage = centerDot.GetComponent<Image>();
            if (dotImage != null) dotImage.color = currentColor;
        }
    }

    /// <summary>
    /// 조준 상태 설정
    /// </summary>
    public void SetADS(bool aiming)
    {
        isAiming = aiming;
    }

    /// <summary>
    /// 커서 표시/숨김 토글 (ESC 키)
    /// </summary>
    void ToggleCursor()
    {
        Cursor.visible = !Cursor.visible;
        Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Confined;
    }

    void OnDestroy()
    {
        // 씬 종료 시 커서 복구
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
