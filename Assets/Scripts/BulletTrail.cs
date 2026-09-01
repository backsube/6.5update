using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 이동하는 총알 시각 효과
/// 플레이어(WeaponController)와 적(EnemyFSM) 모두에서 동일하게 사용
/// 
/// [사용법] BulletTrail.Spawn(시작점, 끝점, 속도);
/// 
/// [특이사항]
/// - 데미지는 Raycast로 즉시 처리 (피격 판정과 시각 효과 분리)
/// - 총알 오브젝트는 시각 연출 전용, 충돌 판정 없음
/// </summary>
public class BulletTrail : MonoBehaviour
{
    // 내부 상태
    private Vector3 startPos;
    private Vector3 endPos;
    private float   speed;
    private float   totalDist;
    private float   traveled;
    private float   tailLength = 0.6f;
    private LineRenderer lr;

    // 팩토리 메서드: 외부 코드는 이것만 호출
    public static BulletTrail Spawn(Vector3 start, Vector3 end, float bulletSpeed = 25f)
    {
        GameObject obj    = new GameObject("BulletTrail");
        BulletTrail trail  = obj.AddComponent<BulletTrail>();
        trail.Init(start, end, bulletSpeed);
        return trail;
    }

    void Init(Vector3 start, Vector3 end, float bulletSpeed)
    {
        startPos  = start;
        endPos    = end;
        speed     = bulletSpeed;
        totalDist = Vector3.Distance(start, end);
        traveled  = 0f;

        lr = gameObject.AddComponent<LineRenderer>();
        lr.material          = new Material(Shader.Find("Sprites/Default"));
        lr.startColor        = new Color(1f, 0.97f, 0.65f, 1f);
        lr.endColor          = new Color(1f, 0.85f, 0.4f,  0f);
        lr.startWidth        = 0.07f;
        lr.endWidth          = 0.02f;
        lr.positionCount     = 2;
        lr.useWorldSpace     = true;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows    = false;

        lr.SetPosition(0, start);
        lr.SetPosition(1, start);
    }

    void Update()
    {
        traveled += speed * Time.deltaTime;
        float t = Mathf.Clamp01(traveled / totalDist);

        // 탄두 앞부분
        Vector3 bulletHead = Vector3.Lerp(startPos, endPos, t);

        // 탄두 꼬리 (tailLength만큼 뒤)
        float tailT        = Mathf.Clamp01((traveled - tailLength) / totalDist);
        Vector3 bulletTail = Vector3.Lerp(startPos, endPos, tailT);

        lr.SetPosition(0, bulletHead);
        lr.SetPosition(1, bulletTail);

        if (traveled >= totalDist)
            Destroy(gameObject);
    }
}
