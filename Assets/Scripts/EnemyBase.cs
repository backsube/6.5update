using UnityEngine;
using System;

/// <summary>
/// 모든 팩션 적의 기반 클래스
/// 체력, 팩션 타입, 사망, Witness System 경보 담당
/// 
/// [부착 위치] 적 프리팹 루트
/// </summary>
public class EnemyBase : MonoBehaviour
{
    [Header("적 데이터 설정")]
    [SerializeField] private EnemyData enemyData;

    [Header("기본 설정 (데이터 덮어쓰기용)")]
    public FactionType faction = FactionType.Reaver;
    [SerializeField] private int maxHealth = 100;

    [Header("Witness System")]
    [Tooltip("피격 시 주변 동료에게 경보 보내는 반경")]
    [SerializeField] private float witnessAlertRadius = 15f;

    [Header("루팅 설정")]
    [Tooltip("사망 시 시체(상자) 안에 들어갈 아이템 목록입니다.")]
    [SerializeField] private System.Collections.Generic.List<ItemData> dropItems = new System.Collections.Generic.List<ItemData>();

    [Tooltip("적 사망 시 열리는 상자 UI의 크기를 지정합니다.")]
    [SerializeField] private ContainerSectionType containerType = ContainerSectionType.TypeA;

    [Tooltip("추후 화폐 자동 획득용으로 보류된 변수")]
    [SerializeField] private int lootSolariumValue = 30;

    // EnemyData 접근자
    public EnemyData Data => enemyData;
    public int LootSolariumValue => lootSolariumValue;

    // ─── 상태 프로퍼티 ───────────────────────────
    public bool IsDead       { get; private set; }
    public bool IsLootable   { get; private set; }

    private int currentHealth;

    // ─── 이벤트 ──────────────────────────────────
    /// <summary>사망 시 발생 (EnemyFSM이 구독)</summary>
    public event Action OnDeath;

    /// <summary>피격 시 발생 (파라미터: 현재 HP)</summary>
    public event Action<int> OnDamageTaken;

    // ─────────────────────────────────────────────
    void Awake()
    {
        if (enemyData != null)
        {
            faction = enemyData.faction;
            maxHealth = enemyData.maxHealth;
        }
        currentHealth = maxHealth;
    }

    // ─────────────────────────────────────────────
    //  피격 처리
    // ─────────────────────────────────────────────
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        OnDamageTaken?.Invoke(currentHealth);

        Debug.Log($"[EnemyBase] {gameObject.name} 피격. HP: {currentHealth}/{maxHealth}");

        // Maintainer 적대 전환 — 공격받으면 적대화
        if (faction == FactionType.Maintainer && currentHealth > 0)
        {
            GetComponent<EnemyFSM>()?.SetMaintainerHostile();
        }

        // Witness System — 생존 시 주변 아군에게 경보
        if (currentHealth > 0)
        {
            AlertNearbyAllies();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // ─────────────────────────────────────────────
    //  사망
    // ─────────────────────────────────────────────
    void Die()
    {
        if (IsDead) return;
        IsDead     = true;
        IsLootable = true;

        OnDeath?.Invoke();
        Debug.Log($"[EnemyBase] {gameObject.name} 사망. 시체가 루팅 가능한 상자로 변환됨.");

        // 시체를 플레이어가 통과할 수 있도록 콜라이더를 트리거로 변경
        Collider coll = GetComponent<Collider>();
        if (coll != null)
        {
            coll.isTrigger = true;
        }

        // 경로 탐색 컴포넌트가 있다면 비활성화하여 길을 막지 않게 함
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 사망 즉시 시체를 상자(box_defualt)로 변환
        box_defualt box = gameObject.AddComponent<box_defualt>();
        box.boxName = "죽은 " + faction.ToString();
        box.holdDuration = 1.0f; // 시체 루팅은 1초 꾹 누르기
        box.containerType = this.containerType;
        box.containerItems = new System.Collections.Generic.List<ItemData>(this.dropItems); // 설정한 아이템 복사
        
        // 동적 컴포넌트 추가이므로 런타임에 UI 레퍼런스와 인벤토리를 찾아 연결
        box.uiRefs = FindAnyObjectByType<InventoryLootUIReferences>();
        if (inventory.Instance != null)
            box.inventoryScript = inventory.Instance;
        else
            box.inventoryScript = FindAnyObjectByType<inventory>();

        // (참고) 다른 팀원이 쓰던 SessionManager.Instance?.AddLoot(...) 코드는
        // 향후 box 내부의 실제 아이템 생성 로직이 완성되면 그 안에서 처리되거나,
        // 상자를 닫을 때/인벤토리로 옮길 때 처리하는 방식으로 변경되어야 합니다.
    }

    // ─────────────────────────────────────────────
    //  Witness System — 반경 내 동일 팩션 경보
    // ─────────────────────────────────────────────
    void AlertNearbyAllies()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, witnessAlertRadius);

        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;

            EnemyBase  otherBase = col.GetComponent<EnemyBase>();
            EnemyFSM   otherFSM  = col.GetComponent<EnemyFSM>();

            if (otherBase != null && otherFSM != null && otherBase.faction == faction && !otherBase.IsDead)
            {
                otherFSM.ForceAlert(transform.position);
            }
        }
    }

    // ─────────────────────────────────────────────
    //  외부 접근용
    // ─────────────────────────────────────────────
    public float GetHealthPercent() => (float)currentHealth / maxHealth;
    public int   GetCurrentHealth() => currentHealth;
    public int   GetMaxHealth()     => maxHealth;

    // ─────────────────────────────────────────────
    //  에디터 시각화
    // ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, witnessAlertRadius);
    }

    // HP 바 (피격 후 표시)
    void OnGUI()
    {
        if (Camera.main == null) return;

        // 적 머리 위 화면 좌표 계산
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
        if (screenPos.z < 0) return;
        Vector2 center = new Vector2(screenPos.x, Screen.height - screenPos.y);

        // ── HP 바: 살아있고 최대 체력이 아닐 때만 표시 ──
        if (!IsDead && GetHealthPercent() < 1f)
        {
            const float barW = 60f;
            const float barH = 7f;
            float hp = GetHealthPercent();

            // 배경 (어두운 회색)
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            GUI.DrawTexture(new Rect(center.x - barW * 0.5f, center.y - barH * 0.5f, barW, barH), Texture2D.whiteTexture);

            // HP 채움 (초록 → 노랑 → 빨강)
            GUI.color = Color.Lerp(Color.red, Color.green, hp);
            GUI.DrawTexture(new Rect(center.x - barW * 0.5f, center.y - barH * 0.5f, barW * hp, barH), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}

// ─────────────────────────────────────────────────
//  팩션 타입 열거형 (전역 공유)
// ─────────────────────────────────────────────────
public enum FactionType
{
    Maintainer,  // 유지보수파 — Phase 0~1
    Reaver,      // 말살약탈파 — Phase 0~1
    Archivist,   // 데이터 수집가 — Phase 2 예정
    Guardian     // 구인류 수호자 — Phase 2 예정
}
