using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/*
C# Ijob responsible for Calculating the block data for a chunk, given the pending chunk's parameters.
hexagon faces that are determined to be hidden from view are not included in the final result, which improves performance substantially 

By job completion, the following data structures should be populated and ready for use in the chunk's mesh.

vertexNativeList: Vertcies for visible hexagons. Has an expected length of up to 16 (per hexagon) * width * width * height.
uvNativeList: UVs for the vertices. Same size as vertextNativeList, with each float2 corresponding to its index pair in the vertext list.
triangleNativeList: Indices for triangles connecting the vertices. expected length of up to 60 (maximum per hexagon) * width * width * height. 
blockTypeMap: a hashmap of hexagon local coordinate to block value. 0 is absent, 1,2,3 are various different colors for different parts of the terrain.
*/
[BurstCompile(CompileSynchronously = true)]
public struct ChunkJob : IJob
{
    [ReadOnly] public int width;
    [ReadOnly] public int height;
    [ReadOnly] public float scale;
    [ReadOnly] public int seed;
    [ReadOnly] public int worldX;
    [ReadOnly] public int worldZ;
    [ReadOnly] public int zScale;
    public NativeList<float3> vertexNativeList;
    public NativeList<float2> uvNativeList;
    public NativeList<int> triangleNativeList;
    public NativeHashMap<float3, int> blockTypeMap;

    //We need to know the adjacent four chunks' block data to determine whether the sides of this chunk are visible.
    //If no side chunk exists we'll just calculate the perlin noise for an additional row.
    //By doing so, we can determine what the block data of the chunk will be when it's generated, 
    //and thus make an accurate estimate of this chunk's sides' visibility.
    [ReadOnly] public NativeHashMap<float3, int> forwardBlockTypeMap;
    [ReadOnly] public NativeHashMap<float3, int> backBlockTypeMap;
    [ReadOnly] public NativeHashMap<float3, int> rightBlockTypeMap;
    [ReadOnly] public NativeHashMap<float3, int> leftBlockTypeMap;

    public void Execute()
    {
        BaseJobService.ChunkSettings chunkSettings = new(width,height,worldX,worldZ,scale,zScale,seed,forwardBlockTypeMap,backBlockTypeMap, rightBlockTypeMap, leftBlockTypeMap,blockTypeMap);
        //calculate boolean map
        GenerateTerrainData(chunkSettings);
        GenerateMeshData(chunkSettings);
    }

    void GenerateTerrainData(BaseJobService.ChunkSettings chunkSettings){
        for(int x = 0; x < width; x++){
            for(int y = 0; y < height; y++){
                for(int z = 0; z < width; z++){
                    if(y < BaseJobService.GetPerlinValue(x,y,z, chunkSettings)){
                        if(y > zScale * 2f/4){
                            //standard dirt
                            blockTypeMap.TryAdd(new float3(x,y,z), 1);
                        }else if(y < 2){
                            blockTypeMap.TryAdd(new float3(x,y,z), 10);
                        }else{
                            //stone below
                            blockTypeMap.TryAdd(new float3(x,y,z), 2);
                        }
                    }else {
                        blockTypeMap.TryAdd(new float3(x,y,z), 0);
                    }
                }
            }
        }
    }

    public void GenerateMeshData(BaseJobService.ChunkSettings chunkSettings){
        int vertexOffset = 0;
        BaseJobService.HexMeshData emptyHex = new(1);
        for(int x = 0; x < width; x++){
            for(int y = 0; y < height; y++){
                for(int z = 0; z < width; z++){
                    BaseJobService.HexMeshData hexMeshData = BaseJobService.GetVisibleDataForHex(x,y,z,vertexOffset, chunkSettings, emptyHex);
                    vertexNativeList.AddRange(hexMeshData.GetVertices().AsArray());
                    uvNativeList.AddRange(hexMeshData.GetUvs().AsArray());
                    triangleNativeList.AddRange(hexMeshData.GetTriangles().AsArray());
                    vertexOffset+=hexMeshData.GetVertices().Length;
                }
            }
        }
        emptyHex.Dispose();
    }
}
