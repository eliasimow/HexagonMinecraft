using System;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using System.Data;
using Unity.Collections;

[RequireComponent(typeof(MeshRenderer))]

/*
Data class containing all relevant information for block placement in a single chunk.
*/
public class ChunkData : MonoBehaviour
{
    public int worldX;
    public int worldZ;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;
    public bool isGenerated = false;
    public NativeHashMap<float3,int> blockType;

    //Given vertices, uvs, triangles, and block data, recreate mesh and recalculate corresponding lighting/normals
    public void GenerateMesh(float3[] verticesTemp, float2[] uvTemp, int[] triangles, NativeHashMap<float3,int> blockType){
        meshRenderer = transform.GetComponent<MeshRenderer>();
        meshFilter = transform.GetComponent<MeshFilter>();
        meshCollider = transform.GetComponent<MeshCollider>();

        this.blockType = blockType;
        Vector3[] vertices = verticesTemp.Select(vertex => new Vector3(vertex.x,vertex.y, vertex.z)).ToArray();
        Vector2[] uvs = uvTemp.Select(uv => new Vector2(uv.x,uv.y)).ToArray();

        Mesh mesh = new()
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            vertices = vertices,
            uv = uvs,
            triangles = triangles
        };

        mesh.Optimize();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        isGenerated = true;
    }

    public String getIndexKey(){
        return worldX + "," + worldZ;
    }

    public void Dispose(){
        blockType.Dispose();
    }

}
