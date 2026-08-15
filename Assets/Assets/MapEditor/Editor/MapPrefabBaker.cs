using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectS.Maps;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Maps.Editor
{
    public sealed class MapPrefabBakeOptions
    {
        public bool validateBeforeSave = true;
        public bool stopOnValidationErrors = true;
        public bool markStatic = true;
        public bool removeEmptyGroups = true;
        public bool removeGeneratedColliders;
        public bool assignToMapDefinition = true;
        public bool useBakedPrefabAtRuntime = true;
    }

    public static class MapPrefabBaker
    {
        private const string DefaultPrefabFolder = "Assets/Assets/MapEditor/BakedMaps";

        public static GameObject SaveAsPrefab(MapDefinition map, MapPrefabBakeOptions options)
        {
            if (map == null)
            {
                EditorUtility.DisplayDialog("Save Map Prefab", "MapDefinition is missing.", "OK");
                return null;
            }

            options ??= new MapPrefabBakeOptions();
            if (!CanSave(map, options))
            {
                return null;
            }

            EnsureDefaultFolder();
            var defaultName = MakeAssetName(map.MapName);
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Map Prefab",
                defaultName,
                "prefab",
                "Choose where to save the baked map prefab.",
                DefaultPrefabFolder);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return SaveAsPrefab(map, path, options);
        }

        public static GameObject SaveAsPrefab(MapDefinition map, string path, MapPrefabBakeOptions options)
        {
            if (map == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            options ??= new MapPrefabBakeOptions();
            var root = MapEditorPreviewBuilder.BuildMapRoot(map, $"{MakeAssetName(map.MapName)}_Baked");
            if (root == null)
            {
                return null;
            }

            try
            {
                Optimize(root, options);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab != null && options.assignToMapDefinition)
                {
                    map.BakedMapPrefab = prefab;
                    if (options.useBakedPrefabAtRuntime)
                    {
                        map.RuntimeBuildMode = MapRuntimeBuildMode.PreferBakedPrefab;
                    }

                    EditorUtility.SetDirty(map);
                    AssetDatabase.SaveAssets();
                }

                Selection.activeObject = prefab;
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void OptimizeScenePreview(GameObject root, MapPrefabBakeOptions options)
        {
            if (root == null)
            {
                EditorUtility.DisplayDialog("Optimize Map Preview", "No preview root is selected or available.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Optimize Map Preview");
            Optimize(root, options ?? new MapPrefabBakeOptions());
            EditorUtility.SetDirty(root);
        }

        private static bool CanSave(MapDefinition map, MapPrefabBakeOptions options)
        {
            if (!options.validateBeforeSave)
            {
                return true;
            }

            var issues = MapValidator.Validate(map);
            var errors = issues.Where(issue => issue.severity == MapValidationSeverity.Error).ToList();
            if (errors.Count == 0)
            {
                return true;
            }

            var message = $"Map has {errors.Count} validation error(s).";
            if (options.stopOnValidationErrors)
            {
                EditorUtility.DisplayDialog("Save Map Prefab", $"{message}\nFix errors before saving.", "OK");
                return false;
            }

            return EditorUtility.DisplayDialog("Save Map Prefab", $"{message}\nSave anyway?", "Save", "Cancel");
        }

        private static void Optimize(GameObject root, MapPrefabBakeOptions options)
        {
            if (options.markStatic)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (ShouldMarkStatic(transform))
                    {
                        transform.gameObject.isStatic = true;
                    }
                }
            }

            if (options.removeGeneratedColliders)
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (IsGeneratedGeometry(collider.transform))
                    {
                        Object.DestroyImmediate(collider);
                    }
                }
            }

            if (options.removeEmptyGroups)
            {
                RemoveEmptyGroups(root.transform);
            }
        }

        private static void RemoveEmptyGroups(Transform root)
        {
            var emptyGroups = new List<GameObject>();
            foreach (Transform child in root)
            {
                RemoveEmptyGroups(child);
                if (child.childCount == 0
                    && child.GetComponents<Component>().Length == 1)
                {
                    emptyGroups.Add(child.gameObject);
                }
            }

            foreach (var emptyGroup in emptyGroups)
            {
                Object.DestroyImmediate(emptyGroup);
            }
        }

        private static bool ShouldMarkStatic(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                if (current.name == "Resources" || current.name == "Spawns")
                {
                    return false;
                }

                current = current.parent;
            }

            return true;
        }

        private static bool IsGeneratedGeometry(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                if (current.name == "Terrain" || current.name == "Auto Height Walls")
                {
                    return true;
                }

                if (current.name == "Objects" || current.name == "Resources" || current.name == "Spawns")
                {
                    return false;
                }

                current = current.parent;
            }

            return false;
        }

        private static void EnsureDefaultFolder()
        {
            if (AssetDatabase.IsValidFolder(DefaultPrefabFolder))
            {
                return;
            }

            const string root = "Assets/Assets/MapEditor";
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets/Assets", "MapEditor");
            }

            AssetDatabase.CreateFolder(root, "BakedMaps");
        }

        private static string MakeAssetName(string mapName)
        {
            var fallback = string.IsNullOrWhiteSpace(mapName) ? "BakedMap" : mapName.Trim();
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fallback = fallback.Replace(invalidChar, '_');
            }

            return fallback.Replace(' ', '_');
        }
    }
}
