# Project S Tilemap System

## 개요

Project S의 새 맵 시스템은 Unity 기본 `Grid`, `Tilemap`, `Tile Palette` 워크플로우를 기반으로 한다.

맵 제작자는 Unity Tile Palette로 타일맵을 직접 그리고, 게임 로직은 `ProjectSTile`에 저장된 판정 데이터를 읽는다.

## 핵심 구성

| 구성 요소 | 역할 |
|---|---|
| `ProjectSTile` | Unity `Tile`을 확장한 Project S용 타일 에셋 |
| `ProjectSTilemapWorld` | 씬의 Tilemap 레이어를 읽어 셀/월드 좌표와 타일 판정을 제공 |
| `ProjectSTilemapNavigator` | Tilemap 셀 기반 A* 경로 탐색 제공 |
| `ResourceTile` | 셀별 자원 종류와 채집 설정을 저장하는 자원 전용 Tile 에셋 |
| `ResourceTilemapNodeSynchronizer` | Resource Tilemap 셀을 런타임 `ResourceNode`로 동기화 |

## Terrain Type

`ProjectSTile`은 다음 Terrain Type을 가진다.

- `Highground`
- `Ground`
- `Underground`
- `Wall`
- `Water`
- `Prop`
- `Ramp`

스프라이트와 Terrain Type은 독립 설정이다. 같은 스프라이트를 여러 `ProjectSTile` 에셋에 넣고 서로 다른 이동/건설/지형 판정을 줄 수 있다.

## 타일 제작

Project 창에서 여러 Sprite 또는 Sprite가 들어 있는 Texture를 선택한 뒤 아래 메뉴를 실행한다.

```text
Assets/Create/Project S/Tilemaps/Tiles From Selected Sprites
```

생성된 타일은 기본적으로 아래 폴더에 저장된다.

```text
Assets/Assets/Tilemaps/Tiles
```

생성 후 Inspector에서 Terrain Type, Walkable, Buildable, Blocks Movement, Blocks Construction, Movement Cost 등을 수정한다.

## 씬 구성

1. 씬에 Unity `Grid`를 만든다.
2. 하위에 `Ground`, `Stair`, `Overlay`, `Obstacle` 용 `Tilemap`을 만든다.
3. 같은 GameObject 또는 부모 GameObject에 `ProjectSTilemapWorld`를 추가한다.
4. 필요한 Tilemap 참조를 할당한다.
5. 같은 GameObject에 `ProjectSTilemapNavigator`를 추가한다.
6. 유닛은 `UnitPathAgent`를 통해 Navigator가 있으면 Tilemap 경로를 사용한다.

Navigator가 없는 씬에서는 기존처럼 클릭 위치까지 직선 이동으로 동작한다.

## Obstacle Tilemap

이동 불가 지형은 `Obstacle` 이름의 전용 Tilemap에 배치한다. 기본 장애물 타일은 이동과 건설을 막고, 시야 차단 정보도 함께 가진다. `Obstacle` Unity Layer가 프로젝트에 등록돼 있으면 그 레이어를 사용하며, 누락된 경우에도 Tilemap 이름으로 동일한 이동·건설 차단 판정을 유지한다.

Unity Editor에서 아래 메뉴를 실행하면 임시 돌 장애물 스프라이트와 배치용 타일 에셋을 생성하거나 갱신한다.

```text
Tools/Project S/Tilemaps/Prepare Obstacle Tile Palette
```

활성 씬의 Grid 하위에 `Obstacle` Tilemap이 없다면 아래 메뉴로 생성한다. 생성된 레이어는 `FrontProps` 정렬 레이어를 사용한다.

```text
Tools/Project S/Tilemaps/Add Obstacle Tilemap To Active Scene
```

생성 경로는 다음과 같다.

```text
Assets/Assets/Tilemaps/Obstacle Tiles/Obstacle_Stone.png
Assets/Assets/Tilemaps/Obstacle Tiles/Obstacle_Stone.asset
```

Obstacle Tilemap의 타일을 추가하거나 제거하면 `ProjectSTilemapWorld`가 내비게이션 캐시를 갱신하고, 미니맵 지형 캐시도 다음 갱신 시점에 변경된 이동 가능 상태를 반영한다.

## 자원 Tilemap

자원은 `Resource` 이름의 별도 Tilemap에 배치한다. 이 레이어는 `ProjectSTilemapWorld`의 Ground/Stair/Overlay/Obstacle 조회 대상이 아니므로, Resource Tile 자체는 이동 가능 여부나 건설 가능 여부를 바꾸지 않는다. 셀마다 생성되는 `ResourceNode`는 기존 상호작용과 건설 충돌 정책을 그대로 사용한다.

`ResourceTilemapNodeSynchronizer`는 `Resource` Tilemap을 찾아 `ResourceTile`이 있는 셀마다 런타임 노드를 생성한다. Minerals/Gas 종류, 초기량, 1회 채집량, 채집 시간, 상호작용 범위는 타일 에셋 설정에서 읽는다. Tilemap이 비어 있지 않으면 `MapCreateSceneAutoBootstrap`은 기존 Home/Expansion 자원 클러스터를 생성하지 않는다.

`Assets/Resources/ResourceNodes.png`에는 좌측 Minerals와 우측 Gas가 Sprite Multiple로 분할되어 있다. 이 경로는 런타임 fallback 자원 노드와 Resource Tile 에셋이 공통으로 사용한다. Unity Editor에서 아래 메뉴를 실행하면 분할 설정을 다시 적용하고 Tile Palette에 바로 넣을 수 있는 두 Resource Tile 에셋을 생성하거나 갱신한다.

```text
Tools/Project S/Resources/Prepare Resource Tile Palette
```

활성 씬의 Grid 아래에 Resource 레이어가 없다면 아래 메뉴로 생성한다.

```text
Tools/Project S/Resources/Add Resource Tilemap To Active Scene
```

생성 경로는 다음과 같다.

```text
Assets/Assets/Tilemaps/Resource Tiles/ResourceNodes_Minerals.asset
Assets/Assets/Tilemaps/Resource Tiles/ResourceNodes_Gas.asset
```
