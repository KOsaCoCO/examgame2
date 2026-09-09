
/****
NOTE NOTE NOTE
1. Add this component to a terrain.
2. Open Window > AI > Navigation > Areas and define the NavMesh Areas you need
   (eg "Walkable", "Water", "Not Walkable"). Give "Water" a distinct area so that
   only NavMeshAgents whose Area Mask includes "Water" will be able to path over it.
3. Click "Sync Area List With Terrain Layers" to size the Area List to match the
   number of texture layers painted on the terrain, then, for each entry (in the
   same order as the terrain's Paint Texture list), pick the NavMesh Area that
   texture should represent, eg the layer painted with a water/riverbed texture
   should be mapped to "Water".
4. Set "Defaultarea" to whichever area should be used for any texture layer you
   didn't explicitly map (usually your main walkable ground texture, eg "Walkable").
IF THIS IS TOO SLOW:
- Increase step to 2, 5, 10 or something higher.
*/

#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEngine.AI;

/** If this script seems to take forever, CHECK THE LAYERS YOUR NAV AGENT IS USING and filter out all the crap you don't need */
[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(NavMeshSurface))]
public class NavMeshGenA : MonoBehaviour
{
    Terrain terrain;
    TerrainData terrainData;
    Vector3 terrainPos;

    /** This NEEDS to be a layer used by your Navigation agent */
    public String NavAgentLayer = "Default";
    /** Area used for any texture layer that isn't explicitly mapped in areaID below */
    public String defaultarea = "Walkable";
    /** Include trees collision in navigation mesh */
    public bool includeTrees;
    public float timeLimitInSecs = 20;
    /** How detailed will the edges of the terrain texture navmesh be? Smaller values mean larger generation times */
    public int step = 1;
    /** How tall each generated area volume is, starting at the terrain surface and extending upward. Increase if slopes/bumps cause gaps in coverage */
    public float volumeHeight = 2f;

    /** One entry per terrain texture layer, in the same order as the terrain's Paint Texture list. Type the exact NavMesh Area name (Window > AI > Navigation > Areas) that texture should represent, eg "Water" for a water texture, so only water-capable NavMeshAgents can path there via their Area Mask. Use "Sync Area List With Terrain Layers" (right-click the component header) to size this list to match the terrain. */
    public List<string> areaID = new List<string>();

    [SerializeField] bool _destroyTempObjects;
    [SerializeField] bool _break;

    [ContextMenu("Sync Area List With Terrain Layers")]
    void SyncAreaListWithTerrainLayers()
    {
        TerrainLayer[] layers = GetComponent<Terrain>().terrainData.terrainLayers;
        List<string> updated = new List<string>();

        for (int i = 0; i < layers.Length; i++)
        {
            string existing = (i < areaID.Count && !string.IsNullOrEmpty(areaID[i])) ? areaID[i] : defaultarea;
            updated.Add(existing);
            Debug.Log($"Terrain texture layer {i}: '{layers[i].name}' -> area '{existing}'");
        }

        areaID = updated;
    }

    [ContextMenu("Generate NavAreas")]
    void Build()
    {
        EditorCoroutineUtility.StartCoroutine(GenMeshes(), this);
    }

    IEnumerator GenMeshes()
    {
        terrain = GetComponent<Terrain>();
        terrainData = terrain.terrainData;
        terrainPos = terrain.transform.position;

        int defaultArea = NavMesh.GetAreaFromName(defaultarea);
        if (defaultArea < 0)
        {
            Debug.LogError($"Default area '{defaultarea}' is not defined. Add it in Window > AI > Navigation > Areas first.");
            yield break;
        }

        int terrainLayer = LayerMask.NameToLayer(NavAgentLayer);
        if (includeTrees && terrainLayer < 0)
        {
            Debug.LogError($"Layer '{NavAgentLayer}' does not exist. Fix NavAgentLayer or disable includeTrees.");
            yield break;
        }

        Vector3 size = terrainData.size;
        Vector3 tpos = terrain.GetPosition();
        float minX = tpos.x;
        float maxX = minX + size.x;
        float minZ = tpos.z;
        float maxZ = minZ + size.z;

        GameObject attachParent;
        Transform childA = terrain.transform.Find("Delete me");

        if (childA != null)
        {
            attachParent = childA.gameObject;
        }
        else
        {
            attachParent = new GameObject();

            attachParent.name = "Delete me";
            attachParent.transform.SetParent(terrain.transform);
            attachParent.transform.localPosition = Vector3.zero;
        }

        yield return null;

        Debug.Log("terrain pos:" + tpos);
        Debug.Log("terrain size:" + size);
        Debug.Log("minX:" + minX + ", maxX:" + maxX + ", minZ:" + minZ + ", maxZ:" + maxZ);

        // get the splat data for this cell as a 1x1xN 3d array (where N = number of textures)
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        Debug.Log("alpha width:" + terrainData.alphamapWidth + ", height:" + terrainData.alphamapHeight + ", resolution:" + terrainData.alphamapResolution);

        float alphaWidth = terrainData.alphamapWidth;
        float alphaHeight = terrainData.alphamapHeight;
        float tWidth = terrainData.size.x;
        float tHeight = terrainData.size.z;
        float startTime = Time.realtimeSinceStartup;
        float xStepsize = tWidth * ((float)1 / (float)alphaWidth);
        float zStepsize = tHeight * ((float)1 / (float)alphaHeight);

        Debug.Log("xStepSize:" + xStepsize);
        Debug.Log("Total Iterations = " + ((alphaWidth / step) * (alphaHeight / step)) + ", step:" + step);

        for (int dx = 0; dx < alphaWidth; dx += step)
        {
            yield return null;

            float xOff = tWidth * ((float)dx / (float)alphaWidth);
            for (int dz = 0; dz < alphaHeight; dz += step)
            {
                float zOff = tHeight * ((float)dz / (float)alphaHeight);

                if (_break)
                {
                    Debug.Log("Breaking");
                    yield break;
                }

                if (Time.realtimeSinceStartup > startTime + timeLimitInSecs)
                {
                    Debug.Log("Time limit exceeded");
                    goto escape;
                }

                int surface = GetMainTextureA(dz, dx, ref splatmapData);
                int area = ResolveArea(surface, defaultArea);
                if (area < 0)
                    continue;

                Vector3 pos = new Vector3(minX + xOff, 0, minZ + zOff);
                // Create a volume "step size" wide/deep that starts at the terrain surface and extends upward

                GameObject obj = new GameObject();
                Transform objT = obj.transform;
                objT.SetParent(attachParent.transform);
                objT.localScale = Vector3.one;
                float height = terrain.SampleHeight(pos);
                objT.position = new Vector3(pos.x, height, pos.z);

                NavMeshModifierVolume nmmv = obj.AddComponent<NavMeshModifierVolume>();
                nmmv.size = new Vector3(xStepsize * step, volumeHeight, zStepsize * step);
                nmmv.center = new Vector3(0, volumeHeight * 0.5f, 0);
                nmmv.area = area;
            }
        }
escape:

        if (includeTrees)
        {
            Debug.Log("Now doing trees");
            TreeInstance[] instances = terrainData.treeInstances;
            TreePrototype[] prototypes = terrainData.treePrototypes;
            Vector3 tsize = terrainData.size;

            foreach (TreeInstance inst in instances)
            {
                TreePrototype prototype = prototypes[inst.prototypeIndex];

                Vector3 pos = terrainPos + Vector3.Scale(inst.position, tsize);
                float rotY = inst.rotation;
                float hscale = inst.heightScale;
                float wscale = inst.widthScale;

                GameObject tree = GameObject.Instantiate(prototype.prefab);
                Transform objT = tree.transform;
                objT.SetParent(attachParent.transform);
                objT.position = pos;
                objT.localRotation = Quaternion.Euler(0, rotY * Mathf.Rad2Deg, 0);
                objT.localScale = new Vector3(wscale, hscale, wscale);
                objT.gameObject.layer = terrainLayer;
                tree.isStatic = true;
            }
        }
        Debug.Log("Done prep, build nav mesh");
        foreach (NavMeshSurface nsurface in GetComponents<NavMeshSurface>())
        {
            nsurface.BuildNavMesh();
            yield return null;
        }

        if (_destroyTempObjects == false)
            yield break;
        Debug.Log($"Finished, destroy our {attachParent.transform.childCount} temp objects");
        GameObject.DestroyImmediate(attachParent.gameObject);
    }

    void destroyChildren(Transform attachParent)
    {
        // Delete children from nav areas
        while (attachParent.childCount > 0)
            GameObject.DestroyImmediate(attachParent.GetChild(0).gameObject);
    }

    /** Resolves which NavMesh area index a given terrain texture layer should use, falling back to defaultArea when the layer isn't explicitly mapped in areaID. Returns -1 if the resolved area name isn't defined. */
    private int ResolveArea(int textureIndex, int defaultArea)
    {
        string areaName = (textureIndex >= 0 && textureIndex < areaID.Count) ? areaID[textureIndex] : null;

        if (string.IsNullOrEmpty(areaName))
            return defaultArea;

        int area = NavMesh.GetAreaFromName(areaName);
        if (area < 0)
        {
            Debug.LogWarning($"Area '{areaName}' (mapped from texture layer {textureIndex}) is not defined in Navigation > Areas. Skipping this cell.");
            return -1;
        }

        return area;
    }

    /** https://answers.unity.com/questions/456973/getting-the-texture-of-a-certain-point-on-terrain.html */
    private float[] GetTextureMixA(int alphaZ, int alphaX, ref float[,,] splatmapData)
    {
        // extract the 3D array data to a 1D array:
        float[] cellMix = new float[splatmapData.GetUpperBound(2) + 1];

        for (int n = 0; n < cellMix.Length; n++)
        {
            cellMix[n] = splatmapData[alphaZ, alphaX, n];
        }

        return cellMix;
    }

    private int GetMainTextureA(int alphaZ, int alphaX, ref float[,,] splatmapData)
    {
        // returns the zero-based index of the most dominant texture
        // on the main terrain at this world position.
        float[] mix = GetTextureMixA(alphaZ, alphaX, ref splatmapData);

        float maxMix = 0;
        int maxIndex = 0;

        // loop through each mix value and find the maximum
        for (int n = 0; n < mix.Length; n++)
        {
            if (mix[n] > maxMix)
            {
                maxIndex = n;
                maxMix = mix[n];
            }
        }
        return maxIndex;
    }
}

#endif
