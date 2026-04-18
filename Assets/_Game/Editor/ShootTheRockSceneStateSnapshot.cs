#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class ShootTheRockSceneStateSnapshot
{
    private const string SummaryPath = "Assets/_Game/UI/ShootTheRockSceneStateSnapshot.md";
    private const string FlatPath = "Assets/_Game/UI/ShootTheRockSceneStateSnapshot.flat.txt";
    private const string ChangePath = "Assets/_Game/UI/ShootTheRockSceneStateChanges.md";
    private const string ProjectSummaryPath = "Assets/_Game/UI/ShootTheRockProjectStateSummary.md";
    private const string RestoreSnapshotPath = "Assets/_Game/UI/ShootTheRockRestoreSnapshot.json";
    private const string RestoreSnapshotVersion = "2026-04-18-shoottherock-restore-state-v1";

    private const int MaxStringLength = 220;
    private const int MaxPropertiesPerComponent = 512;
    private const int MaxDiffEntriesPerSection = 200;

    private static readonly string[] IgnoredPropertyPaths =
    {
        "m_ObjectHideFlags",
        "m_CorrespondingSourceObject",
        "m_PrefabInstance",
        "m_PrefabAsset",
        "m_GameObject",
        "m_Script",
        "m_EditorClassIdentifier",
        "m_Children",
        "m_Father",
        "m_RootOrder"
    };

    [Serializable]
    private sealed class RestoreSnapshot
    {
        public string version;
        public string scenePath;
        public List<RestoreNode> nodes = new List<RestoreNode>();
    }

    [Serializable]
    private sealed class RestoreNode
    {
        public string path;
        public bool activeSelf;
        public int layer;
        public string tag;
        public int siblingIndex;
        public bool hasRectTransform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public List<RestoreComponentData> components = new List<RestoreComponentData>();
    }

    [Serializable]
    private sealed class RestoreComponentData
    {
        public string typeName;
        public int typeIndex;
        public string json;
    }

    private struct SnapshotRecord
    {
        public string Key;
        public string Value;
    }

    private sealed class SnapshotStats
    {
        public int RootCount;
        public int GameObjectCount;
        public int ComponentCount;
        public int PropertyCount;
    }

    public static void SaveCurrentSceneStateSnapshot()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("Shoot the ROCK scene state snapshot: no active loaded scene.");
            return;
        }

        string summaryFullPath = ToFullPath(SummaryPath);
        string flatFullPath = ToFullPath(FlatPath);
        string changeFullPath = ToFullPath(ChangePath);
        string projectSummaryFullPath = ToFullPath(ProjectSummaryPath);
        string restoreSnapshotFullPath = ToFullPath(RestoreSnapshotPath);
        Directory.CreateDirectory(Path.GetDirectoryName(summaryFullPath));

        Dictionary<string, string> previousRecords = LoadFlatSnapshot(flatFullPath);
        SnapshotStats stats = new SnapshotStats();
        List<string> trackedRoots = new List<string>();
        List<Transform> rootTransforms = GetTrackedRoots(trackedRoots, stats);
        List<SnapshotRecord> records = BuildFlatSnapshotRecords(rootTransforms, stats);
        records.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        Dictionary<string, string> currentRecords = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Count; i++)
            currentRecords[records[i].Key] = records[i].Value;

        RestoreSnapshot restoreSnapshot = BuildRestoreSnapshot(scene, rootTransforms);

        File.WriteAllText(flatFullPath, BuildFlatSnapshotText(scene, records), Encoding.UTF8);
        File.WriteAllText(summaryFullPath, BuildSummaryText(scene, trackedRoots, stats, currentRecords.Count), Encoding.UTF8);
        File.WriteAllText(changeFullPath, BuildChangeReportText(scene, previousRecords, currentRecords), Encoding.UTF8);
        File.WriteAllText(projectSummaryFullPath, BuildProjectSummaryText(scene, trackedRoots, stats), Encoding.UTF8);
        File.WriteAllText(restoreSnapshotFullPath, JsonUtility.ToJson(restoreSnapshot, true), Encoding.UTF8);
        AssetDatabase.Refresh();

        Debug.Log($"Shoot the ROCK scene state snapshot saved: {SummaryPath}");
    }

    public static bool RestoreSavedSceneState()
    {
        string fullPath = ToFullPath(RestoreSnapshotPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning("Shoot the ROCK restore: no saved restore snapshot found.");
            return false;
        }

        RestoreSnapshot snapshot = JsonUtility.FromJson<RestoreSnapshot>(File.ReadAllText(fullPath));
        if (snapshot == null || snapshot.nodes == null || snapshot.nodes.Count == 0)
        {
            Debug.LogWarning("Shoot the ROCK restore: restore snapshot is empty or invalid.");
            return false;
        }

        snapshot.nodes.Sort((a, b) =>
        {
            int depthCompare = GetPathDepth(a.path).CompareTo(GetPathDepth(b.path));
            return depthCompare != 0 ? depthCompare : string.CompareOrdinal(a.path, b.path);
        });

        Dictionary<string, RestoreNode> nodesByPath = new Dictionary<string, RestoreNode>(StringComparer.Ordinal);
        HashSet<string> expectedPaths = new HashSet<string>(StringComparer.Ordinal);
        List<string> rootPaths = new List<string>();

        for (int i = 0; i < snapshot.nodes.Count; i++)
        {
            RestoreNode node = snapshot.nodes[i];
            if (node == null || string.IsNullOrEmpty(node.path))
                continue;

            nodesByPath[node.path] = node;
            expectedPaths.Add(node.path);
            if (GetPathDepth(node.path) == 1)
                rootPaths.Add(node.path);
        }

        for (int i = 0; i < rootPaths.Count; i++)
        {
            Transform root = FindTransformByPath(rootPaths[i]);
            if (root != null)
                RemoveUnexpectedDescendants(root, expectedPaths);
        }

        for (int i = 0; i < snapshot.nodes.Count; i++)
            EnsureNodeExists(snapshot.nodes[i], nodesByPath);

        for (int i = 0; i < snapshot.nodes.Count; i++)
            EnsureNodeComponents(snapshot.nodes[i]);

        for (int i = 0; i < snapshot.nodes.Count; i++)
            ApplyNodeState(snapshot.nodes[i]);

        for (int i = 0; i < snapshot.nodes.Count; i++)
        {
            RestoreNode node = snapshot.nodes[i];
            Transform transform = FindTransformByPath(node.path);
            if (transform != null)
                transform.SetSiblingIndex(node.siblingIndex);
        }

        Debug.Log($"Shoot the ROCK saved state restored from: {RestoreSnapshotPath}");
        return true;
    }

    private static List<Transform> GetTrackedRoots(List<string> trackedRoots, SnapshotStats stats)
    {
        List<Transform> roots = new List<Transform>();
        HashSet<string> seenRootPaths = new HashSet<string>(StringComparer.Ordinal);
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return roots;

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject rootObject = rootObjects[i];
            if (rootObject == null)
                continue;

            AddTrackedRoot(rootObject.transform, roots, trackedRoots, stats, seenRootPaths);
        }

        return roots;
    }

    private static void AddTrackedRoot(Transform root, List<Transform> roots, List<string> trackedRoots, SnapshotStats stats, HashSet<string> seenRootPaths)
    {
        if (root == null)
            return;

        string rootPath = GetTransformPath(root);
        if (!seenRootPaths.Add(rootPath))
            return;

        roots.Add(root);
        trackedRoots.Add(rootPath);
        stats.RootCount++;
    }

    private static List<SnapshotRecord> BuildFlatSnapshotRecords(List<Transform> roots, SnapshotStats stats)
    {
        List<SnapshotRecord> records = new List<SnapshotRecord>(4096);
        for (int i = 0; i < roots.Count; i++)
            CaptureFlatSnapshotRecursive(roots[i], stats, records);
        return records;
    }

    private static RestoreSnapshot BuildRestoreSnapshot(Scene scene, List<Transform> roots)
    {
        RestoreSnapshot snapshot = new RestoreSnapshot
        {
            version = RestoreSnapshotVersion,
            scenePath = scene.path
        };

        for (int i = 0; i < roots.Count; i++)
            CaptureRestoreNodeRecursive(roots[i], snapshot.nodes);

        snapshot.nodes.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
        return snapshot;
    }

    private static void CaptureFlatSnapshotRecursive(Transform current, SnapshotStats stats, List<SnapshotRecord> records)
    {
        if (current == null)
            return;

        string objectPath = GetTransformPath(current);
        GameObject gameObject = current.gameObject;
        stats.GameObjectCount++;

        AddRecord(records, objectPath, "GameObject", "activeSelf", gameObject.activeSelf ? "true" : "false");
        AddRecord(records, objectPath, "GameObject", "activeInHierarchy", gameObject.activeInHierarchy ? "true" : "false");
        AddRecord(records, objectPath, "GameObject", "layer", gameObject.layer.ToString());
        AddRecord(records, objectPath, "GameObject", "tag", gameObject.tag);
        AddRecord(records, objectPath, "GameObject", "siblingIndex", current.GetSiblingIndex().ToString());

        Component[] components = current.GetComponents<Component>();
        Dictionary<string, int> typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            string typeName = component != null ? component.GetType().Name : "MissingComponent";

            int count = 0;
            typeCounts.TryGetValue(typeName, out count);
            typeCounts[typeName] = count + 1;
            string componentId = typeName + "#" + count;

            stats.ComponentCount++;
            AddRecord(records, objectPath, componentId, "__type", typeName);

            if (component == null)
            {
                AddRecord(records, objectPath, componentId, "__missing", "true");
                continue;
            }

            CaptureSerializedProperties(component, objectPath, componentId, stats, records);
        }

        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            CaptureFlatSnapshotRecursive(current.GetChild(childIndex), stats, records);
    }

    private static void CaptureRestoreNodeRecursive(Transform current, List<RestoreNode> nodes)
    {
        if (current == null)
            return;

        RestoreNode node = new RestoreNode
        {
            path = GetTransformPath(current),
            activeSelf = current.gameObject.activeSelf,
            layer = current.gameObject.layer,
            tag = current.gameObject.tag,
            siblingIndex = current.GetSiblingIndex(),
            localPosition = current.localPosition,
            localRotation = current.localRotation,
            localScale = current.localScale,
            hasRectTransform = current is RectTransform
        };

        if (current is RectTransform rect)
        {
            node.anchorMin = rect.anchorMin;
            node.anchorMax = rect.anchorMax;
            node.pivot = rect.pivot;
            node.anchoredPosition = rect.anchoredPosition;
            node.sizeDelta = rect.sizeDelta;
            node.offsetMin = rect.offsetMin;
            node.offsetMax = rect.offsetMax;
        }

        Component[] components = current.GetComponents<Component>();
        Dictionary<string, int> typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            Type componentType = component.GetType();
            if (typeof(Transform).IsAssignableFrom(componentType) || ShouldSkipComponentForRestore(componentType))
                continue;

            string typeName = componentType.AssemblyQualifiedName;
            int typeIndex = 0;
            typeCounts.TryGetValue(typeName, out typeIndex);
            typeCounts[typeName] = typeIndex + 1;

            node.components.Add(new RestoreComponentData
            {
                typeName = typeName,
                typeIndex = typeIndex,
                json = EditorJsonUtility.ToJson(component, true)
            });
        }

        nodes.Add(node);

        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            CaptureRestoreNodeRecursive(current.GetChild(childIndex), nodes);
    }

    private static void CaptureSerializedProperties(Component component, string objectPath, string componentId, SnapshotStats stats, List<SnapshotRecord> records)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        int propertyCount = 0;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (ShouldSkipProperty(iterator))
                continue;

            AddRecord(records, objectPath, componentId, iterator.propertyPath, FormatSerializedProperty(iterator));
            stats.PropertyCount++;
            propertyCount++;

            if (propertyCount >= MaxPropertiesPerComponent)
            {
                AddRecord(records, objectPath, componentId, "__truncated", $"true after {MaxPropertiesPerComponent} properties");
                break;
            }
        }
    }

    private static bool ShouldSkipProperty(SerializedProperty property)
    {
        if (property == null)
            return true;

        for (int i = 0; i < IgnoredPropertyPaths.Length; i++)
        {
            if (property.propertyPath.StartsWith(IgnoredPropertyPaths[i], StringComparison.Ordinal))
                return true;
        }

        if (property.propertyType == SerializedPropertyType.Generic && !property.isArray && property.hasVisibleChildren)
            return true;

        return false;
    }

    private static void EnsureNodeExists(RestoreNode node, Dictionary<string, RestoreNode> nodesByPath)
    {
        if (node == null || string.IsNullOrEmpty(node.path))
            return;

        if (FindTransformByPath(node.path) != null)
            return;

        string[] parts = node.path.Split('/');
        if (parts.Length == 0)
            return;

        string currentPath = string.Empty;
        Transform parent = null;
        for (int i = 0; i < parts.Length; i++)
        {
            currentPath = i == 0 ? parts[i] : currentPath + "/" + parts[i];
            Transform existing = parent == null ? FindRootTransform(parts[i]) : parent.Find(parts[i]);
            if (existing == null)
            {
                if (!nodesByPath.TryGetValue(currentPath, out RestoreNode createNode) || createNode == null)
                    createNode = new RestoreNode { path = currentPath, hasRectTransform = false };

                GameObject createdObject = createNode.hasRectTransform ? new GameObject(parts[i], typeof(RectTransform)) : new GameObject(parts[i]);
                Transform createdTransform = createdObject.transform;
                if (parent != null)
                    createdTransform.SetParent(parent, false);
                existing = createdTransform;
            }

            parent = existing;
        }
    }

    private static void EnsureNodeComponents(RestoreNode node)
    {
        Transform transform = FindTransformByPath(node.path);
        if (transform == null)
            return;

        GameObject gameObject = transform.gameObject;
        Dictionary<Type, int> expectedCounts = new Dictionary<Type, int>();
        for (int i = 0; i < node.components.Count; i++)
        {
            Type type = ResolveComponentType(node.components[i].typeName);
            if (type == null || typeof(Transform).IsAssignableFrom(type) || ShouldSkipComponentForRestore(type))
                continue;

            int requiredCount = node.components[i].typeIndex + 1;
            if (!expectedCounts.TryGetValue(type, out int currentRequired) || requiredCount > currentRequired)
                expectedCounts[type] = requiredCount;
        }

        foreach (KeyValuePair<Type, int> pair in expectedCounts)
        {
            while (gameObject.GetComponents(pair.Key).Length < pair.Value)
                gameObject.AddComponent(pair.Key);
        }

        Dictionary<Type, List<Component>> existingByType = new Dictionary<Type, List<Component>>();
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            Type type = component.GetType();
            if (typeof(Transform).IsAssignableFrom(type) || ShouldSkipComponentForRestore(type))
                continue;

            if (!existingByType.TryGetValue(type, out List<Component> list))
            {
                list = new List<Component>();
                existingByType[type] = list;
            }

            list.Add(component);
        }

        foreach (KeyValuePair<Type, List<Component>> pair in existingByType)
        {
            int expectedCount = expectedCounts.TryGetValue(pair.Key, out int count) ? count : 0;
            for (int i = pair.Value.Count - 1; i >= expectedCount; i--)
                UnityEngine.Object.DestroyImmediate(pair.Value[i]);
        }
    }

    private static void ApplyNodeState(RestoreNode node)
    {
        Transform transform = FindTransformByPath(node.path);
        if (transform == null)
            return;

        GameObject gameObject = transform.gameObject;
        gameObject.layer = node.layer;
        if (!string.IsNullOrEmpty(node.tag))
        {
            try
            {
                gameObject.tag = node.tag;
            }
            catch
            {
                Debug.LogWarning($"Shoot the ROCK restore: tag '{node.tag}' missing, skipped for {node.path}.");
            }
        }

        transform.localPosition = node.localPosition;
        transform.localRotation = node.localRotation;
        transform.localScale = node.localScale;

        if (node.hasRectTransform && transform is RectTransform rect)
        {
            rect.anchorMin = node.anchorMin;
            rect.anchorMax = node.anchorMax;
            rect.pivot = node.pivot;
            rect.anchoredPosition = node.anchoredPosition;
            rect.sizeDelta = node.sizeDelta;
            rect.offsetMin = node.offsetMin;
            rect.offsetMax = node.offsetMax;
        }

        for (int i = 0; i < node.components.Count; i++)
        {
            RestoreComponentData componentData = node.components[i];
            Type type = ResolveComponentType(componentData.typeName);
            if (type == null || typeof(Transform).IsAssignableFrom(type) || ShouldSkipComponentForRestore(type))
                continue;

            Component component = GetComponentByTypeIndex(gameObject, type, componentData.typeIndex);
            if (component == null)
                continue;

            try
            {
                EditorJsonUtility.FromJsonOverwrite(componentData.json, component);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Shoot the ROCK restore: failed to apply {type.Name} on {node.path}. {exception.Message}");
            }
        }

        gameObject.SetActive(node.activeSelf);
    }

    private static bool ShouldSkipComponentForRestore(Type type)
    {
        return false;
    }

    private static void RemoveUnexpectedDescendants(Transform root, HashSet<string> expectedPaths)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            RemoveUnexpectedDescendants(child, expectedPaths);
            if (!expectedPaths.Contains(GetTransformPath(child)))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Component GetComponentByTypeIndex(GameObject gameObject, Type type, int typeIndex)
    {
        Component[] components = gameObject.GetComponents(type);
        return typeIndex >= 0 && typeIndex < components.Length ? components[typeIndex] : null;
    }

    private static Type ResolveComponentType(string assemblyQualifiedName)
    {
        if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            return null;

        return Type.GetType(assemblyQualifiedName);
    }

    private static Transform FindRootTransform(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i].transform;
        }

        return null;
    }

    private static Transform FindTransformByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string[] parts = path.Split('/');
        if (parts.Length == 0)
            return null;

        Transform current = FindRootTransform(parts[0]);
        if (current == null)
            return null;

        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null)
                return null;
        }

        return current;
    }

    private static int GetPathDepth(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return 0;

        int depth = 1;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '/')
                depth++;
        }

        return depth;
    }

    private static void AddRecord(List<SnapshotRecord> records, string objectPath, string componentId, string propertyPath, string value)
    {
        records.Add(new SnapshotRecord
        {
            Key = objectPath + " | " + componentId + " | " + propertyPath,
            Value = value ?? string.Empty
        });
    }

    private static string BuildFlatSnapshotText(Scene scene, List<SnapshotRecord> records)
    {
        StringBuilder builder = new StringBuilder(records.Count * 96);
        builder.AppendLine("# Shoot the ROCK Scene State Flat Snapshot");
        builder.AppendLine("# Deterministic line-oriented snapshot for git diffs");
        builder.AppendLine("# Scene: " + scene.path);
        builder.AppendLine("# Saved: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine();

        for (int i = 0; i < records.Count; i++)
            builder.AppendLine(records[i].Key + "\t" + records[i].Value);

        return builder.ToString();
    }

    private static string BuildSummaryText(Scene scene, List<string> trackedRoots, SnapshotStats stats, int recordCount)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("# Shoot the ROCK Scene State Snapshot");
        builder.AppendLine();
        builder.AppendLine("- Saved local time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("- Scene: " + scene.path);
        builder.AppendLine("- Unity version: " + Application.unityVersion);
        builder.AppendLine("- Purpose: compact but inspectable current-state snapshot for agent follow-up work");
        builder.AppendLine("- Detailed canonical diff file: `" + FlatPath + "`");
        builder.AppendLine("- Human-readable change report: `" + ChangePath + "`");
        builder.AppendLine("- Project summary: `" + ProjectSummaryPath + "`");
        builder.AppendLine("- Restore snapshot used by Restore: `" + RestoreSnapshotPath + "`");
        builder.AppendLine();
        builder.AppendLine("## Snapshot coverage");
        builder.AppendLine("- Tracked roots: " + stats.RootCount);
        builder.AppendLine("- GameObjects captured: " + stats.GameObjectCount);
        builder.AppendLine("- Components captured: " + stats.ComponentCount);
        builder.AppendLine("- Serialized property lines captured: " + stats.PropertyCount);
        builder.AppendLine("- Total flat records: " + recordCount);
        builder.AppendLine();
        builder.AppendLine("## Tracked roots");
        for (int i = 0; i < trackedRoots.Count; i++)
            builder.AppendLine("- `" + trackedRoots[i] + "`");
        builder.AppendLine();
        builder.AppendLine("## What this is good for");
        builder.AppendLine("- seeing the current scene / inspector state without opening full scene YAML");
        builder.AppendLine("- letting git show exact property-level diffs between checkpoints");
        builder.AppendLine("- providing the saved Restore baseline used for rollback after bad changes");
        builder.AppendLine();
        builder.AppendLine("## Limits");
        builder.AppendLine("- this is a tracked-root snapshot, not the whole project serialized byte-for-byte");
        builder.AppendLine("- runtime-only non-serialized state does not persist here");
        builder.AppendLine("- some Unity internal noise properties are intentionally skipped for readability");
        return builder.ToString();
    }

    private static string BuildChangeReportText(Scene scene, Dictionary<string, string> previousRecords, Dictionary<string, string> currentRecords)
    {
        List<string> added = new List<string>();
        List<string> removed = new List<string>();
        List<string> changed = new List<string>();

        HashSet<string> allKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in previousRecords)
            allKeys.Add(pair.Key);
        foreach (KeyValuePair<string, string> pair in currentRecords)
            allKeys.Add(pair.Key);

        List<string> sortedKeys = new List<string>(allKeys);
        sortedKeys.Sort(StringComparer.Ordinal);

        for (int i = 0; i < sortedKeys.Count; i++)
        {
            string key = sortedKeys[i];
            bool hadPrevious = previousRecords.TryGetValue(key, out string previousValue);
            bool hasCurrent = currentRecords.TryGetValue(key, out string currentValue);

            if (!hadPrevious && hasCurrent)
            {
                added.Add("- `" + key + "` = `" + EscapeForMarkdown(currentValue) + "`");
                continue;
            }

            if (hadPrevious && !hasCurrent)
            {
                removed.Add("- `" + key + "` (was `" + EscapeForMarkdown(previousValue) + "`)");
                continue;
            }

            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                changed.Add("- `" + key + "`\n  - old: `" + EscapeForMarkdown(previousValue) + "`\n  - new: `" + EscapeForMarkdown(currentValue) + "`");
            }
        }

        StringBuilder builder = new StringBuilder(16384);
        builder.AppendLine("# Shoot the ROCK Scene State Changes");
        builder.AppendLine();
        builder.AppendLine("- Saved local time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("- Scene: " + scene.path);
        builder.AppendLine("- Compared against previous `" + FlatPath + "` snapshot");
        builder.AppendLine("- Restore baseline file: `" + RestoreSnapshotPath + "`");
        builder.AppendLine("- Added entries: " + added.Count);
        builder.AppendLine("- Removed entries: " + removed.Count);
        builder.AppendLine("- Changed entries: " + changed.Count);
        builder.AppendLine();

        AppendDiffSection(builder, "Changed", changed);
        AppendDiffSection(builder, "Added", added);
        AppendDiffSection(builder, "Removed", removed);
        return builder.ToString();
    }

    private static void AppendDiffSection(StringBuilder builder, string title, List<string> entries)
    {
        builder.AppendLine("## " + title);
        if (entries.Count == 0)
        {
            builder.AppendLine("- none");
            builder.AppendLine();
            return;
        }

        int limit = Mathf.Min(entries.Count, MaxDiffEntriesPerSection);
        for (int i = 0; i < limit; i++)
            builder.AppendLine(entries[i]);

        if (entries.Count > limit)
            builder.AppendLine("- ... truncated, additional entries: " + (entries.Count - limit));

        builder.AppendLine();
    }

    private static string BuildProjectSummaryText(Scene scene, List<string> trackedRoots, SnapshotStats stats)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("# Shoot the ROCK Project State Summary");
        builder.AppendLine();
        builder.AppendLine("- Saved local time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("- Unity version: " + Application.unityVersion);
        builder.AppendLine("- Active scene: " + scene.path);
        builder.AppendLine();
        builder.AppendLine("## Key generated state artifacts");
        builder.AppendLine("- `" + SummaryPath + "`");
        builder.AppendLine("- `" + FlatPath + "`");
        builder.AppendLine("- `" + ChangePath + "`");
        builder.AppendLine("- `" + RestoreSnapshotPath + "`");
        builder.AppendLine();
        builder.AppendLine("## Tracked roots in scene-state capture");
        for (int i = 0; i < trackedRoots.Count; i++)
            builder.AppendLine("- `" + trackedRoots[i] + "`");
        builder.AppendLine();
        builder.AppendLine("## Capture counts");
        builder.AppendLine("- GameObjects: " + stats.GameObjectCount);
        builder.AppendLine("- Components: " + stats.ComponentCount);
        builder.AppendLine("- Serialized properties: " + stats.PropertyCount);
        builder.AppendLine();
        builder.AppendLine("## Assets top-level folders");
        string assetsPath = Application.dataPath;
        string[] topDirs = Directory.GetDirectories(assetsPath);
        Array.Sort(topDirs, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < topDirs.Length; i++)
            builder.AppendLine("- `Assets/" + Path.GetFileName(topDirs[i]) + "`");
        builder.AppendLine();
        builder.AppendLine("## _Game folders");
        string gamePath = Path.Combine(assetsPath, "_Game");
        if (Directory.Exists(gamePath))
        {
            string[] gameDirs = Directory.GetDirectories(gamePath);
            Array.Sort(gameDirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < gameDirs.Length; i++)
                builder.AppendLine("- `Assets/_Game/" + Path.GetFileName(gameDirs[i]) + "`");
        }
        else
        {
            builder.AppendLine("- `Assets/_Game` missing");
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> LoadFlatSnapshot(string fullPath)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(fullPath))
            return result;

        string[] lines = File.ReadAllLines(fullPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            int separatorIndex = line.IndexOf('\t');
            if (separatorIndex < 0)
                continue;

            string key = line.Substring(0, separatorIndex);
            string value = separatorIndex + 1 < line.Length ? line.Substring(separatorIndex + 1) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private static string FormatSerializedProperty(SerializedProperty property)
    {
        if (property == null)
            return "<null>";

        if (property.isArray && property.propertyType != SerializedPropertyType.String && !property.propertyPath.EndsWith("Array.size", StringComparison.Ordinal))
            return "Array(size=" + property.arraySize + ")";

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.###");
            case SerializedPropertyType.String:
                return "\"" + EscapeForMarkdown(property.stringValue) + "\"";
            case SerializedPropertyType.Color:
                return FormatColor(property.colorValue);
            case SerializedPropertyType.ObjectReference:
                return FormatObjectReference(property.objectReferenceValue);
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString();
            case SerializedPropertyType.Enum:
                return property.enumDisplayNames != null && property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                    ? property.enumDisplayNames[property.enumValueIndex]
                    : property.enumValueIndex.ToString();
            case SerializedPropertyType.Vector2:
                return FormatVector2(property.vector2Value);
            case SerializedPropertyType.Vector3:
                return FormatVector3(property.vector3Value);
            case SerializedPropertyType.Vector4:
                return FormatVector4(property.vector4Value);
            case SerializedPropertyType.Rect:
                return FormatRect(property.rectValue);
            case SerializedPropertyType.ArraySize:
                return property.intValue.ToString();
            case SerializedPropertyType.Character:
                return property.intValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                return "AnimationCurve(keys=" + property.animationCurveValue.length + ")";
            case SerializedPropertyType.Bounds:
                return FormatBounds(property.boundsValue);
            case SerializedPropertyType.Quaternion:
                Quaternion q = property.quaternionValue;
                return "(" + q.x.ToString("0.###") + ", " + q.y.ToString("0.###") + ", " + q.z.ToString("0.###") + ", " + q.w.ToString("0.###") + ")";
            case SerializedPropertyType.ExposedReference:
                return FormatObjectReference(property.exposedReferenceValue);
            case SerializedPropertyType.FixedBufferSize:
                return property.fixedBufferSize.ToString();
            case SerializedPropertyType.Vector2Int:
                return "(" + property.vector2IntValue.x + ", " + property.vector2IntValue.y + ")";
            case SerializedPropertyType.Vector3Int:
                return "(" + property.vector3IntValue.x + ", " + property.vector3IntValue.y + ", " + property.vector3IntValue.z + ")";
            case SerializedPropertyType.RectInt:
                RectInt rectInt = property.rectIntValue;
                return "x=" + rectInt.x + ", y=" + rectInt.y + ", w=" + rectInt.width + ", h=" + rectInt.height;
            case SerializedPropertyType.BoundsInt:
                BoundsInt boundsInt = property.boundsIntValue;
                return "pos=(" + boundsInt.position.x + ", " + boundsInt.position.y + ", " + boundsInt.position.z + "), size=(" + boundsInt.size.x + ", " + boundsInt.size.y + ", " + boundsInt.size.z + ")";
            case SerializedPropertyType.ManagedReference:
                return property.managedReferenceFullTypename;
            case SerializedPropertyType.Hash128:
                return property.hash128Value.ToString();
            default:
                return "<" + property.propertyType + ">";
        }
    }

    private static string FormatObjectReference(UnityEngine.Object value)
    {
        if (value == null)
            return "null";

        if (value is Component component)
            return GetTransformPath(component.transform);

        if (value is GameObject gameObject)
            return GetTransformPath(gameObject.transform);

        string assetPath = AssetDatabase.GetAssetPath(value);
        if (!string.IsNullOrEmpty(assetPath))
            return assetPath;

        return value.name;
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return "null";

        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string EscapeForMarkdown(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string flattened = value.Replace("\r", " ").Replace("\n", " ").Trim();
        if (flattened.Length > MaxStringLength)
            flattened = flattened.Substring(0, MaxStringLength) + "...";

        return flattened.Replace("`", "'").Replace("\"", "'");
    }

    private static string ToFullPath(string assetRelativePath)
    {
        string assetsRelative = assetRelativePath.Replace("Assets/", string.Empty);
        return Path.Combine(Application.dataPath, assetsRelative);
    }

    private static string FormatVector2(Vector2 value)
    {
        return "(" + value.x.ToString("0.###") + ", " + value.y.ToString("0.###") + ")";
    }

    private static string FormatVector3(Vector3 value)
    {
        return "(" + value.x.ToString("0.###") + ", " + value.y.ToString("0.###") + ", " + value.z.ToString("0.###") + ")";
    }

    private static string FormatVector4(Vector4 value)
    {
        return "(" + value.x.ToString("0.###") + ", " + value.y.ToString("0.###") + ", " + value.z.ToString("0.###") + ", " + value.w.ToString("0.###") + ")";
    }

    private static string FormatColor(Color color)
    {
        return "RGBA(" + color.r.ToString("0.###") + ", " + color.g.ToString("0.###") + ", " + color.b.ToString("0.###") + ", " + color.a.ToString("0.###") + ")";
    }

    private static string FormatRect(Rect rect)
    {
        return "x=" + rect.x.ToString("0.###") + ", y=" + rect.y.ToString("0.###") + ", w=" + rect.width.ToString("0.###") + ", h=" + rect.height.ToString("0.###");
    }

    private static string FormatBounds(Bounds bounds)
    {
        return "center=" + FormatVector3(bounds.center) + ", size=" + FormatVector3(bounds.size);
    }
}
#endif
