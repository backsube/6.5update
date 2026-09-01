# NOUMENON 협업 에이전트 지침

이 파일은 `Version-6.5` 브랜치를 받은 디자이너와 해당 디자이너의 Codex agent를 위한 작업 기준이다.

## 역할과 우선순위

주요 작업 범위는 레벨 디자인, 맵 구성, 조명, 머티리얼, VFX, 환경 프리팹과 3D 에셋 임포트다. 기존 게임플레이 코드는 디자인 작업에 꼭 필요한 경우가 아니면 수정하지 않는다. 변경 전에 `Docs/DEVELOPMENT_STATUS.md`와 `Docs/GAME_PRD.md`를 읽는다.

## 반드시 지킬 사항

1. Unity **6000.5.10f1**과 URP를 사용한다.
2. 주 작업 씬은 `Assets/Scenes/parkseongbin.unity`다. 대규모 변경 전 씬 복사본 또는 별도 작업 씬을 만들고, 승인된 결과만 주 씬에 반영한다.
3. 기존 Player, Camera, EventSystem, UI canvas, Inventory, SessionManager 및 적 AI 오브젝트의 스크립트 참조를 끊지 않는다.
4. 에셋 파일과 `.meta`를 항상 같이 이동·커밋한다. Unity 밖에서 `.meta`를 임의로 재생성하거나 GUID를 바꾸지 않는다.
5. 3D 에셋은 가능하면 `Assets/Art/Environment`, `Assets/Art/Characters`, `Assets/Art/Props`, `Assets/Art/VFX` 아래에 분류한다. 기존 폴더를 이동할 때는 참조 손상 여부를 먼저 확인한다.
6. 원본 FBX/텍스처는 보존하고, 씬 배치는 Prefab Variant를 우선한다. 동일 오브젝트를 씬에 반복 복제하기보다 프리팹화한다.
7. URP/Lit 호환 머티리얼을 사용한다. 분홍색 셰이더, 누락 텍스처, Missing Script가 없는지 확인한다.
8. NavMesh를 바꾸는 지형·장애물 작업 후 적 이동과 추적을 다시 베이크/검증한다.
9. UI 그래픽과 TMP 텍스트는 상호작용 대상이 아니라면 `Raycast Target`을 끈다. 특히 `BACKPACK` 라벨이 첫 인벤토리 슬롯 입력을 막았던 문제가 재발하지 않게 한다.
10. `Library`, `Temp`, `Logs`, `UserSettings`, IDE 파일은 커밋하지 않는다.

## 씬/맵 작업 완료 조건

- Console 컴파일 오류 0개, Missing Script 0개
- Player Spawn에서 이동·조준·사격 가능
- `F` 상호작용, 상자 열기, 아이템 루팅 가능
- `Tab` 인벤토리 및 첫 번째 슬롯 전체 영역 드래그 가능
- 적이 NavMesh 위에서 감지·추적·공격하며 사망 후 루팅 대상으로 전환
- 카메라 클리핑, 플레이 불가 낙하 지점, 막힌 동선 없음
- 머티리얼/라이트맵/텍스처 참조 누락 없음
- 변경된 씬, 프리팹, 에셋과 모든 `.meta`가 Git에 포함됨

## 권장 커밋 단위

- `art: import <asset pack or prop set>`
- `level: block out <area name>`
- `lighting: tune <scene or zone>`
- `fix: repair <prefab/material/reference>`

한 커밋에 코드 변경과 대량 바이너리 에셋 변경을 섞지 않는다. 큰 FBX/텍스처를 추가하기 전에 저장소 용량과 Git LFS 정책을 확인한다.

