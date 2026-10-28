using UnityEngine;

[ExecuteAlways]
public class EnvironmentDecorator : MonoBehaviour
{
    public Terrain terrain;
    public Material waterMaterial;
    public Material treeTrunkMaterial;
    public Material treeCrownMaterial;
    public Material rockMaterial;
    public int treeCount = 30;
    public int rockCount = 40;
    public float waterY = -1f;
    public float waterSize = 200f;
    public float treeMinHeight = 1.5f;
    public float treeMaxHeight = 4f;
    public float rockMinScale = 0.3f;
    public float rockMaxScale = 1.2f;

    private bool spawned;
    private GameObject decorationRoot;

    void OnEnable()
    {
        if (!spawned)
        {
            SpawnDecorations();
            spawned = true;
        }
    }

    void SpawnDecorations()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();

        if (terrain == null)
        {
            Debug.LogWarning("EnvironmentDecorator: no Terrain found");
            return;
        }

        decorationRoot = new GameObject("~Decorations");
        decorationRoot.transform.SetParent(transform, false);

        SpawnWater();
        SpawnTrees();
        SpawnRocks();
    }

    void SpawnWater()
    {
        Material mat = waterMaterial;
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = new Color(0.2f, 0.4f, 0.7f, 0.7f);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_DstBlend", 10);
            mat.SetFloat("_SrcBlend", 5);
            mat.SetFloat("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.name = "Water";
        water.transform.SetParent(decorationRoot.transform, false);
        water.transform.localPosition = new Vector3(0, waterY, 0);
        water.transform.localScale = new Vector3(waterSize, waterSize, 1);
        water.transform.localRotation = Quaternion.Euler(90, 0, 0);

        MeshRenderer mr = water.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void SpawnTrees()
    {
        TerrainData td = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        float w = td.size.x;
        float d = td.size.z;

        Material crownMat = treeCrownMaterial;
        if (crownMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            crownMat = new Material(shader);
            crownMat.color = new Color(0.2f, 0.6f, 0.15f);
        }

        Material trunkMat = treeTrunkMaterial;
        if (trunkMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            trunkMat = new Material(shader);
            trunkMat.color = new Color(0.4f, 0.25f, 0.1f);
        }

        for (int i = 0; i < treeCount; i++)
        {
            Vector2 r = Random.insideUnitCircle;
            float x = Mathf.Lerp(5, w - 5, (r.x + 1f) / 2f);
            float z = Mathf.Lerp(5, d - 5, (r.y + 1f) / 2f);
            float y = td.GetHeight(Mathf.RoundToInt(x / td.size.x * td.heightmapResolution),
                                   Mathf.RoundToInt(z / td.size.z * td.heightmapResolution));
            Vector3 pos = new Vector3(tPos.x + x, tPos.y + y, tPos.z + z);

            float height = Random.Range(treeMinHeight, treeMaxHeight);
            float trunkRadius = height * 0.06f;
            float crownRadius = height * 0.35f;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "TreeTrunk";
            trunk.transform.SetParent(decorationRoot.transform, false);
            trunk.transform.localPosition = pos + new Vector3(0, height * 0.35f, 0);
            trunk.transform.localScale = new Vector3(trunkRadius * 2, height * 0.35f, trunkRadius * 2);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "TreeCrown";
            crown.transform.SetParent(decorationRoot.transform, false);
            crown.transform.localPosition = pos + new Vector3(0, height * 0.7f + crownRadius * 0.5f, 0);
            crown.transform.localScale = new Vector3(crownRadius * 2, crownRadius * 2, crownRadius * 2);
            crown.GetComponent<MeshRenderer>().sharedMaterial = crownMat;
        }
    }

    void SpawnRocks()
    {
        TerrainData td = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        float w = td.size.x;
        float d = td.size.z;

        Material rMat = rockMaterial;
        if (rMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            rMat = new Material(shader);
            rMat.color = new Color(0.45f, 0.45f, 0.45f);
        }

        for (int i = 0; i < rockCount; i++)
        {
            Vector2 r = Random.insideUnitCircle;
            float x = Mathf.Lerp(5, w - 5, (r.x + 1f) / 2f);
            float z = Mathf.Lerp(5, d - 5, (r.y + 1f) / 2f);
            float y = td.GetHeight(Mathf.RoundToInt(x / td.size.x * td.heightmapResolution),
                                   Mathf.RoundToInt(z / td.size.z * td.heightmapResolution));
            Vector3 pos = new Vector3(tPos.x + x, tPos.y + y, tPos.z + z);

            float scale = Random.Range(rockMinScale, rockMaxScale);
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(decorationRoot.transform, false);
            rock.transform.localPosition = pos + new Vector3(0, scale * 0.3f, 0);
            float sx = scale * Random.Range(0.7f, 1.3f);
            float sy = scale * Random.Range(0.4f, 0.8f);
            float sz = scale * Random.Range(0.7f, 1.3f);
            rock.transform.localScale = new Vector3(sx, sy, sz);
            rock.transform.localRotation = Random.rotation;
            rock.GetComponent<MeshRenderer>().sharedMaterial = rMat;
        }
    }

    void OnDestroy()
    {
        if (decorationRoot != null)
        {
            DestroyImmediate(decorationRoot);
        }
    }
}
