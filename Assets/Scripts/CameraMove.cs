using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMove : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;

    [Header("마우스 추적 설정")]
    [Tooltip("카메라가 마우스 방향으로 이동하는 최대 거리")]
    [SerializeField] private float mouseFollowDistance = 3f;
    [Tooltip("카메라 이동 부드러움 (작을수록 부드러움)")]
    [SerializeField] private float smoothSpeed = 5f;

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (target == null || mainCam == null || Mouse.current == null) return;

        // 마우스 화면 좌표 → 월드 평면 위의 좌표로 변환
        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mouseScreenPos);
        Plane groundPlane = new Plane(Vector3.up, target.position);

        Vector3 mouseWorldPos = target.position;
        if (groundPlane.Raycast(ray, out float distance))
        {
            mouseWorldPos = ray.GetPoint(distance);
        }

        Vector3 clampedOffset = Vector3.zero;

        // 인벤토리가 열려있지 않을 때만 마우스 방향으로 카메라 오프셋 적용
        if (inventory.Instance == null || !inventory.Instance.isInventoryOpen)
        {
            Vector3 dirToMouse = mouseWorldPos - target.position;
            clampedOffset = Vector3.ClampMagnitude(dirToMouse, mouseFollowDistance);
        }

        // 최종 카메라 위치 = 플레이어 + 기본 오프셋 + 마우스 방향 오프셋
        Vector3 desiredPos = target.position + offset + clampedOffset * 0.5f;
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
    }
}
