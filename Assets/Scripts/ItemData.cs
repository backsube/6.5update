using UnityEngine;

/// <summary>
/// 게임 내 아이템의 고정 데이터(이름, 아이콘, 크기, 무게 등)를 정의하는 ScriptableObject입니다.
/// 유니티 에디터에서 에셋 형태로 여러 종류의 아이템을 쉽게 찍어낼 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "New Item Data", menuName = "Noumenon/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("인게임 및 UI에 표시될 아이템의 이름입니다.")]
    public string itemName;
    
    [Tooltip("인벤토리 슬롯에 표시될 아이템 이미지입니다.")]
    public Sprite icon;
    
    [Header("물리적 특성")]
    [Tooltip("인벤토리 그리드에서 차지하는 가로 칸 수입니다.")]
    public int width = 1;
    
    [Tooltip("인벤토리 그리드에서 차지하는 세로 칸 수입니다.")]
    public int height = 1;
    
    [Tooltip("아이템의 무게입니다. 중량 시스템(페널티 등)에 사용됩니다.")]
    public float weight = 1f;
    
    [Header("가치")]
    [Tooltip("상점 거래나 정산 시 기준이 되는 아이템의 가격(솔라늄 가치)입니다.")]
    public int price = 0;
}
