# Project S Verification Backlog

## 문서 목적

이 문서는 구현은 완료했지만 나중에 한꺼번에 검증할 항목을 누적해서 관리하기 위한 체크리스트다.

앞으로 기능 구현 중 즉시 검증하지 못했거나, 실제 Unity Editor/PlayMode/Headless 환경에서 별도 확인이 필요한 항목은 이 문서에 추가한다.

## 검증 대기 항목

### Unity 테스트 실행 환경

- [ ] Unity Headless PlayMode 테스트가 테스트 러너에 진입하지 못하고 즉시 종료되는 원인을 확인한다.
- [ ] 기존 Unity 프로세스, 프로젝트 잠금, 사용자 캐시 DB, 라이선스 연결 상태가 테스트 실행을 막는지 확인한다.
- [ ] `Temp/playmode-results.xml` 결과 파일이 정상 생성되는지 확인한다.

### 승패 판정

- [ ] 유닛이 모두 사망해도 해당 팀 건물이 남아 있으면 경기가 종료되지 않는지 Unity PlayMode에서 확인한다.
- [ ] 상대 팀의 모든 완성 건물이 파괴되었을 때만 승리/패배가 확정되는지 Unity PlayMode에서 확인한다.
- [ ] 경기 종료 후 AI, 생산 큐, 플레이어 명령 컨트롤러가 기존처럼 정지되는지 Unity PlayMode에서 확인한다.

### 유닛 명령 생명주기

- [ ] 여러 유닛 선택 후 `Move`, 우클릭 이동, `AttackMove`, `FocusAttack`, `Patrol` 명령을 연속 입력했을 때 마지막 명령만 수행되는지 확인한다.
- [ ] 이동 중 새 명령을 빠르게 입력했을 때 이전 경로 결과가 뒤늦게 적용되지 않는지 확인한다.
- [ ] 건설/채집 중 이동, 정지, 공격 명령을 내렸을 때 이전 상호작용이 계속 실행되지 않는지 확인한다.
- [ ] 이동 또는 명령 수행 완료 후 `Mode`, `ActionState`, `LatestCommand`가 모두 정지 상태로 정리되는지 확인한다.

### 정지 상태 자동 전투

- [ ] `Idle` 상태 유닛이 감지 거리 내 적을 발견하면 공격 상태로 전환되는지 확인한다.
- [ ] `Idle` 상태 유닛이 공격 사거리 밖, 감지 거리 안의 적을 발견하면 추격을 시작하는지 확인한다.
- [ ] `Idle` 상태 유닛이 추격 중이던 적이 감지 거리 밖으로 벗어나면 `Idle`로 복귀하는지 확인한다.
- [ ] `HoldPosition` 상태 유닛이 감지 거리 내 적을 확인하되, 공격 사거리 밖의 적은 추격하지 않는지 확인한다.
- [ ] `HoldPosition` 상태 유닛이 공격 사거리 안의 적에게는 공격 상태로 진입하는지 확인한다.
- [ ] 적이 감지 범위 밖으로 벗어나거나 사망했을 때 정지 상태로 자연스럽게 복귀하는지 확인한다.

### 전투 명령 상태 정책

- [ ] `AttackMove` 중 감지한 적이 사망하거나 감지 거리 밖으로 벗어나면 원래 목적지 이동을 재개하는지 확인한다.
- [ ] `AttackMove` 목적지에 도착하고 교전 대상이 없으면 `Idle`로 정리되는지 확인한다.
- [ ] `Patrol` 중 감지한 적이 사망하거나 감지 거리 밖으로 벗어나면 순찰 경로로 복귀하는지 확인한다.
- [ ] `FocusAttack`은 감지 거리 밖의 지정 대상도 추격하고, 대상이 사망하거나 사라지면 `Idle`로 정리되는지 확인한다.
- [ ] 자동 반격은 `Idle`에서는 감지 거리 안의 공격자에게만 반응하고, `HoldPosition`에서는 공격 사거리 안의 공격자에게만 반응하는지 확인한다.

### 목적지 분산 및 점유 타일

- [ ] 우클릭 이동, Move UI, `AttackMove`, `FocusAttack`, `Patrol`, `Interact` 모두 목적지에서 유닛이 겹치지 않는지 확인한다.
- [ ] 이동 중인 아군은 점유 장애물로 취급하지 않고, 도착/공격 정지 상태의 아군만 점유 장애물로 반영되는지 확인한다.
- [ ] 유닛의 x/y footprint 값이 1x1보다 큰 경우에도 목적지 후보와 점유 타일 계산이 올바른지 확인한다.
- [ ] 공격을 위해 멈춰 있는 유닛이 점유 타일 장애물로 반영되어 다른 아군의 최종 위치와 겹치지 않는지 확인한다.

### Obstacle 경로 차단

- [ ] Unity Editor에서 `Tools > Project S > Tilemaps > Prepare Obstacle Tile Palette`를 실행하거나 스크립트 리컴파일 뒤 `Assets/Assets/Tilemaps/Obstacle Tiles/Obstacle_Stone.asset`이 생성되고, Walkable/Buildable false, 모든 차단 옵션 true, Grid Collider 설정인지 확인한다.
- [ ] `Tools > Project S > Tilemaps > Add Obstacle Tilemap To Active Scene`로 생성한 `Obstacle` Tilemap이 Grid 하위와 FrontProps 정렬에 배치되고, Obstacle 레이어가 존재할 때만 해당 레이어를 사용하는지 확인한다.
- [ ] PlayMode에서 이름이 `Obstacle`인 Tilemap에 타일을 추가·삭제했을 때 경로 탐색, 건설 가능 판정, 미니맵 차단 색상이 즉시 갱신되는지 확인한다.
- [ ] `NamedObstacleTilemapChanges_RefreshNavigationAndMinimapTerrain` PlayMode 테스트를 Unity Test Runner에서 실행한다.
- [ ] Obstacle 레이어 타일맵이 A* 경로 계산에서 이동 불가 셀로 처리되는지 확인한다.
- [ ] 유닛이 실제 이동 중에도 Obstacle 지역을 지나가거나 스치듯 통과하지 않는지 확인한다.
- [ ] 대각선 이동 시 Obstacle 모서리 사이를 끼고 통과하지 않는지 확인한다.
- [ ] 목적지 후보 fallback이 Obstacle 또는 점유 footprint와 충돌하는 위치를 선택하지 않는지 확인한다.
- [ ] `Navigator_PathAvoidsObstacleLayerTilemapCells` PlayMode 테스트를 Unity Test Runner에서 실행한다.
- [ ] `Navigator_DiagonalMoveBetweenObstacleCorners_IsRejected` PlayMode 테스트를 Unity Test Runner에서 실행한다.
- [ ] `UnitPathAgent_MoveTo_DoesNotEnterObstacleLayerTilemapCells` PlayMode 테스트를 Unity Test Runner에서 실행한다.
- [ ] `UnitPathAgent_FallbackDestinationSkipsObstacleAndOccupiedFootprint` PlayMode 테스트를 Unity Test Runner에서 실행한다.

### 이동감 및 성능

- [ ] 다수 유닛 동시 이동 시 `UnitPathRequestScheduler`가 프레임당 경로 요청 처리량을 제한하는지 확인한다.
- [ ] 경로 요청 큐가 몰려도 유닛 이동이 버벅이거나 첫 이동 방향이 흔들리지 않는지 확인한다.
- [ ] Tilemap 샘플 캐시와 A* binary heap 적용 후 경로 탐색 프레임 비용이 줄었는지 비교한다.
- [ ] 이동 속도가 목적지 근처와 경로 중간에서 일정하게 유지되는지 확인한다.
- [ ] `UnitPathRequestScheduler` 디버그 오버레이 또는 로그로 pending/peak queue, frame/total completed/failed/discarded, fallback attempts, pathfinding ms, queue wait frames를 확인한다.
- [ ] 다수 유닛 동시 명령 시 `ProjectSTilemapWorld`의 cache rebuild 수가 불필요하게 증가하지 않고, sample hit/miss 비율이 안정적인지 확인한다.
- [ ] 목적지 점유로 fallback 후보가 많이 발생하는 상황에서 queue wait frames와 pathfinding ms가 과도하게 튀지 않는지 확인한다.

### 보급 및 생산 제한

- [ ] 본진이 제공하는 보급량과 현재 유닛 사용량이 HUD에 `현재 / 최대`로 정확히 표시되는지 확인한다.
- [ ] 최대 보급량에 도달했을 때 생산 큐가 자원을 소비하지 않고 보급 부족 사유를 표시하는지 확인한다.
- [ ] 대기 및 진행 중 생산을 취소하면 예약 보급량과 자원이 모두 즉시 반환되는지 확인한다.
- [ ] 생산 완료와 유닛 사망 또는 비활성화 뒤 현재 사용 보급량이 실제 활성 유닛 수와 일치하는지 확인한다.

### 공격 목표 우선순위

- [ ] 동일 감지 거리의 후보가 있을 때 최근 자신을 공격한 적, 전투 유닛, 작업 유닛, 방어 건물, 생산 건물, 본진 순으로 선택되는지 확인한다.
- [ ] 명시적 `FocusAttack` 대상은 더 높은 자동 표적 후보가 감지되어도 유지되는지 확인한다.
- [ ] `AttackMove`가 우선 표적 교전 후 대상 사망 또는 감지 이탈 시 원래 목적지로 이동을 재개하는지 확인한다.
- [ ] AI 공격 부대가 방어/생산 건물과 본진을 우선순위에 맞춰 선택하는지 확인한다.

### 미니맵 및 전장 정보

- [ ] 미니맵이 Tilemap 실제 셀 범위와 맞게 표시되고, 아군/적군 유닛과 건물이 식별 가능한 색으로 갱신되는지 확인한다.
- [ ] 미니맵 지형 캐시가 이동 가능, 이동 불가, 이동 가능하지만 건설 불가, 맵 외부 영역을 서로 다른 색으로 표시하며 실제 타일 형태와 일치하는지 확인한다.
- [ ] 미니맵에서 미네랄·가스, 선택 유닛, 현재 공격 중인 대상의 표시가 기존 유닛·건물·카메라 뷰포트와 겹쳐도 식별 가능한지 확인한다.
- [ ] 타일 또는 장애물 변경으로 `ProjectSTilemapWorld` 내비게이션 캐시가 갱신될 때만 미니맵 지형 텍스처가 다시 생성되고, 일반 플레이 중 프레임 드롭이 없는지 확인한다.
- [ ] 카메라 뷰포트 표시가 줌과 이동에 맞춰 갱신되는지 확인한다.
- [ ] 미니맵 좌클릭이 카메라만 이동시키며 유닛 선택, 이동, 공격 명령으로 중복 처리되지 않는지 확인한다.

### 유닛 텍스처 색상

- [ ] Unity Editor에서 `Tools > Project S > Apply Baked Unit Textures`를 실행해 모든 `B_*` 프리팹이 `Textures/B_*.png` 스프라이트를 참조하고 본체 `SpriteRenderer.color`가 흰색인지 확인한다.
- [ ] PlayMode에서 아군·적군 유닛에 팀 색상 마커가 추가되지 않고, 유닛별 텍스처 색상과 선택/체력 UI가 정상 표시되는지 확인한다.

### 광역 공격 및 상성

- [ ] 광역 공격 유닛이 주 대상과 반경 안의 적 유닛·건물만 최대 대상 수까지 동시에 피해를 주는지 확인한다.
- [ ] 주 대상이 광역 반경 밖에 있거나 아군·중립 대상이 반경 안에 있어도 잘못된 추가 피해가 발생하지 않는지 확인한다.
- [ ] Soldier-Spliter-Ranger 및 Tank-Striker-Swarm 상성에서 유리 상성 125%, 불리 상성 80%, 그 외 100% 피해가 적용되는지 확인한다.

### 보급 건물 확장

- [ ] Supply Depot 선택 후 타일 배치, 건설 완료, 파괴/비활성화까지 최대 보급량이 10만큼 정확히 증감하는지 확인한다.
- [ ] 보급 부족 상태에서 AI가 한 번만 Supply Depot 건설을 요청하고, 완료 뒤 생산을 정상 재개하는지 확인한다.
- [ ] Supply Depot 건설에 미네랄 100과 건설 시간 6초가 적용되며 잘못된 타일에서는 자원이 차감되지 않는지 확인한다.

### AI 기지 운영 및 방어

- [ ] AI가 전투 생산 건물을 잃었을 때 작업 유닛으로 새 건설 부지를 만들고 건설을 완료하는지 확인한다.
- [ ] AI 주요 건물 반경 안에 적 전투 유닛이 진입하면 AI 전투 유닛이 해당 위협을 우선 공격하는지 확인한다.
- [ ] 건설 대기 중인 AI 작업 유닛이 자원 채집 명령에서 건설 명령으로 전환되고, 건설 완료 뒤 정상 채집 루프로 복귀하는지 확인한다.

### 쉬운 AI 병력 구성 및 연구

- [ ] AI가 전투 유닛 5기 전에는 Soldier만 생산하고, 이후 성공 생산 5회마다 Ranger와 Striker를 번갈아 시도하는지 확인한다.
- [ ] Ranger 또는 Striker 생산이 자원·보급·생산 조건 때문에 실패하면 Soldier 생산으로 안전하게 복귀하고, Tank·Spliter·Swarm은 기본 조합에 포함하지 않는지 확인한다.
- [ ] 최초 90초와 전투 유닛 7기 조건 전에는 공격하지 않고, 이후 공격 명령 사이에도 18초 이상의 간격을 유지하는지 확인한다.
- [ ] 전투 유닛 6기와 연구비 외 미네랄 200, 가스 50을 확보한 경우에만 Weapon Calibration 이후 Mobility Tuning을 한 번씩 연구하는지 확인한다.
- [ ] `SimpleSkirmishAiEasyPlayModeTests`를 Unity Test Runner에서 실행한다.

### 생산 조건 시스템

- [ ] 조건이 없는 기존 생산 정의가 이전과 동일하게 생산 가능한지 확인한다.
- [ ] 나중에 `CompletedBuilding` 조건을 설정한 정의만 요구 건물이 완성되기 전 생산을 거부하고, 자원·보급 예약을 소비하지 않는지 확인한다.
- [ ] 조건을 만족한 뒤 생산 버튼과 생산 큐가 즉시 다시 사용 가능한지 확인한다.

### 자원 거점 확장

- [ ] 확장 자원 지대의 미네랄과 가스가 기본 시작 지대와 겹치지 않고 생성되는지 확인한다.
- [ ] Resource Drop-off 및 확장 Main Base를 건설한 뒤 일꾼이 가장 가까운 살아 있는 반납처로 복귀하는지 확인한다.
- [ ] 반납처가 파괴되거나 비활성화되면 일꾼이 남아 있는 가까운 반납처를 다시 선택하는지 확인한다.
- [ ] AI가 확장 자원 지대 근처에 확장 본진을 건설하고 해당 거점을 실제 채집에 활용하는지 확인한다.

### 전술 명령 및 생산 중단

- [ ] Move, Attack Move, Patrol, Hold, Stop의 HUD 버튼과 단축키가 모두 같은 명령 상태로 전환되는지 확인한다.
- [ ] 선택 유닛이 여러 명일 때 HUD가 단일 명령/상태 또는 혼합 상태를 올바르게 표시하는지 확인한다.
- [ ] 생산 건물의 Rally 입력이 지도 클릭 한 번만 소비하고, 이후 생산 유닛이 지정 위치로 이동하는지 확인한다.
- [ ] 생산 건물이 파괴되거나 비활성화되면 활성·대기 생산의 비용과 보급 예약이 한 번만 반환되고 HUD가 더 이상 해당 큐를 표시하지 않는지 확인한다.

### 전술 명령 HUD 및 랠리

- [ ] Unity PlayMode에서 `M`, `A`, `P`, `H`, `S` 단축키와 HUD 버튼이 동일한 대기 명령 안내 및 유닛 `Mode`/`ActionState` 표시를 제공하는지 확인한다.
- [ ] 생산 건물 선택 후 현재 랠리 좌표가 표시되고, Rally 버튼 뒤 지도 또는 미니맵 클릭이 해당 좌표를 즉시 갱신하는지 확인한다.
- [ ] 생산 및 B 건설 메뉴에서 `Q/E/R/T/Y/U/I` 슬롯 단축키와 UI 버튼이 한 번씩만 같은 항목을 실행하는지 확인한다.
- [ ] `Esc`가 건설·이동·공격이동·순찰·집결점 보류 상태를 취소하고, `1-0` 선택, `Ctrl+1-0` 저장, `Shift+1-0` 추가 선택이 명령 단축키와 충돌하지 않는지 확인한다.

### 확장 자원 거점 및 생산 중단

- [ ] 자원 노드 기준 상하좌우 3칸·대각선 2칸 보호 영역이 건설 프리뷰와 실제 건설 시도에 동일하게 적용되는지 Unity PlayMode에서 확인한다.
- [ ] AI가 보호 영역 안쪽 후보를 건너뛰고 반경을 넓혀 보호 영역 밖의 Main Base 후보를 순차적으로 선택하는지 Unity PlayMode에서 확인한다.
- [ ] 런타임 부트스트랩의 두 추가 자원 지대에서 Main Base 또는 Drop-off를 건설한 뒤, 일꾼이 기존 `ResourceDropOff.FindNearest` 정책대로 가장 가까운 반납 지점을 선택하는지 확인한다.
- [ ] AI가 추가 자원 지대 근처에 두 번째 Main Base를 건설하고, 새 본진 생산 큐와 반납 지점을 실제로 활용하는지 확인한다.
- [ ] 생산 건물 파괴 또는 비활성화 직후 활성/대기 생산 비용과 예약 보급이 한 번만 반환되고, 선택 HUD 및 생산 패널이 즉시 사라지는지 확인한다.

### Worker 자동 자원 분배

- [ ] 기본 비활성인 `WorkerAutoAssignmentManager`를 활성화했을 때 팀별 유휴 Worker만 살아있는 반납처가 있는 자원 노드에 균등 배정되는지 Unity PlayMode에서 확인한다.
- [ ] 자동 배정 중인 Worker에게 플레이어의 명시적 우클릭 이동·채집·건설 명령을 내리면 자동 관리자가 해당 명령을 덮어쓰지 않는지 확인한다.
- [ ] 자원 노드 고갈, 반납처 비활성화, Worker 유휴 전환 시 재평가가 일어나며 매 프레임 `FindObjects` 탐색을 수행하지 않는지 확인한다.
- [ ] Team1 HUD의 자동 Worker 분배 버튼이 기본 OFF로 표시되고, 버튼 클릭 시 ON/OFF 상태와 현재 배정 Worker 수가 즉시 갱신되는지 Unity PlayMode에서 확인한다.
- [ ] 자동 Worker 분배 HUD 버튼이 기존 이동·채집·건설 단축키와 충돌하지 않고, 직접 명령 우선 원칙을 유지하는지 확인한다.

### 자원 Tilemap 배치

- [x] `ResourceTile` 및 `ResourceTilemapNodeSynchronizer`를 추가했다. `Resource` 이름의 Tilemap에서 `ResourceTile` 셀만 읽어 런타임 `ResourceNode`로 생성·동기화하며, 별도 Resource 레이어는 `ProjectSTilemapWorld`의 이동·건설 판정 Tilemap 조회에 포함하지 않는다.
- [x] `Assets/01.Textures/Resources/ResourceNodes.png`를 추가하고 좌측 Minerals·우측 Gas를 Sprite Multiple로 분할하는 import 설정과 `Tools/Project S/Resources/Prepare Resource Tile Palette` 메뉴를 추가했다. 메뉴는 `Assets/Assets/Tilemaps/Resource Tiles`의 두 Resource Tile 에셋을 생성하거나 갱신한다.
- [x] `MapCreateSceneAutoBootstrap`이 Resource Tilemap에 자원 타일이 하나라도 있을 때 기존 Home/Expansion 자원 클러스터 생성을 건너뛰도록 연결했다.
- [x] fallback 자원 노드가 `Assets/Resources/ResourceNodes.png`의 Minerals/Gas Sprite를 런타임에 로드하고, Resource Tile 에셋도 같은 Sprite 참조를 사용하도록 보정했다.
- [ ] `Resource` Tilemap에 Minerals/Gas 타일을 각각 배치했을 때 셀별 자원 타입·초기량·채집량·시간·상호작용 범위가 `ResourceNode`로 전달되는지 Unity PlayMode에서 확인한다.
- [ ] Resource Tilemap이 있는 경우 fallback 자원 클러스터가 중복 생성되지 않고, 비어 있는 경우에만 기존 fallback이 유지되는지 Unity PlayMode에서 확인한다.
- [ ] `ResourceTilemap_SynchronizesResourceNodes_WithoutChangingNavigationOrBuildability` PlayMode 테스트를 Unity Test Runner에서 실행한다.
- [ ] `ResourceNodeSprites_AreAvailableToRuntimeFallback` PlayMode 테스트를 Unity Test Runner에서 실행한다.

### 최소 유닛 업그레이드

- [ ] 생산 건물을 선택해 Research 화면에서 Weapon Calibration과 Mobility Tuning을 각각 시작했을 때 비용 차감, 진행률, 완료 상태가 HUD에 정확히 표시되는지 확인한다.
- [ ] Team1의 Weapon Calibration(+3 ATK)과 Mobility Tuning(+15% Move)이 기존 유닛과 이후 생산된 Team1 유닛에 모두 적용되고 Team2에는 영향을 주지 않는지 확인한다.
- [ ] `TeamUpgradeResearch_AppliesCompletedUpgradesToOnlyItsTeam` PlayMode 테스트를 Unity Test Runner에서 실행한다.

### 전투 타깃 탐색

- [ ] 다수 유닛이 서로 다른 공간 버킷에 분산된 전투에서 감지거리 밖의 적이 후보 조회에 포함되지 않고, `UnitTargetQueryStatistics`의 방문 후보 수가 전체 적 수보다 충분히 작은지 확인한다.
- [ ] 공격 이동·순찰·대기 상태에서 최근 공격자, 전투 유닛, 일꾼, 방어 건물, 생산 건물, 본진 우선순위가 유지되는지 확인한다.
- [ ] 같은 우선순위의 적이 조금 더 가까워졌을 때 즉시 목표가 흔들리지 않고, 뚜렷하게 더 가까운 적 또는 더 높은 위협의 적이 나타날 때만 목표를 바꾸는지 확인한다.
- [ ] 이동 유닛과 건물의 공간 버킷 갱신 뒤 자동 공격, 광역 공격, 자동 포탑이 새 위치의 대상만 정상 탐색하는지 확인한다.
- [ ] `SpatialQuery_VisitsOnlyNearbyEnemyBuckets`, `SpatialQuery_RefreshesTargetAfterItChangesCells`, `AttackMove_KeepsCurrentTargetWhenSamePriorityCandidateIsOnlySlightlyCloser` PlayMode 테스트를 Unity Test Runner에서 실행한다.

### 유닛별 전술 행동

- [ ] Soldier가 표준 사거리 경계에서 안정적으로 정지하고, 새 직접 명령을 받으면 추격이나 공격을 즉시 중단하는지 확인한다.
- [ ] Ranger가 공격 사거리 45% 안에서 `RetreatingFromTarget`으로 전환되고 72% 거리에서 재교전하며, 후퇴 경로가 장애물과 아군 점유 셀을 통과하지 않는지 확인한다.
- [ ] Ranger의 `HoldPosition`과 일반 `Move`가 자동 후퇴보다 우선하고, `FocusAttack`은 지정 대상을 유지한 채 거리 조절만 수행하는지 확인한다.
- [ ] Tank 프리팹에 체력 260, 공격력 26, 사거리 5.5, 감지 7, 공격 속도 0.55, 이동 속도 2가 적용되는지 확인한다.
- [ ] Striker와 Swarm이 표준 유닛보다 깊게 접근하고 더 짧은 재경로 간격으로 목표를 추적하면서 기존 점유 분산을 유지하는지 확인한다.
- [ ] Spliter가 주 대상 포함, 반경 2, 최대 3대상 광역 공격 규칙을 유지하는지 확인한다.
- [ ] `UnitTacticalBehaviorPlayModeTests`와 기존 명령·광역 공격 PlayMode 테스트를 Unity Test Runner에서 함께 실행한다.

### 능동 스킬 최소 루프

- [ ] Striker 선택 HUD에 `Overdrive [F]` 버튼과 준비, 활성 지속시간, 쿨다운이 정상 표시되는지 확인한다.
- [ ] 이동, 공격 이동, 집중 공격, 위치 사수 중 Overdrive를 사용해도 최신 명령과 현재 목적지가 초기화되지 않는지 확인한다.
- [ ] Overdrive가 4초 동안 이동 속도를 50% 높이고 종료 뒤 건물 오라와 연구 보너스를 보존한 채 자기 수정자만 제거하는지 확인한다.
- [ ] 여러 Striker 선택 시 사용 가능한 유닛만 실행되고 쿨다운 중인 유닛 수와 실패 사유가 HUD에 표시되는지 확인한다.
- [ ] Soldier 등 스킬이 없는 유닛과 AI가 Overdrive를 자동으로 실행하지 않는지 확인한다.
- [ ] `UnitActiveSkillPlayModeTests`를 Unity Test Runner에서 실행한다.

### 전투 시각 피드백

- [ ] 유닛과 자동 포탑 공격 적중 시 황색 파동, 피격 시 적색 파동, 사망 시 큰 소멸 파동이 각각 한 번씩 표시되는지 확인한다.
- [ ] Striker의 Overdrive 성공 시 청록색 강화 파동이 표시되고, 쿨다운 실패 시에는 이펙트가 발생하지 않는지 확인한다.
- [ ] 다수 유닛 교전에서도 전투 피드백 오브젝트가 최대 풀 크기 48개를 넘지 않고 가장 오래된 효과를 재사용하는지 확인한다.
- [ ] 현재 시야 밖 적 유닛과 건물의 적중, 피격, 사망, 스킬 효과가 화면에 노출되지 않고 아군 효과는 계속 표시되는지 확인한다.
- [ ] 전투 피드백 추가 전후의 공격력, 상성 배율, 체력 차감, 공격 속도가 동일한지 확인한다.
- [ ] `CombatFeedbackPlayModeTests`와 기존 전투·안개·능동 스킬 PlayMode 테스트를 Unity Test Runner에서 함께 실행한다.

### 재경기 및 매치 종료

- [ ] 승패 확정 뒤 결과 오버레이에서 `Rematch`를 누르면 현재 씬이 다시 로드되고 자원, 유닛, 건물, 생산 큐, 매치 타이머가 새 매치 상태로 초기화되는지 확인한다.
- [ ] 빌드 시작 시 `MainMenu` 씬에서 전장 배경과 명령 버튼 이미지가 표시되고, `PLAY`를 연속 클릭해도 `MapCreate_Scene` 로드가 한 번만 시작되는지 확인한다.
- [ ] 승패 확정 뒤 `Main Menu`를 누르면 `MainMenu` 씬으로 복귀하고 새 매치를 시작할 수 있는지 확인한다.
- [ ] 승패 확정 뒤 `End Match`는 중복 실행되지 않으며, 실제 빌드에서는 애플리케이션을 종료하고 Unity Editor에서는 종료 요청만 안전하게 기록하는지 확인한다.
- [ ] `MatchOutcomePlayModeTests`와 `MainMenuFlowPlayModeTests`를 Unity Test Runner에서 실행한다.

### 안개 전쟁

- [ ] 아군 유닛과 건물이 현재 시야를 제공하고, 이동·파괴·비활성화 뒤 이전 시야가 탐색 완료 상태로 전환되는지 확인한다.
- [ ] `BlocksVision` Obstacle 뒤의 셀이 미탐색 또는 탐색 완료 상태로 유지되고, 장애물을 제거하면 시야가 즉시 확장되는지 확인한다.
- [ ] 메인 화면 오버레이와 미니맵 모두 현재 시야 밖 적 유닛·건물 마커를 숨기고, 탐색 완료 지형은 어둡게 남기는지 확인한다.
- [ ] 다수 유닛 이동 중 전맵 계산이 매 프레임 발생하지 않고, 제공자 셀·활성 상태·시야 반경·지형 리비전 변경 때만 `FogOfWarManager.RebuildCount`가 증가하는지 확인한다.
- [ ] `Visibility_ChangesOnlyWhenProviderChangesCells`, `BlocksVisionTile_HidesCellsBehindItUntilTerrainChanges`, `Minimap_HidesEnemyMarkersOutsideCurrentVision` PlayMode 테스트를 Unity Test Runner에서 실행한다.
- [ ] 시야에서 벗어난 적 유닛과 건물이 실제 위치 대신 마지막 관측 위치에 작고 반투명한 일반 마커로 표시되는지 확인한다.
- [ ] 적을 재발견하면 마지막 관측 마커가 실제 팀 마커로 교체되고, 적이 파괴되면 마지막 관측 마커도 제거되는지 확인한다.
- [ ] Worker 6, Soldier 7, Spliter 7, Ranger 9, Tank 8, Striker 6, Swarm 5.5의 기본 시야 반경이 프리팹과 실제 시야에 동일하게 적용되는지 확인한다.
- [ ] `Minimap_UsesLastObservedPositionAfterEnemyEntersFog` PlayMode 테스트를 Unity Test Runner에서 실행한다.

### 건설 메뉴 해제

- [ ] 작업 유닛으로 건물 배치를 성공한 직후 B 건물 목록과 배치 프리뷰가 닫히고, 즉시 다른 명령을 입력할 수 있는지 확인한다.
- [ ] B 건물 목록을 연 상태와 건물 배치 대기 상태에서 Esc를 누르면 목록과 배치 상태가 함께 닫히는지 확인한다.
- [ ] B 건물 목록 또는 건물 배치 대기 상태에서 다른 아군 유닛·건물을 선택하면 목록이 닫히는지 확인한다.

### 건물별 생산 역할

- [ ] MainBase가 Worker만, Production이 Soldier/Ranger/Tank/Striker/Swarm만, SpliterProduction이 Spliter만 표시하고 생산 가능한지 실제 Unity PlayMode에서 확인한다.
- [ ] 역할과 맞지 않는 생산 정의가 HUD 버튼과 단축키 슬롯에 나타나지 않고, 직접 큐 요청도 비용·보급 예약 없이 역할 실패 사유로 거부되는지 확인한다.
- [ ] `UnitProductionQueue_FiltersDefinitionsByConfiguredBuildingRole` PlayMode 테스트를 Unity Test Runner에서 실행한다.

### 기술 및 해금 시스템 기반

- [ ] 조건이 없는 생산 및 건설 항목이 기존과 동일하게 항상 사용 가능한지 Unity PlayMode에서 확인한다.
- [ ] 팀별 `TeamUnlockState`에 해금 ID를 부여했을 때 해당 조건을 가진 생산 버튼과 건설 버튼이 `LOCKED` 상태로 보이고, 클릭 및 `Q/E/R/T/Y/U/I` 단축키가 같은 실패 사유를 표시하는지 확인한다.
- [ ] 잠긴 생산 요청과 건설 배치 요청이 자원·보급을 소비하지 않으며, 해금 직후에는 즉시 다시 사용할 수 있는지 확인한다.
- [ ] `UnlockRequirement_UsesSameTeamStateForProductionAndConstruction` PlayMode 테스트를 Unity Test Runner에서 실행한다.
