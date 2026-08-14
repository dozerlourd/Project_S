using System.IO;
using ProjectS.Maps;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Maps.Editor
{
    public static class ProjectSMapEditorAssetFactory
    {
        private const string RootFolder = "Assets/ProjectS/MapEditor/Generated";
        private const string PrefabFolder = RootFolder + "/Prefabs";

        [MenuItem("Tools/Project S/Create Default Map Tool Assets")]
        public static void CreateDefaultTileSet()
        {
            EnsureFolder("Assets/ProjectS", "MapEditor");
            EnsureFolder("Assets/ProjectS/MapEditor", "Generated");
            EnsureFolder(RootFolder, "Prefabs");

            var highGroundStraightEnd = CreateEdgePrefab("HighGround_Straight_End", 1f, true);
            var highGroundCornerEdge = CreateCornerPrefab("HighGround_Corner_Edge", 1f, true);
            var highGround = CreateGroundPrefab("HighGround", 1f);
            var baseGround = CreateGroundPrefab("BaseGround", 0f);
            var baseToHighRamp = CreateRampPrefab("Base_To_High_Ramp", 0f, 1f);
            var lowerBlockedGround = CreateGroundPrefab("LowerBlockedGround", -1f);
            var lowGroundStraightEnd = CreateEdgePrefab("LowGround_Straight_End", -1f, false);
            var lowGroundCornerEdge = CreateCornerPrefab("LowGround_Corner_Edge", -1f, false);
            var prop = CreatePrimitivePrefab("Rock_Prop", PrimitiveType.Sphere, new Vector3(1f, 0.8f, 1f), new Vector3(0f, 0.4f, 0f));
            var resource = CreatePrimitivePrefab("Resource_Node", PrimitiveType.Cylinder, new Vector3(1.2f, 0.5f, 1.2f), new Vector3(0f, 0.25f, 0f));
            var spawn = CreatePrimitivePrefab("Spawn_Marker", PrimitiveType.Cylinder, new Vector3(1.5f, 0.1f, 1.5f), new Vector3(0f, 0.05f, 0f));

            var assetPath = RootFolder + "/DefaultTileSet.asset";
            var tileSet = AssetDatabase.LoadAssetAtPath<TileSetDefinition>(assetPath);
            if (tileSet == null)
            {
                tileSet = ScriptableObject.CreateInstance<TileSetDefinition>();
                AssetDatabase.CreateAsset(tileSet, assetPath);
            }

            var serialized = new SerializedObject(tileSet);
            ClearEntryList(serialized, "terrainTiles");
            ClearEntryList(serialized, "rampTiles");
            ClearEntryList(serialized, "cliffTiles");
            ClearEntryList(serialized, "propPrefabs");
            ClearEntryList(serialized, "resourcePrefabs");
            ClearEntryList(serialized, "spawnPrefabs");

            AddEntry(serialized, "terrainTiles", "high_ground_straight_end", "High Ground - Straight End", highGroundStraightEnd, MapTerrainType.HighGroundStraightEnd, 1, true, false);
            AddEntry(serialized, "terrainTiles", "high_ground_corner_edge", "High Ground - Corner Edge", highGroundCornerEdge, MapTerrainType.HighGroundCornerEdge, 1, true, false);
            AddEntry(serialized, "terrainTiles", "high_ground", "High Ground", highGround, MapTerrainType.HighGround, 1, true, true);
            AddEntry(serialized, "terrainTiles", "base_ground", "Base Ground", baseGround, MapTerrainType.BaseGround, 0, true, true);
            AddEntry(serialized, "rampTiles", "base_to_high_ramp", "Base To High Ramp", baseToHighRamp, MapTerrainType.BaseToHighRamp, 0, true, false);
            AddEntry(serialized, "terrainTiles", "lower_blocked_ground", "Lower Blocked Ground", lowerBlockedGround, MapTerrainType.LowerBlockedGround, -1, false, false);
            AddEntry(serialized, "terrainTiles", "low_ground_straight_end", "Low Ground - Straight End", lowGroundStraightEnd, MapTerrainType.LowGroundStraightEnd, -1, false, false);
            AddEntry(serialized, "terrainTiles", "low_ground_corner_edge", "Low Ground - Corner Edge", lowGroundCornerEdge, MapTerrainType.LowGroundCornerEdge, -1, false, false);
            AddEntry(serialized, "propPrefabs", "rock", "Rock", prop, MapTerrainType.Empty, 0, false, false, PlacedMapObjectType.Prop, true, true);
            AddEntry(serialized, "resourcePrefabs", "resource", "Resource", resource, MapTerrainType.Empty, 0, false, false, PlacedMapObjectType.Resource, true, true);
            AddEntry(serialized, "spawnPrefabs", "spawn", "Spawn", spawn, MapTerrainType.Empty, 0, true, true, PlacedMapObjectType.Spawn, false, false);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = tileSet;
            EditorGUIUtility.PingObject(tileSet);
        }

        private static void ClearEntryList(SerializedObject serialized, string listName)
        {
            var list = serialized.FindProperty(listName);
            list.ClearArray();
        }

        private static void AddEntry(
            SerializedObject serialized,
            string listName,
            string id,
            string displayName,
            GameObject prefab,
            MapTerrainType terrainType,
            int heightLevel,
            bool walkable,
            bool buildable,
            PlacedMapObjectType objectType = PlacedMapObjectType.Terrain,
            bool blocksMovement = false,
            bool blocksConstruction = false)
        {
            var list = serialized.FindProperty(listName);
            list.arraySize++;
            var item = list.GetArrayElementAtIndex(list.arraySize - 1);
            item.FindPropertyRelative("id").stringValue = id;
            item.FindPropertyRelative("displayName").stringValue = displayName;
            item.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            item.FindPropertyRelative("objectType").enumValueIndex = (int)objectType;
            item.FindPropertyRelative("terrainType").enumValueIndex = (int)terrainType;
            item.FindPropertyRelative("size").vector2IntValue = Vector2Int.one;
            item.FindPropertyRelative("heightLevel").intValue = heightLevel;
            item.FindPropertyRelative("defaultWalkable").boolValue = walkable;
            item.FindPropertyRelative("defaultBuildable").boolValue = buildable;
            item.FindPropertyRelative("allowRotation").boolValue = true;
            item.FindPropertyRelative("blocksMovement").boolValue = blocksMovement;
            item.FindPropertyRelative("blocksConstruction").boolValue = blocksConstruction;
        }

        private static GameObject CreatePrimitivePrefab(string name, PrimitiveType primitiveType, Vector3 scale, Vector3 localPosition)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            DeleteExistingAsset(path);

            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.localScale = scale;
            instance.transform.localPosition = localPosition;

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateGroundPrefab(string name, float surfaceHeight)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            DeleteExistingAsset(path);

            var root = new GameObject(name);
            AddPrimitiveChild(root.transform, "Surface", PrimitiveType.Cube, new Vector3(2f, 0.18f, 2f), new Vector3(0f, surfaceHeight - 0.09f, 0f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateEdgePrefab(string name, float surfaceHeight, bool wallDropsDown)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            DeleteExistingAsset(path);

            var root = new GameObject(name);
            AddPrimitiveChild(root.transform, "Surface", PrimitiveType.Cube, new Vector3(2f, 0.18f, 2f), new Vector3(0f, surfaceHeight - 0.09f, 0f));
            AddEdgeWall(root.transform, "Edge Wall", surfaceHeight, wallDropsDown, false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCornerPrefab(string name, float surfaceHeight, bool wallDropsDown)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            DeleteExistingAsset(path);

            var root = new GameObject(name);
            AddPrimitiveChild(root.transform, "Surface", PrimitiveType.Cube, new Vector3(2f, 0.18f, 2f), new Vector3(0f, surfaceHeight - 0.09f, 0f));
            AddEdgeWall(root.transform, "South Edge Wall", surfaceHeight, wallDropsDown, false);
            AddEdgeWall(root.transform, "West Edge Wall", surfaceHeight, wallDropsDown, true);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateRampPrefab(string name, float lowerHeight, float upperHeight)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            DeleteExistingAsset(path);

            var root = new GameObject(name);
            var ramp = AddPrimitiveChild(root.transform, "Ramp Surface", PrimitiveType.Cube, new Vector3(2f, 0.18f, 2.25f), Vector3.zero);
            ramp.transform.localPosition = new Vector3(0f, (lowerHeight + upperHeight) * 0.5f - 0.09f, 0f);
            ramp.transform.localRotation = Quaternion.Euler(-26.565f, 0f, 0f);

            AddPrimitiveChild(root.transform, "Lower Lip", PrimitiveType.Cube, new Vector3(2f, 0.12f, 0.18f), new Vector3(0f, lowerHeight - 0.06f, -0.95f));
            AddPrimitiveChild(root.transform, "Upper Lip", PrimitiveType.Cube, new Vector3(2f, 0.12f, 0.18f), new Vector3(0f, upperHeight - 0.06f, 0.95f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void DeleteExistingAsset(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void AddEdgeWall(Transform parent, string name, float surfaceHeight, bool wallDropsDown, bool rotateToWest)
        {
            var wallHeight = 1f;
            var centerY = wallDropsDown ? surfaceHeight - wallHeight * 0.5f : surfaceHeight + wallHeight * 0.5f;
            var scale = rotateToWest ? new Vector3(0.18f, wallHeight, 2f) : new Vector3(2f, wallHeight, 0.18f);
            var position = rotateToWest ? new Vector3(-0.91f, centerY, 0f) : new Vector3(0f, centerY, -0.91f);
            AddPrimitiveChild(parent, name, PrimitiveType.Cube, scale, position);
        }

        private static GameObject AddPrimitiveChild(Transform parent, string name, PrimitiveType primitiveType, Vector3 scale, Vector3 localPosition)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localScale = scale;
            child.transform.localPosition = localPosition;
            return child;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = Path.Combine(parent, child).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
