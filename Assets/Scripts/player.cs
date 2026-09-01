using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    public float speed;
    [SerializeField] private float rotationSpeed = 720f;
    float hAxis;
    float vAxis;
    bool Ddown;

    Vector3 dodogeVec;
    bool isDodging;

    Vector3 moveVec;
    Vector3 lookDirection;  // 마우스를 향한 시선 방향

    private Camera mainCam;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCam = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        GetInput();
        Move();
        HandleRotation();
        Dodge();
    }
    void GetInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            hAxis = 0f;
            vAxis = 0f;
            Ddown = false;
            return;
        }

        hAxis = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        vAxis = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        Ddown = keyboard.spaceKey.wasPressedThisFrame;
    }
    void Move()
    {
        if (isDodging)
        {
            // 구르기 중에는 방향 고정 — 입력 무시
            moveVec = dodogeVec;
        }
        else
        {
            // 시선과 무관하게 월드 고정 방향으로 이동
            // W(위), S(아래), A(왼쪽), D(오른쪽)
            moveVec = new Vector3(hAxis, 0, vAxis).normalized;
        }

        transform.position += moveVec * speed * Time.deltaTime;
    }
    void HandleRotation()
    {
        // 인벤토리가 열려 있으면 시선 처리를 멈춤
        if (inventory.Instance != null && inventory.Instance.isInventoryOpen) return;

        if (isDodging)
        {
            // 구르기 중에는 구르기 방향을 바라봄
            lookDirection = dodogeVec;
        }
        else
        {
            // 마우스 커서 위치를 바라봄
            lookDirection = GetMouseWorldDirection();
        }

        if (lookDirection.sqrMagnitude < 0.01f) return;
 
        Quaternion targetRot = Quaternion.LookRotation(lookDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 마우스 커서의 월드 위치를 구해 플레이어→마우스 방향을 반환
    /// </summary>
    Vector3 GetMouseWorldDirection()
    {
        if (mainCam == null || Mouse.current == null)
            return transform.forward;

        Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 mouseWorldPos = ray.GetPoint(distance);
            Vector3 dir = mouseWorldPos - transform.position;
            dir.y = 0f;
            return dir.normalized;
        }

        return transform.forward;
    }

    void Dodge()
    {
        if (Ddown && !isDodging)
        {
            dodogeVec = moveVec;
            isDodging = true;
            speed *= 1.6f;

            Invoke("DodgeOut", 0.3f);
        }
    }
    void DodgeOut()
    {
        speed *= 0.625f;
        isDodging = false;
    }
}
