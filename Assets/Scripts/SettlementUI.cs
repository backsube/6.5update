using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 세션 정산 화면 UI
/// SessionManager.EndSession() 호출 시 표시됨
/// 
/// [부착 위치] SettlementPanel 오브젝트 (Canvas 하위)
/// [씬 세팅] SessionManager의 settlementUI 필드에 드래그 연결
/// </summary>
public class SettlementUI : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("정산 UI 전체 패널 루트")]
    [SerializeField] private GameObject panel;

    [Header("결과 텍스트")]
    [SerializeField] private TextMeshProUGUI resultTitleText;    // "탈출 성공" or "탈출 실패"
    [SerializeField] private TextMeshProUGUI killCountText;      // "처치 수: 3"
    [SerializeField] private TextMeshProUGUI solariumText;       // "획득 솔라늄: 50"
    [SerializeField] private TextMeshProUGUI durationText;       // "탐사 시간: 04:32"
    [SerializeField] private TextMeshProUGUI lootListText;       // 아이템 목록

    [Header("버튼")]
    [SerializeField] private Button returnButton;                // 로비 복귀

    // ─────────────────────────────────────────────
    void Awake()
    {
        // 시작 시 패널 숨김
        if (panel != null)
            panel.SetActive(false);

        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnButtonClick);
    }

    // ─────────────────────────────────────────────
    //  결과 표시 (SessionManager에서 호출)
    // ─────────────────────────────────────────────
    public void ShowResult(SessionResultData data)
    {
        if (panel != null)
            panel.SetActive(true);

        // 게임 일시 정지
        Time.timeScale = 0f;

        // ── 결과 텍스트 채우기 ──────────────────
        if (resultTitleText != null)
        {
            resultTitleText.text  = data.extractionSuccess ? "탈출 성공" : "탈출 실패";
            resultTitleText.color = data.extractionSuccess
                ? new Color(0.2f, 1f, 0.4f)   // 성공: 녹색
                : new Color(1f, 0.3f, 0.3f);  // 실패: 붉은색
        }

        if (killCountText != null)
            killCountText.text = $"처치 수: {data.killCount}";

        if (solariumText != null)
            solariumText.text = $"획득 솔라늄: {data.solariumEarned}";

        if (durationText != null)
            durationText.text = $"탐사 시간: {data.GetFormattedDuration()}";

        if (lootListText != null)
        {
            lootListText.text = data.lootedItemNames.Count == 0
                ? "획득 아이템 없음"
                : "획득 아이템:\n" + string.Join("\n", data.lootedItemNames);
        }
    }

    // ─────────────────────────────────────────────
    //  버튼 처리
    // ─────────────────────────────────────────────
    void OnReturnButtonClick()
    {
        Time.timeScale = 1f;
        SessionManager.Instance?.ReturnToLobby();
    }
}
