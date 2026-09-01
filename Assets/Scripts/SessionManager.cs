using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세션 흐름 관리 싱글톤
/// 세션 시작 → 데이터 누적 → 세션 종료 → SettlementUI 표시
/// 
/// [부착 위치] 씬 내 "_GameManager" 또는 "SessionManager" 빈 오브젝트
/// [주의] 씬에 단 하나만 존재해야 함
/// </summary>
public class SessionManager : MonoBehaviour
{
    // ─── 싱글톤 ──────────────────────────────────
    public static SessionManager Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private SettlementUI settlementUI;

    // ─── 세션 데이터 ──────────────────────────────
    private SessionResultData currentSession;
    private float             sessionStartTime;
    private bool              sessionEnded;

    // ─────────────────────────────────────────────
    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // NOTE: DontDestroyOnLoad는 Phase 1에서 로비 씬 전환 시 활성화
        // DontDestroyOnLoad(gameObject);

        StartSession();
    }

    // ─────────────────────────────────────────────
    //  세션 시작
    // ─────────────────────────────────────────────
    void StartSession()
    {
        currentSession   = new SessionResultData();
        sessionStartTime = Time.time;
        sessionEnded     = false;
        Debug.Log("[SessionManager] 세션 시작.");
    }

    // ─────────────────────────────────────────────
    //  세션 중 데이터 수집 (외부에서 호출)
    // ─────────────────────────────────────────────

    /// <summary>적 처치 등록. EnemyFSM.HandleDeath()에서 호출.</summary>
    public void RegisterKill()
    {
        if (sessionEnded) return;
        currentSession.RegisterKill();
        Debug.Log($"[SessionManager] 처치 등록. 총 {currentSession.killCount}킬");
    }

    /// <summary>루팅 아이템 등록. 인벤토리 시스템에서 호출 (Phase 1).</summary>
    public void AddLoot(string itemName, int solariumValue = 0)
    {
        if (sessionEnded) return;
        currentSession.AddLoot(itemName, solariumValue);
    }

    // ─────────────────────────────────────────────
    //  세션 종료
    // ─────────────────────────────────────────────

    /// <summary>
    /// 세션 종료 트리거.
    /// ExtractionPoint 탈출 성공 또는 PlayerHealth.Die()에서 호출.
    /// </summary>
    /// <param name="success">탈출 성공 여부</param>
    public void EndSession(bool success)
    {
        if (sessionEnded) return;
        sessionEnded = true;

        currentSession.extractionSuccess       = success;
        currentSession.sessionDurationSeconds  = Time.time - sessionStartTime;

        Debug.Log($"[SessionManager] 세션 종료. 탈출: {success} | 처치: {currentSession.killCount} | 솔라늄: {currentSession.solariumEarned} | 시간: {currentSession.GetFormattedDuration()}");

        // 정산 UI 표시
        if (settlementUI != null)
            settlementUI.ShowResult(currentSession);
        else
            Debug.LogWarning("[SessionManager] SettlementUI가 연결되지 않았습니다.");
    }

    // ─────────────────────────────────────────────
    //  씬 복귀
    // ─────────────────────────────────────────────

    /// <summary>
    /// SettlementUI의 복귀 버튼에서 호출.
    /// Phase 1: 로비 씬으로 전환. 현재: 현재 씬 재로드.
    /// </summary>
    public void ReturnToLobby()
    {
        Time.timeScale = 1f; // 혹시 정지되어 있으면 복구

        // Phase 1에서 활성화:
        // SceneManager.LoadScene("Lobby");

        // Phase 0: 현재 씬 재시작
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
