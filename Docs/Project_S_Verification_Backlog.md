# Project S Verification Backlog

## 문서 목적

이 문서는 구현은 완료했지만 나중에 한꺼번에 검증할 항목을 누적해서 관리하기 위한 체크리스트다.

앞으로 기능 구현 중 즉시 검증하지 못했거나, 실제 Unity Editor/PlayMode/Headless 환경에서 별도 확인이 필요한 항목은 이 문서에 추가한다.

## 검증 대기 항목

### Unity 테스트 실행 환경

- [ ] Unity Headless PlayMode 테스트가 테스트 러너에 진입하지 못하고 즉시 종료되는 원인을 확인한다.
- [ ] 기존 Unity 프로세스, 프로젝트 잠금, 사용자 캐시 DB, 라이선스 연결 상태가 테스트 실행을 막는지 확인한다.
- [ ] `Temp/playmode-results.xml` 결과 파일이 정상 생성되는지 확인한다.

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
