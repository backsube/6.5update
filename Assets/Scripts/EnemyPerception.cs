using UnityEngine;

/// <summary>
/// 적의 감지 시스템 (시야 + 청각)
/// GDD 04-1 시야 시스템 기반 구현
/// 
/// [기능]
/// - 120° 부채꼴 시야 (GDD 기준)
/// - 장애물(벽) 레이캐스트 차단
/// - 소리 이벤트 수신 (WeaponController 발사음 등)
/// 
/// [부착 위치] EnemyBase, EnemyFSM과 같은 적 프리팹
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [Header("시야 설정")]
    [Tooltip("시야 반경 (유닛)")]
    [SerializeField] private float viewRange    = 10f;
    [Tooltip("시야각 — GDD: 120°")]
    [SerializeField] private float viewAngle    = 120f;
    [Tooltip("시야를 차단하는 레이어 (Wall 등)")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("청각 설정")]
    [Tooltip("소리 감지 최대 반경")]
    [SerializeField] private float hearingRange = 8f;

    [Header("플레이어 레이어")]
    [Tooltip("Player 레이어 마스크")]
    [SerializeField] private LayerMask playerLayer;

    // ─── 공개 상태 ────────────────────────────────
    /// <summary>현재 시야 내 플레이어 Transform (없으면 null)</summary>
    public Transform DetectedPlayer  { get; private set; }
    /// <summary>마지막으로 플레이어를 확인한 위치</summary>
    public Vector3   LastKnownPosition { get; private set; }
    /// <summary>현재 시야 내에 플레이어가 있는가</summary>
    public bool      PlayerInSight    { get; private set; }

    // 소리 감지 내부 상태
    private bool    soundDetected;
    private Vector3 soundAlertPosition;

    // ─────────────────────────────────────────────
    void Start()
    {
        EnemyBase enemyBase = GetComponent<EnemyBase>();
        if (enemyBase != null && enemyBase.Data != null)
        {
            viewRange = enemyBase.Data.viewRange;
            viewAngle = enemyBase.Data.viewAngle;
            hearingRange = enemyBase.Data.hearingRange;
        }
    }

    void Update()
    {
        CheckVision();
    }

    // ─────────────────────────────────────────────
    //  시야 체크
    // ─────────────────────────────────────────────
    void CheckVision()
    {
        PlayerInSight  = false;
        DetectedPlayer = null;

        // 반경 내 Player 레이어 오브젝트 탐색
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, viewRange, playerLayer);

        foreach (var col in playersInRange)
        {
            Transform target   = col.transform;
            Vector3   dirToTarget = (target.position - transform.position).normalized;

            // 1) 시야각 체크
            float angle = Vector3.Angle(transform.forward, dirToTarget);
            if (angle > viewAngle * 0.5f) continue;

            // 2) 장애물 레이캐스트
            float dist = Vector3.Distance(transform.position, target.position);
            Vector3 eyePos = transform.position + Vector3.up * 0.5f; // 눈 높이 보정

            if (Physics.Raycast(eyePos, dirToTarget, dist, obstacleMask))
                continue; // 벽에 막힘

            // 감지 성공
            PlayerInSight      = true;
            DetectedPlayer     = target;
            LastKnownPosition  = target.position;
            break;
        }
    }

    // ─────────────────────────────────────────────
    //  청각 — 외부 이벤트로 소리 수신
    // ─────────────────────────────────────────────
    /// <summary>
    /// 총소리, 발소리 등 소리 이벤트 수신
    /// WeaponController.Shoot() 또는 플레이어 이동 스크립트에서 호출
    /// </summary>
    /// <param name="soundOrigin">소리 발생 월드 위치</param>
    /// <param name="soundRadius">소리 전파 반경</param>
    public void OnSoundHeard(Vector3 soundOrigin, float soundRadius)
    {
        float dist = Vector3.Distance(transform.position, soundOrigin);

        if (dist <= Mathf.Min(soundRadius, hearingRange))
        {
            soundDetected     = true;
            soundAlertPosition = soundOrigin;
        }
    }

    /// <summary>
    /// FSM에서 매 프레임 소리 감지 여부 확인 후 소비
    /// 한 번 읽으면 초기화됨
    /// </summary>
    public bool ConsumeSoundDetection(out Vector3 alertPos)
    {
        alertPos = soundAlertPosition;
        if (soundDetected)
        {
            soundDetected = false;
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────
    //  에디터 시야 시각화
    // ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // 시야 반경
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRange);

        // 시야각 경계선
        Vector3 fwd        = Application.isPlaying ? transform.forward : transform.forward;
        Vector3 leftBound  = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * fwd * viewRange;
        Vector3 rightBound = Quaternion.Euler(0,  viewAngle * 0.5f, 0) * fwd * viewRange;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftBound);
        Gizmos.DrawRay(transform.position, rightBound);

        // 청각 반경
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hearingRange);
    }
}
