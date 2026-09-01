using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;


/// <summary>
/// Phase 0 — 기본 총기 시스템
/// 부착 위치: player 오브젝트
/// 
/// [기능]
/// - 좌클릭: 사격 (Raycast)
/// - 우클릭 홀드: ADS (조준) → CrosshairUI 연동
/// - R키: 재장전 (딜레이 있음)
/// - 탄약 카운터 UI 출력
/// 
/// [Phase 0 제외 범위]
/// - 총기 교체, 부위별 데미지, 탄착 파티클
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("사격 설정")]
    [SerializeField] private float fireRate = 0.45f;       // 발사 간격 (초) - 권총 느낌으로 0.45초
    [SerializeField] private float range = 50f;            // 사격 사거리
    [SerializeField] private int damage = 18;              // 기본 데미지
    [SerializeField] private float hipSpreadAngle = 1.0f;  // 비조준 시 탄퍼짐 각도 (도) — 탑다운 특성상 작아야 정상 느낌
    [SerializeField] private float adsSpreadAngle = 0.0f;  // 조준 시 탄퍼짐 각도 (도) — 완전 정밀
    [SerializeField] private float bulletSpeed = 25f;      // 총알 시각 이동 속도 (낮을수록 눈에 보임)

    [Header("탄약 설정")]
    [SerializeField] private int magazineSize = 12;        // 탄창 용량 - 권총 탄창 12발
    [SerializeField] private int reserveAmmo = 36;         // 예비 탄약
    [SerializeField] private float reloadTime = 1.5f;      // 재장전 시간 - 권총 빠른 재장전 1.5초

    [Header("레이어 설정")]
    [SerializeField] private LayerMask hitLayerMask = ~0;  // 맞출 수 있는 레이어 (기본: 전부)

    [Header("UI 연결")]
    [SerializeField] private CrosshairUI crosshairUI;      // player에 붙은 CrosshairUI
    [SerializeField] private TextMeshProUGUI ammoText;     // 탄약 표시 텍스트

    // 내부 상태
    private int currentAmmo;
    private bool isReloading = false;
    private float nextFireTime = 0f;
    private Camera mainCam;
    private LineRenderer laserSight;

    void Start()
    {
        mainCam = Camera.main;
        currentAmmo = magazineSize;
        UpdateAmmoUI();

        // 조준선(Laser Sight) LineRenderer 설정
        laserSight = gameObject.AddComponent<LineRenderer>();
        laserSight.material = new Material(Shader.Find("Sprites/Default"));
        laserSight.startWidth = 0.015f;
        laserSight.endWidth = 0.015f;
        laserSight.startColor = new Color(1f, 0f, 0f, 0.4f); // 반투명 빨간색
        laserSight.endColor = new Color(1f, 0f, 0f, 0.05f); // 끄트머리는 투명하게 페이드
        laserSight.useWorldSpace = true;
        laserSight.positionCount = 2;
        laserSight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laserSight.receiveShadows = false;
        laserSight.enabled = false;
    }

    void Update()
    {
        // 인벤토리(상자 포함)가 열려있으면 총기 조작 제한
        if (inventory.Instance != null && inventory.Instance.isInventoryOpen)
        {
            if (laserSight != null) laserSight.enabled = false;
            if (crosshairUI != null) crosshairUI.SetADS(false);
            return;
        }

        HandleADS();
        HandleFire();
        HandleReload();
        UpdateLaserSight();
    }

    // ──────────────────────────────────────────────
    //  ADS (조준)
    // ──────────────────────────────────────────────
    void HandleADS()
    {
        bool isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (crosshairUI != null)
            crosshairUI.SetADS(isAiming);
    }

    void UpdateLaserSight()
    {
        if (laserSight == null) return;

        bool isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;
        
        if (isAiming && !isReloading)
        {
            laserSight.enabled = true;
            
            // 1. 마우스 월드 좌표 (플레이어 지면 평면 기준)
            Plane groundPlane = new Plane(Vector3.up, transform.position);
            if (mainCam == null || Mouse.current == null) return;
            Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Vector3 targetWorldPos;

            if (groundPlane.Raycast(ray, out float enterDist))
            {
                targetWorldPos = ray.GetPoint(enterDist);
            }
            else
            {
                targetWorldPos = ray.origin + ray.direction * range;
            }

            // 2. 플레이어 눈높이 시작점 및 수평 조준선 타겟 계산
            Vector3 startPos = transform.position + Vector3.up * 0.5f;
            Vector3 targetPlanePos = targetWorldPos;
            targetPlanePos.y = startPos.y;

            Vector3 shootDir = (targetPlanePos - startPos).normalized;
            Vector3 castStart = startPos + shootDir * 0.7f; // 자기 자신 충돌 방지 오프셋
            Vector3 endPos;

            // 수평 구도로 충돌 지점 계산
            if (Physics.Raycast(castStart, shootDir, out RaycastHit hit, range, hitLayerMask, QueryTriggerInteraction.Ignore))
            {
                endPos = hit.point;
            }
            else
            {
                endPos = startPos + shootDir * range;
            }

            laserSight.SetPosition(0, startPos);
            laserSight.SetPosition(1, endPos);
        }
        else
        {
            laserSight.enabled = false;
        }
    }

    // ──────────────────────────────────────────────
    //  사격
    // ──────────────────────────────────────────────
    void HandleFire()
    {
        // 재장전 중이거나 탄약 없으면 사격 불가
        if (isReloading) return;

        // GetMouseButtonDown: 클릭당 1발 (권총 = 반자동). 쿨타임은 빠른 연사 방지
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextFireTime)
        {
            if (currentAmmo <= 0)
            {
                // 탄약 없으면 자동 재장전 시도
                TryReload();
                return;
            }

            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        currentAmmo--;
        UpdateAmmoUI();

        // 1. 마우스 월드 좌표 (플레이어 지면 평면 기준)
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (mainCam == null || Mouse.current == null) return;
        Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 targetWorldPos;

        if (groundPlane.Raycast(ray, out float enterDist))
        {
            targetWorldPos = ray.GetPoint(enterDist);
        }
        else
        {
            targetWorldPos = ray.origin + ray.direction * range;
        }

        // 2. 플레이어 눈높이 시작점 및 수평 발사 방향 계산
        Vector3 startPos = transform.position + Vector3.up * 0.5f;
        Vector3 targetPlanePos = targetWorldPos;
        targetPlanePos.y = startPos.y;

        Vector3 shootDir = (targetPlanePos - startPos).normalized;

        // 3. 조준 상태 여부에 따른 탄퍼짐 적용
        bool isAiming = Mouse.current.rightButton.isPressed;
        float currentSpread = isAiming ? adsSpreadAngle : hipSpreadAngle;

        if (currentSpread > 0f)
        {
            Quaternion spreadRotation = Quaternion.Euler(
                Random.Range(-currentSpread, currentSpread),
                Random.Range(-currentSpread, currentSpread),
                0f
            );
            shootDir = spreadRotation * shootDir;
        }

        // 4. 총알 발사 판정 (플레이어 자신 충돌 방지를 위해 약간 앞에서 발사)
        Vector3 castStart = startPos + shootDir * 0.7f;
        Vector3 trailEnd;

        // SphereCast를 사용하여 플레이어 평면에서 수평으로 총알 발사. 트리거(시체 등)는 무시하고 관통.
        if (Physics.SphereCast(castStart, 0.25f, shootDir, out RaycastHit hit, range, hitLayerMask, QueryTriggerInteraction.Ignore))
        {
            trailEnd = hit.point;

            // 적중한 대상이 적(EnemyBase)이면 데미지 적용 (자식 콜라이더 포함)
            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
            if (enemy != null) enemy.TakeDamage(damage);
        }
        else
        {
            trailEnd = startPos + shootDir * range;
        }

        // 탄도 시각화 — BulletTrail이 start→end 까지 이동하며 총알 표현
        BulletTrail.Spawn(startPos, trailEnd, bulletSpeed);
    }

    // ──────────────────────────────────────────────
    //  재장전
    // ──────────────────────────────────────────────
    void HandleReload()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !isReloading)
        {
            TryReload();
        }
    }

    void TryReload()
    {
        // 예비 탄약 없거나 이미 탄창이 가득 차면 재장전 불필요
        if (reserveAmmo <= 0 || currentAmmo >= magazineSize) return;

        isReloading = true;
        Debug.Log($"[WeaponController] 재장전 시작... ({reloadTime}초)");

        Invoke(nameof(FinishReload), reloadTime);
    }

    void FinishReload()
    {
        int needed = magazineSize - currentAmmo;
        int taken = Mathf.Min(needed, reserveAmmo);

        currentAmmo += taken;
        reserveAmmo -= taken;

        isReloading = false;
        Debug.Log($"[WeaponController] 재장전 완료. 탄약: {currentAmmo}/{reserveAmmo}");
        UpdateAmmoUI();
    }

    // ──────────────────────────────────────────────
    //  UI 갱신
    // ──────────────────────────────────────────────
    void UpdateAmmoUI()
    {
        if (ammoText == null) return;

        if (isReloading)
            ammoText.text = "재장전 중...";
        else
            ammoText.text = $"{currentAmmo} / {reserveAmmo}";
    }

    // ──────────────────────────────────────────────
    //  외부 접근용 프로퍼티 (향후 인벤토리 연동용)
    // ──────────────────────────────────────────────
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;

    /// <summary>
    /// 탄약 보충 (루팅 시스템 연동용 — Phase 1)
    /// </summary>
    public void AddAmmo(int amount)
    {
        reserveAmmo += amount;
        UpdateAmmoUI();
    }
}
