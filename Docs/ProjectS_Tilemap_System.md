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
2. 하위에 `Ground`, `Overlay`, `Obstacle` 용 `Tilemap`을 만든다.
3. 같은 GameObject 또는 부모 GameObject에 `ProjectSTilemapWorld`를 추가한다.
4. 필요한 Tilemap 참조를 할당한다.
5. 같은 GameObject에 `ProjectSTilemapNavigator`를 추가한다.
6. 유닛은 `UnitPathAgent`를 통해 Navigator가 있으면 Tilemap 경로를 사용한다.

Navigator가 없는 씬에서는 기존처럼 클릭 위치까지 직선 이동으로 동작한다.
