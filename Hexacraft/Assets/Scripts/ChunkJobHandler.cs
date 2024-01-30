using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/*
Wrapper class containing relevant data for one RebuildChunk / Chunk Job instance.
Includes a disposal method for post job completion, as the memory has to be cleared manually for native structures.
*/
public class ChunkJobHandler
{
    public int x;
    public int z;

    //these point to the same structures used in the job, and should only be accessed after job completion.
    public NativeList<float3> vertexNativeList;
    public NativeList<float2> uvNativeList;
    public NativeList<int> triangleNativeList;
    public NativeHashMap<float3, int> blockTypeMap;
    
    IJob chunkJob;

    JobHandle handler;

    bool isRebuild = false;

    public ChunkJobHandler(IJob chunkJob, bool isRebuild, JobHandle handler, NativeList<float3> vertices, NativeList<float2> uvs, NativeList<int> triangles, NativeHashMap<float3, int> blockTypeMap, int x, int z){
        this.chunkJob = chunkJob;
        this.handler = handler;

        this.vertexNativeList = vertices;
        this.uvNativeList = uvs;
        this.triangleNativeList = triangles;
        this.blockTypeMap = blockTypeMap;

        this.x = x;
        this.z = z;
        this.isRebuild = isRebuild;
    }

    public bool IsCompleted(){
        return handler.IsCompleted;
    }

    public void Complete(){
        handler.Complete();
    }

    public void ClearMemory(){
        vertexNativeList.Dispose();
        uvNativeList.Dispose();
        triangleNativeList.Dispose();
    }
}
