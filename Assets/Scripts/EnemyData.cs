using UnityEngine;

/// <summary>
/// 적 캐릭터의 기본 능력치를 정의하는 스크립터블 오브젝트
/// GDD 05 팩션 설계 및 적 밸런싱을 위해 사용
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Noumenon/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보")]
    public string enemyName = "Unknown";
    public FactionType faction = FactionType.Reaver;

    [Header("체력")]
    public int maxHealth = 100;

    [Header("이동 속도")]
    public float moveSpeed = 3.5f;
    public float runSpeed = 5.5f;
    public float rotationSpeed = 360f;
    public float stoppingDistance = 1.5f;

    [Header("시야 및 청각")]
    public float viewRange = 10f;
    public float viewAngle = 120f;
    public float hearingRange = 8f;

    [Header("전투")]
    public float attackRange = 12f;
    public float attackCooldown = 1.5f;
    public int attackDamage = 15;

    [Header("Maintainer 전용")]
    public float warningDistance = 5f;
}
