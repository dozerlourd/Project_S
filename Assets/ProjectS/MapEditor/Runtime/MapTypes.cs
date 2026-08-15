using System;
using UnityEngine;

namespace ProjectS.Maps
{
    public enum MapTerrainType
    {
        Empty,
        Flat,
        HighGround,
        Cliff,
        Ramp,
        BaseGround,
        HighGroundStraightEnd,
        HighGroundCornerEdge,
        LowerBlockedGround,
        LowGroundStraightEnd,
        LowGroundCornerEdge,
        BaseToHighRamp,
        BaseToHighRampTwoCell,
        BaseToHighRampThreeCell
    }

    public static class MapTerrainRules
    {
        public static bool IsRamp(MapTerrainType terrainType)
        {
            return terrainType == MapTerrainType.Ramp
                || terrainType == MapTerrainType.BaseToHighRamp
                || terrainType == MapTerrainType.BaseToHighRampTwoCell
                || terrainType == MapTerrainType.BaseToHighRampThreeCell;
        }

        public static bool UsesAutoHeightWalls(MapTerrainType terrainType)
        {
            return terrainType == MapTerrainType.HighGround
                || terrainType == MapTerrainType.BaseGround
                || terrainType == MapTerrainType.LowerBlockedGround;
        }
    }

    public enum PlacedMapObjectType
    {
        Terrain,
        Prop,
        Resource,
        Spawn
    }

    public enum MapValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum MapRuntimeBuildMode
    {
        BuildFromDefinition,
        UseBakedPrefab,
        PreferBakedPrefab
    }

    [Serializable]
    public sealed class MapCellData
    {
        public int x;
        public int y;
        public int heightLevel;
        public MapTerrainType terrainType = MapTerrainType.Empty;
        public bool walkable;
        public bool buildable;
        public bool occupied;
        public string tileId;
        public float rotationY;

        public Vector2Int Position => new Vector2Int(x, y);

        public void Reset(int gridX, int gridY)
        {
            x = gridX;
            y = gridY;
            heightLevel = 0;
            terrainType = MapTerrainType.Empty;
            walkable = false;
            buildable = false;
            occupied = false;
            tileId = string.Empty;
            rotationY = 0f;
        }
    }

    [Serializable]
    public sealed class PlacedMapObject
    {
        public string id;
        public GameObject prefab;
        public PlacedMapObjectType objectType;
        public Vector2Int gridPosition;
        public Vector2Int size = Vector2Int.one;
        public int heightLevel;
        public float rotationY;
        public bool blocksMovement;
        public bool blocksConstruction = true;
    }

    [Serializable]
    public sealed class SpawnPointData
    {
        public string id = "Player";
        public Vector2Int gridPosition;
        public int playerIndex;
        public int heightLevel;
    }

    [Serializable]
    public sealed class ResourceNodeData
    {
        public string id = "Resource";
        public Vector2Int gridPosition;
        public Vector2Int size = Vector2Int.one;
        public int amount = 1500;
        public int heightLevel;
        public GameObject prefab;
    }

    [Serializable]
    public sealed class MapValidationIssue
    {
        public MapValidationSeverity severity;
        public string message;
        public Vector2Int gridPosition;

        public MapValidationIssue(MapValidationSeverity severity, string message, Vector2Int gridPosition)
        {
            this.severity = severity;
            this.message = message;
            this.gridPosition = gridPosition;
        }
    }
}
