using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class UITooltipManager : MonoBehaviour
{
    public static UITooltipManager Instance { get; private set; }

    [Header("UI 참조")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    private RectTransform rectTransform;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (tooltipPanel != null)
        {
            rectTransform = tooltipPanel.GetComponent<RectTransform>();

            CanvasGroup canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            foreach (Graphic graphic in tooltipPanel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            tooltipPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent.GetComponent<RectTransform>(), 
                Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero,
                null, 
                out localPoint);

            // 마우스 커서 약간 우측 하단에 표시
            rectTransform.anchoredPosition = localPoint + new Vector2(15f, -15f);
        }
    }

    public void ShowTooltip(string text)
    {
        if (tooltipPanel == null || tooltipText == null) return;

        tooltipText.text = text;
        tooltipPanel.SetActive(true);
        
        // 텍스트 크기에 맞춰 패널 강제 리빌드
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
}
