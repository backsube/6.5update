using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어가 주변 상호작용 가능한 오브젝트(아이템, 상자 등)를 탐색하고 
/// 상호작용(E키)을 수행할 수 있게 해주는 핵심 컴포넌트입니다.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("상호작용 설정")]
    public float interactionRadius = 3f;
    
    private IInteractable currentTarget;
    private float currentHoldTime = 0f;

    private bool waitForKeyRelease = false;

    void Update()
    {
        // 1. 매 프레임마다 주변에서 가장 가까운 상호작용 대상을 탐색
        FindClosestInteractable();

        // 2. 상호작용 로직 (F키 꾹 누르기)
        
        // 상호작용 완료 직후 플레이어가 아직 F키를 떼지 않았다면, 입력을 무시함 (연속 획득 방지)
        if (waitForKeyRelease)
        {
            if (Keyboard.current == null || !Keyboard.current.fKey.isPressed)
            {
                waitForKeyRelease = false;
            }
            return;
        }

        if (currentTarget != null)
        {
            if (Keyboard.current != null && Keyboard.current.fKey.isPressed)
            {
                float requiredTime = currentTarget.GetHoldDuration();
                currentHoldTime += Time.deltaTime;

                float progress = requiredTime > 0f ? Mathf.Clamp01(currentHoldTime / requiredTime) : 1f;
                currentTarget.SetHoldProgress(progress);

                if (currentHoldTime >= requiredTime)
                {
                    currentTarget.Interact(gameObject);
                    currentTarget.SetHoldProgress(0f); // 실행 후 게이지 리셋
                    currentTarget = null;
                    currentHoldTime = 0f;
                    waitForKeyRelease = true; // 다음 프레임부터 키를 뗄 때까지 상호작용 방지
                }
            }
            else
            {
                // 키를 떼면 게이지 초기화
                if (currentHoldTime > 0f)
                {
                    currentHoldTime = 0f;
                    currentTarget.SetHoldProgress(0f);
                }
            }
        }
        else
        {
            currentHoldTime = 0f;
        }
    }

    // 최적화: 매 프레임 배열 할당을 막기 위한 버퍼 생성
    private Collider[] hitColliders = new Collider[32];

    /// <summary>
    /// 플레이어 반경 내에 있는 모든 IInteractable 오브젝트를 찾고, 
    /// 가장 가까운 단 하나의 대상만 현재 타겟(currentTarget)으로 설정합니다.
    /// </summary>
    void FindClosestInteractable()
    {
        // 최적화 1: OverlapSphereNonAlloc을 사용하여 가비지 컬렉터(GC) 스파이크 방지
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, interactionRadius, hitColliders);
        
        IInteractable closest = null;
        float minSqrDistance = float.MaxValue; // 최적화 2: Sqrt 연산을 피하기 위해 sqrMagnitude 사용

        Vector3 currentPos = transform.position;

        // 1. 가장 가까운 대상 찾기
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitColliders[i];
            IInteractable interactable = hit.GetComponent<IInteractable>();
            
            if (interactable != null)
            {
                // 이미 열려있는 상자는 타겟 후보에서 제외 (아이템 등을 주울 수 있도록)
                box_defualt box = hit.GetComponent<box_defualt>();
                if (box != null && box.isOpen)
                    continue;

                float sqrDist = (currentPos - interactable.GetTransform().position).sqrMagnitude;
                if (sqrDist < minSqrDistance)
                {
                    minSqrDistance = sqrDist;
                    closest = interactable;
                }
            }
        }

        // 최적화 3: 불필요한 '방어 코드(모든 대상 강제 해제)' 루프 제거.
        // 어차피 아래의 타겟 변경 처리 로직에서 이전 타겟을 정확히 해제해주기 때문입니다.

        // 2. 타겟 변경 처리 (반경 밖으로 벗어난 기존 타겟 처리 포함)
        if (currentTarget != closest)
        {
            if (currentTarget != null) currentTarget.SetTargeted(false);
            currentTarget = closest;
            if (currentTarget != null) currentTarget.SetTargeted(true);
        }
    }

    // 에디터에서 상호작용 반경을 눈으로 보기 위한 기즈모
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
