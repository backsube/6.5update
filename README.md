# NOUMENON — Version 6.5

Unity 6로 마이그레이션한 NOUMENON 개발 브랜치입니다. 원본 `park` 브랜치의 게임플레이와 `parkseongbin` 씬을 기반으로 Unity 6 호환성, 인벤토리/루팅 UI, 전투 및 적 AI를 정비했습니다.

## 시작하기

- Unity Editor: **6000.5.10f1**
- Render Pipeline: **Universal Render Pipeline 17.5.0**
- Input: **Input System 1.20.0** (`Active Input Handling: Input System Package`)
- 주 작업 씬: `Assets/Scenes/parkseongbin.unity`
- Build Settings 등록 씬: `parkseongbin`, `SampleScene`, `test`

Unity Hub에서 이 저장소의 루트 폴더를 프로젝트로 추가한 뒤 정확한 에디터 버전으로 여십시오. 최초 실행 시 `Library` 재생성 때문에 시간이 걸릴 수 있습니다.

## 현재 플레이 조작

- 이동: `WASD`
- 조준: 마우스 / 우클릭
- 사격: 좌클릭
- 재장전: `R`
- 상호작용: `F` 길게 누르기
- 인벤토리: `Tab`
- UI 닫기: `Esc` (해당 UI에서 지원되는 경우)

## 협업 문서

- [개발 현황](Docs/DEVELOPMENT_STATUS.md)
- [게임 개발 PRD](Docs/GAME_PRD.md)
- [Codex/디자이너 작업 규칙](AGENTS.md)

## 버전 관리 원칙

Unity 생성 폴더(`Library`, `Temp`, `Logs`, `UserSettings`)와 IDE 생성 파일은 커밋하지 않습니다. 에셋을 추가하거나 이동할 때 Unity가 생성한 `.meta` 파일을 반드시 함께 커밋하십시오.


