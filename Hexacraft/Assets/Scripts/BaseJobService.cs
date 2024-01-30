using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


public static class BaseJobService
{


    //Context struct containing all relevant information for a chunk job.
    //Used so that the methods in this service don't have 15 parameters.
    //Sheesh!
    public struct ChunkSettings{
        public readonly int width;
        public readonly int height;
        public readonly int worldX;
        public readonly int worldZ;
        public readonly float scale;
        public readonly int zScale;
        public readonly int seed;

        //We need to know the adjacent four chunks' block data to determine whether the sides of this chunk are visible.
        //If no side chunk exists we'll just calculate the perlin noise for an additional row.
        //By doing so, we can determine what the block data of the chunk will be when it's generated, 
        //and thus make an accurate estimate of this chunk's sides' visibility.
        public readonly NativeHashMap<float3, int> forwardBlockTypeMap;
        public readonly NativeHashMap<float3, int> backBlockTypeMap;
        public readonly NativeHashMap<float3, int> rightBlockTypeMap;
        public readonly NativeHashMap<float3, int> leftBlockTypeMap;
        public readonly NativeHashMap<float3, int> blockTypeMap;
        public ChunkSettings(
            int width,
            int height,
            int worldX,
            int worldZ,
            float scale,
            int zScale,
            int seed,
            NativeHashMap<float3, int> forwardBlockTypeMap, 
            NativeHashMap<float3, int> backBlockTypeMap, 
            NativeHashMap<float3, int> rightBlockTypeMap, 
            NativeHashMap<float3, int> leftBlockTypeMap,
            NativeHashMap<float3, int> blockTypeMap
        ){
            this.width = width;
            this.height = height;
            this.worldX = worldX;
            this.worldZ = worldZ;
            this.scale = scale;
            this.zScale = zScale;
            this.seed = seed;
            this.forwardBlockTypeMap = forwardBlockTypeMap;
            this.backBlockTypeMap = backBlockTypeMap;
            this.rightBlockTypeMap = rightBlockTypeMap;
            this.leftBlockTypeMap = leftBlockTypeMap;
            this.blockTypeMap = blockTypeMap;
        }
    }

    //Get perlin value (0 - 1) * zScale + 1 at point x,y,z in chunk
    public static float GetPerlinValue(int x,int y, int z, ChunkSettings chunkSettings){
        float3 center = CalculateLocalHexCenter(x,y,z);
        float2 offset = new float2(chunkSettings.width * chunkSettings.worldX * (.3f/.4f),chunkSettings.width * chunkSettings.worldZ * Mathf.Sqrt(3)/2)*chunkSettings.scale + new float2(chunkSettings.seed,chunkSettings.seed);
        float2 worldVector = chunkSettings.scale * new float2(center.x,center.z) + offset;
        return Mathf.PerlinNoise(worldVector.x, worldVector.y)* chunkSettings.zScale + 1;
    }

    //convert from hex index to local mesh position
    static float3 CalculateLocalHexCenter(int x, int y, int z){
        float xMid = x * (.3f/.4f);
        float yMid = y * .5f;
        float zMid = z * Mathf.Sqrt(3)/2 + Mathf.Abs(x%2) * Mathf.Sqrt(3)/4;
        
        return new float3(xMid, yMid, zMid);
    }

    //check for block existence at index x,y,z.
    //Returns true if blockType > 0.
    public static bool BlockPresent(
            int x, 
            int y, 
            int z,
            ChunkSettings chunkSettings
    ){
        int blockType = 0;
        if(x < 0){
            if(chunkSettings.leftBlockTypeMap.Count > 0){
                chunkSettings.leftBlockTypeMap.TryGetValue(new float3(chunkSettings.width+x,y,z), out blockType);
            }else{
                return y < BaseJobService.GetPerlinValue(x,y,z, chunkSettings);
            }
        }else if(x >= chunkSettings.width){
            if(chunkSettings.rightBlockTypeMap.Count > 0){
                chunkSettings.rightBlockTypeMap.TryGetValue(new float3(x-chunkSettings.width,y,z), out blockType);
            }else{
                return y < BaseJobService.GetPerlinValue(x,y,z, chunkSettings);
            }
        }else if(z < 0){
            if(chunkSettings.backBlockTypeMap.Count > 0){
                chunkSettings.backBlockTypeMap.TryGetValue(new float3(x,y,chunkSettings.width+z), out blockType);
            }else{
                return y < BaseJobService.GetPerlinValue(x,y,z, chunkSettings);
            }
        }else if(z >= chunkSettings.width){
            if(chunkSettings.forwardBlockTypeMap.Count > 0){
                chunkSettings.forwardBlockTypeMap.TryGetValue(new float3(x,y,z-chunkSettings.width), out blockType);
            }else{
                return y < BaseJobService.GetPerlinValue(x,y,z, chunkSettings);
            }
        }else if(y < 0){
            return true;
        }else if(y >= chunkSettings.height){
            return false;
        }else{
            chunkSettings.blockTypeMap.TryGetValue(new float3(x,y,z), out blockType);
        }
        return blockType != 0;
    }

    //Main calculation method responsible for determining which sides of a mesh to calculate.
    //Returns HexMeshData, a struct containing the vertices, uvs, and triangles of the calculated relevant sides of the hex.

    public static HexMeshData GetVisibleDataForHex(int x, int y, int z, int vertexOffset, ChunkSettings chunkSettings, BaseJobService.HexMeshData emptyHex){
        int blockValue = 0;
        if(chunkSettings.blockTypeMap.TryGetValue(new float3(x,y,z), out blockValue)){
            if(blockValue == 0){
                return emptyHex;
            }
        }

        NativeArray<bool> adjacentBlocks = new NativeArray<bool>(8, Allocator.Temp);

        adjacentBlocks[2] = BlockPresent(x,y,z-1,chunkSettings);
        adjacentBlocks[5] = BlockPresent(x,y,z+1,chunkSettings);
        adjacentBlocks[6] = BlockPresent(x,y+1,z,chunkSettings);
        adjacentBlocks[7] = BlockPresent(x,y-1,z,chunkSettings);

        if(x%2 == 0){
            adjacentBlocks[0] = BlockPresent(x+1,y,z,chunkSettings);
            adjacentBlocks[1] = BlockPresent(x+1,y,z-1,chunkSettings);

            adjacentBlocks[3] = BlockPresent(x-1,y,z-1,chunkSettings);
            adjacentBlocks[4] = BlockPresent(x-1,y,z,chunkSettings);
        }else{
            adjacentBlocks[0] = BlockPresent(x+1,y,z+1,chunkSettings);
            adjacentBlocks[1] = BlockPresent(x+1,y,z,chunkSettings);

            adjacentBlocks[3] = BlockPresent(x-1,y,z,chunkSettings);
            adjacentBlocks[4] = BlockPresent(x-1,y,z+1,chunkSettings);
        }
        
        BaseJobService.HexMeshData blockData = new(x,y,z,blockValue,vertexOffset,chunkSettings.worldX,chunkSettings.worldZ,adjacentBlocks);
        adjacentBlocks.Dispose();
        return blockData;
    }

    //Struct for a single hex. Calculates in its constructor the values for its vertex positions, uvs, and triangle indexes.
    public struct HexMeshData {
        NativeList<float3> vertices;
        NativeList<float2> uvs;
        NativeList<int> triangles;

        public HexMeshData(int empty){
            vertices = new NativeList<float3>(0, Allocator.Persistent);
            uvs = new NativeList<float2>(0, Allocator.Persistent);
            triangles = new NativeList<int>(0, Allocator.Persistent);
        }

        public HexMeshData(int x, int y, int z, int blockType, int vertexOffset, int worldX, int worldZ, NativeArray<bool> neighborsPresent){
            vertices = new NativeList<float3>(12, Allocator.Persistent);
            uvs = new NativeList<float2>(12, Allocator.Persistent);
            triangles = new NativeList<int>(60, Allocator.Persistent);


            float3 middlePoint = CalculateLocalHexCenter(x,y,z);
            float xMid = middlePoint.x;
            float yMid = middlePoint.y;
            float zMid = middlePoint.z;

            vertices.Add(new float3(xMid + 0.5f,yMid, zMid));
            vertices.Add(new float3(xMid + 0.25f,yMid, zMid - Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid - 0.25f,yMid, zMid - Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid - 0.5f,yMid, zMid));
            vertices.Add(new float3(xMid - 0.25f,yMid, zMid + Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid + 0.25f,yMid, zMid + Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid + 0.5f,yMid+0.5f, zMid));
            vertices.Add(new float3(xMid + 0.25f,yMid+0.5f, zMid - Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid - 0.25f,yMid+0.5f, zMid - Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid - 0.5f,yMid+0.5f, zMid));
            vertices.Add(new float3(xMid - 0.25f,yMid+0.5f, zMid + Mathf.Sqrt(3)/4));
            vertices.Add(new float3(xMid + 0.25f,yMid+0.5f, zMid + Mathf.Sqrt(3)/4));

            float2 start = new float2(0.05f + (blockType-1)*0.1f,0.91f);
            float2 end = new float2(0.05f + (blockType-1)*0.1f,0.81f);            
            for(int i = 0; i < 6; i ++){
                uvs.Add(end);
            }
            for(int i = 0; i < 6; i ++){
                uvs.Add(start);
            }

            //topR:
            if(!neighborsPresent[0]){
                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset+11);
                triangles.Add(vertexOffset + 5);

                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 6);
                triangles.Add(vertexOffset + 11);                    
            }

            //botR:
            if(!neighborsPresent[1]){

                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset+1);
                triangles.Add(vertexOffset + 7);

                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 7);
                triangles.Add(vertexOffset + 6);
            }

            //bot:
            if(!neighborsPresent[2]){
                triangles.Add(vertexOffset+7);
                triangles.Add(vertexOffset+1);
                triangles.Add(vertexOffset + 2);

                triangles.Add(vertexOffset + 8);
                triangles.Add(vertexOffset + 7);
                triangles.Add(vertexOffset + 2);   
            }

            //botL:
            if(!neighborsPresent[3]){
                triangles.Add(vertexOffset + 9);
                triangles.Add(vertexOffset + 8);
                triangles.Add(vertexOffset + 2);

                triangles.Add(vertexOffset + 9);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 3);
            }

            //topL:
            if(!neighborsPresent[4]){
                triangles.Add(vertexOffset + 10);
                triangles.Add(vertexOffset + 9);
                triangles.Add(vertexOffset + 3);

                triangles.Add(vertexOffset + 10);
                triangles.Add(vertexOffset + 3);
                triangles.Add(vertexOffset + 4);
            }

            //top:
            if(!neighborsPresent[5]){
                triangles.Add(vertexOffset + 11);
                triangles.Add(vertexOffset + 10);
                triangles.Add(vertexOffset + 4);

                triangles.Add(vertexOffset + 11);
                triangles.Add(vertexOffset + 4);
                triangles.Add(vertexOffset + 5);
            }

            //topY
            if(!neighborsPresent[6]){
                triangles.Add(vertexOffset + 8);
                triangles.Add(vertexOffset + 6);
                triangles.Add(vertexOffset + 7);

                triangles.Add(vertexOffset + 9);
                triangles.Add(vertexOffset + 6);
                triangles.Add(vertexOffset + 8);

                triangles.Add(vertexOffset + 11);
                triangles.Add(vertexOffset + 6);
                triangles.Add(vertexOffset + 9);

                triangles.Add(vertexOffset + 11);
                triangles.Add(vertexOffset + 9);
                triangles.Add(vertexOffset + 10);
            }
            //botY
            if(!neighborsPresent[7]){

                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 1);

                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 3);
                triangles.Add(vertexOffset + 2);

                triangles.Add(vertexOffset);
                triangles.Add(vertexOffset + 5);
                triangles.Add(vertexOffset + 3);

                triangles.Add(vertexOffset + 3);
                triangles.Add(vertexOffset + 5);
                triangles.Add(vertexOffset + 4);
            }
        }
        public NativeList<float3> GetVertices(){
            return vertices;
        }
        public NativeList<float2> GetUvs(){
            return uvs;
        }

        public NativeList<int> GetTriangles(){
            return triangles;
        }

        public void Dispose(){
            vertices.Dispose();
            uvs.Dispose();
            triangles.Dispose();
        }
    }
}
