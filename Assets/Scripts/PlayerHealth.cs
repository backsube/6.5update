using UnityEngine;
using TMPro;

/// <summary>
/// 플레이어 체력 관리
/// 적 EnemyFSM.ShootAtPlayer()에서 TakeDamage() 호출
/// 사망 시 SessionManager.EndSession(false) 연동
/// 
/// [부착 위치] player 오브젝트 (player.cs, WeaponController.cs와 동일)
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("체력 설정")]
    [SerializeField] private int maxHealth = 100;

    [Header("UI 연결")]
    [Tooltip("HP: 80/100 형식으로 표시")]
    [SerializeField] private TextMeshProUGUI healthText;

    // ─── 상태 ─────────────────────────────────────
    public bool IsDead { get; private set; }

    private int currentHealth;

    // ─────────────────────────────────────────────
    void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    // ─────────────────────────────────────────────
    //  피격
    // ─────────────────────────────────────────────
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHealth -= amount;
        currentHealth  = Mathf.Max(0, currentHealth);

        Debug.Log($"[PlayerHealth] 피격. HP: {currentHealth}/{maxHealth}");
        UpdateUI();

        if (currentHealth <= 0)
            Die();
    }

    // ─────────────────────────────────────────────
    //  회복 (수리 키트 등 — Phase 1 확장)
    // ─────────────────────────────────────────────
    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
    }

    // ─────────────────────────────────────────────
    //  사망
    // ─────────────────────────────────────────────
    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log("[PlayerHealth] 플레이어 사망. 세션 종료.");
        SessionManager.Instance?.EndSession(success: false);
    }

    // ─────────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────────
    void UpdateUI()
    {
        if (healthText != null)
            healthText.text = $"HP: {currentHealth} / {maxHealth}";
    }

    // ─────────────────────────────────────────────
    //  외부 접근용
    // ─────────────────────────────────────────────
    public float GetHealthPercent() => (float)currentHealth / maxHealth;
    public int   GetCurrentHealth() => currentHealth;
}
