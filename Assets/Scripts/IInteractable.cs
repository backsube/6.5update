using UnityEngine;

/// <summary>
/// 플레이어가 상호작용할 수 있는 모든 오브젝트(아이템, 상자, NPC 등)가 
/// 공통으로 가져야 하는 규약을 정의한 인터페이스입니다.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 플레이어가 상호작용 키(E)를 눌렀을 때 실행될 로직을 구현합니다.
    /// </summary>
    /// <param name="interactor">상호작용을 시도한 주체(주로 플레이어 GameObject)</param>
    void Interact(GameObject interactor);

    /// <summary>
    /// 이 오브젝트가 플레이어의 상호작용 타겟으로 지정되었는지(UI 표시 등) 상태를 설정합니다.
    /// </summary>
    /// <param name="targeted">타겟 지정 여부</param>
    void SetTargeted(bool targeted);

    /// <summary>
    /// 거리 계산 등을 위해 이 오브젝트의 Transform을 반환합니다.
    /// </summary>
    Transform GetTransform();

    /// <summary>
    /// 상호작용(F키)을 꾹 눌러야 하는 시간을 반환합니다. (0이면 즉시 실행)
    /// </summary>
    float GetHoldDuration();

    /// <summary>
    /// 플레이어가 상호작용 키를 누르고 있는 진행도(0.0 ~ 1.0)를 전달받습니다.
    /// 원형 게이지 등 UI 표시에 사용됩니다.
    /// </summary>
    /// <param name="progress">현재 진행도 (0.0 ~ 1.0)</param>
    void SetHoldProgress(float progress);
}
