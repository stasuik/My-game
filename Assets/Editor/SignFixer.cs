using UnityEngine;
using UnityEditor;
using System.IO;

public class SignFixer
{
    const float SignWidth = 0.6f;

    [MenuItem("Construction/Fix Sign Prefabs")]
    static void FixAllSigns()
    {
        var mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        var signs = new[]
        {
            new SignData
            {
                texPath = "Assets/roadmodels/znak1/глав дорога.png",
                assetPath = "Assets/roadmodels/znak1/znak1.asset",
                prefabPath = "Assets/roadmodels/znak1/znak_glavnaya_doroga.prefab",
                itemName = "Знак «Главная дорога»",
            },
            new SignData
            {
                texPath = "Assets/roadmodels/znak1/пешеход.png",
                assetPath = "Assets/roadmodels/znak1/znak_peshehod.asset",
                prefabPath = "Assets/roadmodels/znak1/znak_peshehod.prefab",
                itemName = "Знак «Пешеход»",
            },
            new SignData
            {
                texPath = "Assets/roadmodels/znak2/остановка.png",
                assetPath = "Assets/roadmodels/znak2/znak2.asset",
                prefabPath = "Assets/roadmodels/znak2/znak_stop.prefab",
                itemName = "Знак «Стоп»",
            },
            new SignData
            {
                texPath = "Assets/roadmodels/znak3/images.png",
                assetPath = "Assets/roadmodels/znak3/znak3.asset",
                prefabPath = "Assets/roadmodels/znak3/znak3.prefab",
                itemName = "Знак 3",
            },
        };

        foreach (var sign in signs)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sign.texPath);
            if (tex == null)
            {
                Debug.LogWarning($"Texture not found: {sign.texPath}");
                continue;
            }

            float aspect = (float)tex.width / tex.height;
            float signHeight = SignWidth / aspect;

            // Material
            string matPath = sign.prefabPath.Replace(".prefab", ".mat");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Surface", 0); // Opaque
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetTextureScale("_MainTex", Vector2.one);
            mat.SetTextureOffset("_MainTex", Vector2.zero);
            AssetDatabase.CreateAsset(mat, matPath);

            // GameObject with Unity Quad
            var go = new GameObject(sign.itemName, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            go.transform.localScale = new Vector3(SignWidth, signHeight, 0.05f);

            PrefabUtility.SaveAsPrefabAsset(go, sign.prefabPath);
            Object.DestroyImmediate(go);

            // BuildableItem asset
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sign.prefabPath);
            var existing = AssetDatabase.LoadAssetAtPath<BuildableItem>(sign.assetPath);
            if (existing != null)
            {
                existing.finalPrefab = prefab;
                existing.ghostPrefab = prefab;
                EditorUtility.SetDirty(existing);
                Debug.Log($"Updated existing asset: {sign.assetPath}");
            }
            else
            {
                var asset = ScriptableObject.CreateInstance<BuildableItem>();
                asset.itemName = sign.itemName;
                asset.finalPrefab = prefab;
                asset.ghostPrefab = prefab;
                asset.isSplineType = false;
                asset.snapToTerrain = true;
                asset.stepDistance = 2.8f;
                asset.heightOffset = 0.04f;
                asset.rotationOffset = Vector3.zero;
                asset.requireRoadProximity = true;
                asset.maxRoadDistance = 5f;
                AssetDatabase.CreateAsset(asset, sign.assetPath);
                Debug.Log($"Created new asset: {sign.assetPath}");
            }

            Debug.Log($"{sign.itemName}: quad {SignWidth} x {signHeight:F3} (aspect {aspect:F3})");
        }

        // Fix old material Y-offset
        var oldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/roadmodels/znak1/Materials/пешеход.mat");
        if (oldMat != null)
        {
            oldMat.SetTextureOffset("_BaseMap", Vector2.zero);
            oldMat.SetTextureOffset("_MainTex", Vector2.zero);
            EditorUtility.SetDirty(oldMat);
            Debug.Log("Removed Y-offset 0.21 from пешеход.mat");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("All signs fixed!");
    }

    struct SignData
    {
        public string texPath;
        public string assetPath;
        public string prefabPath;
        public string itemName;
    }
}
