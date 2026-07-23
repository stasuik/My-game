using UnityEngine;
using UnityEditor;
using System.IO;

public static class BridgeSetupEditor
{
    private const string BridgeFbxPath = "Assets/most.fbx";
    private const string BridgePrefabPath = "Assets/Bridge_Segment.prefab";
    private const string BridgePillarPath = "Assets/Bridge_Pillar.prefab";
    private const string ItemBridgePath = "Assets/Item_Bridge.asset";

    [MenuItem("Tools/Setup Bridge Prefab from FBX")]
    public static void SetupBridgePrefab()
    {
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BridgeFbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError("Bridge FBX not found at: " + BridgeFbxPath);
            return;
        }

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePrefabPath);
        if (existingPrefab != null)
        {
            bool overwrite = EditorUtility.DisplayDialog("Overwrite?", "Bridge_Segment.prefab already exists. Overwrite?", "Yes", "No");
            if (!overwrite) return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        if (instance == null)
        {
            Debug.LogError("Failed to instantiate bridge FBX");
            return;
        }

        Bounds bounds = CalculateBounds(instance);
        float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float scale = 10f / maxExtent;
        instance.transform.localScale = Vector3.one * scale;

        if (instance.GetComponent<Collider>() == null)
        {
            instance.AddComponent<BoxCollider>();
        }
        else
        {
            var existingColliders = instance.GetComponents<Collider>();
            foreach (var c in existingColliders) Object.DestroyImmediate(c);
            instance.AddComponent<BoxCollider>();
        }

#if UNITY_2018_3_OR_NEWER
        PrefabUtility.SaveAsPrefabAsset(instance, BridgePrefabPath);
#else
        PrefabUtility.CreatePrefab(BridgePrefabPath, instance);
#endif
        Object.DestroyImmediate(instance);

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Bridge_Pillar";
        pillar.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
        var pillarCol = pillar.GetComponent<CapsuleCollider>();
        if (pillarCol != null) Object.DestroyImmediate(pillarCol);
        pillar.AddComponent<MeshCollider>().convex = true;

#if UNITY_2018_3_OR_NEWER
        PrefabUtility.SaveAsPrefabAsset(pillar, BridgePillarPath);
#else
        PrefabUtility.CreatePrefab(BridgePillarPath, pillar);
#endif
        Object.DestroyImmediate(pillar);

        AssetDatabase.Refresh();

        AssignToBridgeItem();
    }

    [MenuItem("Tools/Assign Bridge Prefab to Item_Bridge")]
    public static void AssignToBridgeItem()
    {
        BuildableItem bridgeItem = AssetDatabase.LoadAssetAtPath<BuildableItem>(ItemBridgePath);
        if (bridgeItem == null)
        {
            Debug.LogError("Item_Bridge.asset not found at: " + ItemBridgePath);
            return;
        }

        GameObject bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePrefabPath);
        GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePillarPath);

        SerializedObject so = new SerializedObject(bridgeItem);
        so.FindProperty("finalPrefab").objectReferenceValue = bridgePrefab;
        so.FindProperty("ghostPrefab").objectReferenceValue = bridgePrefab;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(bridgeItem);

        Debug.Log("Item_Bridge.asset updated with bridge prefab!");
    }

    [MenuItem("Tools/Setup Construction Scene Bridge Pillar")]
    public static void SetupScenePillarPrefab()
    {
        GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePillarPath);
        if (pillarPrefab == null)
        {
            Debug.LogError("Bridge_Pillar.prefab not found. Run Setup Bridge Prefab first.");
            return;
        }

        GameObject constructionManager = GameObject.Find("ConstructionManager");
        if (constructionManager == null)
        {
            Debug.LogError("ConstructionManager not found in scene");
            return;
        }

        VRRoadBuilder roadBuilder = constructionManager.GetComponent<VRRoadBuilder>();
        if (roadBuilder != null)
        {
            SerializedObject so = new SerializedObject(roadBuilder);
            so.FindProperty("bridgePillarPrefab").objectReferenceValue = pillarPrefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(roadBuilder);
            Debug.Log("bridgePillarPrefab assigned in scene!");
        }
    }

    private static Bounds CalculateBounds(GameObject obj)
    {
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
}
