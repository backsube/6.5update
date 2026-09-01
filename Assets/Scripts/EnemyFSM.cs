using UnityEngine;

/// <summary>
/// 적 AI 유한 상태 머신 (FSM)
/// GDD 04-5 기반 — Phase 0~1 최소 구현
/// 
/// [상태]
/// Idle     → 순찰 (랜덤 waypoint)
/// Alert    → 마지막 감지 위치 수색
/// Aggro    → 플레이어 추적 + 사격
/// Retreat  → HP 30% 이하, 뒤로 후퇴
/// Dead     → 루팅 가능 상태
/// 
/// [팩션별 분기]
/// Reaver    : 시야에 플레이어 → 즉시 Aggro
/// Maintainer: 플레이어 접근 시 Alert(경고) → 공격받으면 Aggro
/// 
/// [부착 위치] EnemyBase, EnemyPerception과 같은 적 프리팹
/// </summary>
[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyPerception))]
public class EnemyFSM : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed        = 3.5f;
    [SerializeField] private float runSpeed         = 5.5f;
    [SerializeField] private float rotationSpeed    = 360f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("순찰 설정")]
    [SerializeField] private float patrolRadius   = 8f;
    [SerializeField] private float patrolWaitTime = 2f;

    [Header("전투 설정")]
    [SerializeField] private float attackRange    = 12f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int   attackDamage   = 15;
    [Tooltip("사격 탄퍼짐 (도). 클수록 덜 정확함. 8-12도가 벽 뒤 회피 가능한 수준")]
    [SerializeField] private float aimSpreadAngle = 10f;

    [Header("Maintainer 전용")]
    [Tooltip("이 거리 이내로 접근하면 경고 상태 진입")]
    [SerializeField] private float warningDistance = 5f;

    // ─── 컴포넌트 참조 ───────────────────────────
    private EnemyBase       enemyBase;
    private EnemyPerception perception;
    private Transform       player;

    // ─── FSM 상태 ────────────────────────────────
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    // Idle 순찰
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private float   patrolWaitTimer;
    private bool    isWaiting;

    // Alert 수색
    private Vector3 alertPosition;
    private float   alertSearchTimer;

    // 전투
    private float attackTimer;

    // Maintainer 전용
    private bool maintainerHostile = false;

    // ─────────────────────────────────────────────
    void Awake()
    {
        enemyBase  = GetComponent<EnemyBase>();
        perception = GetComponent<EnemyPerception>();

        if (enemyBase != null && enemyBase.Data != null)
        {
            moveSpeed        = enemyBase.Data.moveSpeed;
            runSpeed         = enemyBase.Data.runSpeed;
            rotationSpeed    = enemyBase.Data.rotationSpeed;
            stoppingDistance = enemyBase.Data.stoppingDistance;
            attackRange      = enemyBase.Data.attackRange;
            attackCooldown   = enemyBase.Data.attackCooldown;
            attackDamage     = enemyBase.Data.attackDamage;
            warningDistance  = enemyBase.Data.warningDistance;
        }

        spawnPosition = transform.position;
        patrolTarget  = spawnPosition;

        // Player 태그로 플레이어 Transform 획득
        // ★ player 오브젝트에 태그 "Player" 설정 필요
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogWarning("[EnemyFSM] 'Player' 태그가 설정된 오브젝트를 찾을 수 없습니다.");

        // 사망 이벤트 구독
        enemyBase.OnDeath += HandleDeath;
    }

    void Update()
    {
        if (CurrentState == EnemyState.Dead) return;

        // 체력 30% 이하 → Retreat (어떤 상태에서든 우선 적용)
        if (CurrentState != EnemyState.Retreat && enemyBase.GetHealthPercent() <= 0.3f)
        {
            ChangeState(EnemyState.Retreat);
            return;
        }

        CheckStateTransitions();
        ExecuteCurrentState();
        CheckSoundDetection();
    }

    // ─────────────────────────────────────────────
    //  상태 전환 조건 체크
    // ─────────────────────────────────────────────
    void CheckStateTransitions()
    {
        switch (enemyBase.faction)
        {
            case FactionType.Reaver:
                TransitionReaver();
                break;
            case FactionType.Maintainer:
                TransitionMaintainer();
                break;
            // Archivist, Guardian: Phase 2에서 추가
        }
    }

    void TransitionReaver()
    {
        if (perception.PlayerInSight)
        {
            // 시야에 플레이어 → 즉시 Aggro
            if (CurrentState != EnemyState.Aggro)
                ChangeState(EnemyState.Aggro);
        }
        else if (CurrentState == EnemyState.Aggro)
        {
            // 시야를 잃으면 마지막 위치로 Alert 수색
            alertPosition = perception.LastKnownPosition;
            ChangeState(EnemyState.Alert);
        }
    }

    void TransitionMaintainer()
    {
        if (player == null) return;
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (maintainerHostile)
        {
            // 적대화된 Maintainer는 Reaver와 동일하게 동작
            if (perception.PlayerInSight)
            {
                if (CurrentState != EnemyState.Aggro)
                    ChangeState(EnemyState.Aggro);
            }
            else if (CurrentState == EnemyState.Aggro)
            {
                alertPosition = perception.LastKnownPosition;
                ChangeState(EnemyState.Alert);
            }
        }
        else
        {
            // 적대화 전: 경고 거리 기반 Alert ↔ Idle
            if (distToPlayer < warningDistance && CurrentState == EnemyState.Idle)
            {
                alertPosition = player.position;
                ChangeState(EnemyState.Alert); // 경고 자세
            }
            else if (distToPlayer >= warningDistance && CurrentState == EnemyState.Alert && !maintainerHostile)
            {
                ChangeState(EnemyState.Idle); // 물러나면 해제
            }
        }
    }

    // ─────────────────────────────────────────────
    //  소리 감지 → Alert 전환
    // ─────────────────────────────────────────────
    void CheckSoundDetection()
    {
        if (CurrentState == EnemyState.Aggro) return;

        if (perception.ConsumeSoundDetection(out Vector3 soundPos))
        {
            alertPosition = soundPos;
            ChangeState(EnemyState.Alert);
        }
    }

    // ─────────────────────────────────────────────
    //  상태별 실행 로직
    // ─────────────────────────────────────────────
    void ExecuteCurrentState()
    {
        switch (CurrentState)
        {
            case EnemyState.Idle:    ExecuteIdle();    break;
            case EnemyState.Alert:   ExecuteAlert();   break;
            case EnemyState.Aggro:   ExecuteAggro();   break;
            case EnemyState.Retreat: ExecuteRetreat(); break;
        }
    }

    // ── Idle: 스폰 지점 반경 내 랜덤 순찰 ──────────
    void ExecuteIdle()
    {
        if (isWaiting)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                isWaiting    = false;
                patrolTarget = spawnPosition + new Vector3(
                    Random.Range(-patrolRadius, patrolRadius), 0f,
                    Random.Range(-patrolRadius, patrolRadius));
            }
            return;
        }

        MoveToward(patrolTarget, moveSpeed);

        if (Vector3.Distance(transform.position, patrolTarget) < 0.5f)
        {
            isWaiting       = true;
            patrolWaitTimer = patrolWaitTime;
        }
    }

    // ── Alert: 감지 위치로 이동 → 일정 시간 수색 ───
    void ExecuteAlert()
    {
        MoveToward(alertPosition, moveSpeed);

        alertSearchTimer -= Time.deltaTime;

        if (alertSearchTimer <= 0f)
        {
            // 수색 완료 → Idle 복귀
            if (!maintainerHostile)
                ChangeState(EnemyState.Idle);
        }
    }

    // ── Aggro: 플레이어 추적 + 사거리 내 사격 ──────
    void ExecuteAggro()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > stoppingDistance)
            MoveToward(player.position, runSpeed);
        else
            FaceTarget(player.position);

        // 사격
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && dist <= attackRange)
        {
            ShootAtPlayer();
            attackTimer = attackCooldown;
        }
    }

    // ── Retreat: 플레이어 반대 방향으로 후퇴 ────────
    void ExecuteRetreat()
    {
        if (player == null) return;

        Vector3 awayDir     = (transform.position - player.position).normalized;
        Vector3 retreatDest = transform.position + awayDir * 5f;

        MoveToward(retreatDest, moveSpeed * 0.8f);
    }

    // ─────────────────────────────────────────────
    //  상태 변경
    // ─────────────────────────────────────────────
    public void ChangeState(EnemyState newState)
    {
        if (CurrentState == newState) return;

        ExitState(CurrentState);
        CurrentState = newState;
        EnterState(newState);

        Debug.Log($"[EnemyFSM] {gameObject.name}: → {newState}");
    }

    void EnterState(EnemyState state)
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        switch (state)
        {
            case EnemyState.Idle:
                if (meshRenderer != null) meshRenderer.material.color = Color.white;
                break;
            case EnemyState.Alert:
                alertSearchTimer = 5f;
                if (meshRenderer != null) meshRenderer.material.color = Color.yellow;
                break;
            case EnemyState.Aggro:
                attackTimer = 0f; // 진입 즉시 공격 가능
                if (meshRenderer != null) meshRenderer.material.color = Color.red;
                break;
            case EnemyState.Retreat:
                if (meshRenderer != null) meshRenderer.material.color = Color.blue;
                break;
            case EnemyState.Dead:
                // 콜라이더 트리거로 변경 (루팅 가능, 물리 충돌만 제거)
                Collider col = GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
                if (meshRenderer != null) meshRenderer.material.color = Color.gray;
                break;
        }
    }

    void ExitState(EnemyState state) { /* 향후 Exit 처리 확장용 */ }

    // ─────────────────────────────────────────────
    //  외부 호출 메서드
    // ─────────────────────────────────────────────
    /// <summary>
    /// Witness System 호출: 특정 위치로 Alert 강제 전환
    /// EnemyBase.AlertNearbyAllies()에서 호출됨
    /// </summary>
    public void ForceAlert(Vector3 position)
    {
        if (CurrentState == EnemyState.Dead) return;
        alertPosition = position;
        ChangeState(EnemyState.Alert);
    }

    /// <summary>
    /// Maintainer 공격받음 → 적대 전환
    /// EnemyBase.TakeDamage()에서 호출됨
    /// </summary>
    public void SetMaintainerHostile()
    {
        if (enemyBase.faction != FactionType.Maintainer) return;
        if (maintainerHostile) return;

        maintainerHostile = true;
        ChangeState(EnemyState.Aggro);
        Debug.Log($"[EnemyFSM] {gameObject.name} Maintainer → 적대 전환!");
    }

    // ─────────────────────────────────────────────
    //  이동 / 회전
    // ─────────────────────────────────────────────
    void MoveToward(Vector3 target, float speed)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        transform.position += dir.normalized * speed * Time.deltaTime;
        FaceTarget(target);
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    //  전투: 플레이어에게 Raycast 사격
    // ─────────────────────────────────────────────
    void ShootAtPlayer()
    {
        if (player == null) return;

        Vector3 eyePos    = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = player.position + Vector3.up * 0.5f;
        Vector3 dir       = (targetPos - eyePos).normalized;

        // 탄퍼짐 적용: 일정 각도 내 랜덤 편차로 완벽 명중 방지
        if (aimSpreadAngle > 0f)
        {
            Quaternion spread = Quaternion.Euler(
                Random.Range(-aimSpreadAngle, aimSpreadAngle),
                Random.Range(-aimSpreadAngle, aimSpreadAngle),
                0f
            );
            dir = spread * dir;
        }

        // 탄착점 결정
        Vector3 trailEnd;
        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, attackRange))
        {
            trailEnd = hit.point;
            
            // 진단용 로그 추가: 적이 무엇을 맞히고 있는지 콘솔에 출력
            Debug.Log($"[EnemyFSM] {gameObject.name} 사격 적중: {hit.collider.gameObject.name} (레이어: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
            
            // 자식 콜라이더에 맞아도 부모 오브젝트의 PlayerHealth를 찾을 수 있도록 GetComponentInParent 사용
            PlayerHealth ph = hit.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null) 
            {
                ph.TakeDamage(attackDamage);
            }
        }
        else
        {
            trailEnd = eyePos + dir * attackRange;
        }

        // 플레이어와 동일한 총알 시각 효과
        BulletTrail.Spawn(eyePos, trailEnd, 25f);
    }

    // ─────────────────────────────────────────────
    //  사망 처리
    // ─────────────────────────────────────────────
    void HandleDeath()
    {
        ChangeState(EnemyState.Dead);
        SessionManager.Instance?.RegisterKill();
    }

    void OnDestroy()
    {
        if (enemyBase != null)
            enemyBase.OnDeath -= HandleDeath;
    }

    // ─────────────────────────────────────────────
    //  에디터 시각화
    // ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // 공격 사거리
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 순찰 반경
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            Application.isPlaying ? spawnPosition : transform.position,
            patrolRadius);

        // Maintainer 경고 거리
        if (enemyBase != null && enemyBase.faction == FactionType.Maintainer)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, warningDistance);
        }
    }
}

// ─────────────────────────────────────────────────
//  FSM 상태 열거형 (전역 공유)
// ─────────────────────────────────────────────────
public enum EnemyState
{
    Idle,
    Alert,
    Aggro,
    Retreat,
    Dead
}
