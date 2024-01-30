using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System;
using Unity.VisualScripting;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;

public class WorldGenerator : MonoBehaviour
{
    //Terrain Settings:
    //Chunks inside this range are generated, outside are disabled.
    public int drawDistance; 
    //For x and z coordinates of chunk, set to 8
    public int chunkWidth;
    //For y coordinate of chunk, set to 30.
    public int chunkHeight; 
    //Determines how quickly the terrain changes.
    public float mapZoom; //
    //Determines the verticality of the map's hills.
    public int mapZScale; 
    //Determines the initial placement of 0,0 in perlin noise. Generated to a random number on startup.
    private int seed;


    //Chunk fields.
    public GameObject ChunkPrefab;
    Dictionary<string, ChunkData> chunkDictionary;
    List<ChunkJobHandler> chunkJobs;

    //How often to check for new chunks to queue:
    int refreshChunkTimer = 60;
    int refreshChunkTimerIndex;


    //The player!
    public Transform player;
    private Collider playerCollider;

    //menu settings:
    bool loading;
    float loadingTime = 0;
    public GameObject loadingScreen;
    public GameObject cursor;
    public GameObject menuCamera;
    public UnityEngine.UI.Toggle wireframeUI;
    public UnityEngine.UI.Slider worldHeightUI;
    bool isWireframe;
    public TMP_InputField seedUI;
    public Material wireMeshMaterial;

    //Called via menu to begin map generation.
    //Takes the configurable settings from the option menu.
    //Schedules a ChunkJob for the first viewable chunks within the drawDistance.

    //Seed is calculated via hashing the supplied string.
    public void StartMap()
    {
        playerCollider = player.GetComponent<Collider>();
        loadingScreen.SetActive(true);

        chunkDictionary = new Dictionary<string, ChunkData>();
        chunkJobs = new List<ChunkJobHandler>();

        mapZScale = 10 * (int)worldHeightUI.value;
        isWireframe = wireframeUI.isOn;
        seed = seedUI.text.Length <0 ? seedUI.text.GetHashCode() : new System.Random().Next(0,10000);
        seed = seed % 1000;

        int playerX = (int) (player.transform.position.x/(chunkWidth*(3f/4f)));
        int playerZ = (int) (player.transform.position.z/(chunkWidth*Mathf.Sqrt(3)/2));

        for (int x = playerX - drawDistance; x < playerX + drawDistance; x++)
        {
            for (int y = playerZ - drawDistance; y < playerZ + drawDistance; y++)
            {
                chunkJobs.Add(ScheduleChunkJob(x, y));
            }
        }
        loading = true;
        ProcessRunningJobs();
    }

    //Every refreshChunkTimer frame, we check for chunks to be enabled/disabled.
    //If a chunk should be enabled, but does not exist, we schedule a ChunkJob to build it for the first time.

    //We also manage current jobs by calling ProcessRunningJobs
    public void Update()
    {
        ProcessRunningJobs();
        LoadingCheck();

        if(++refreshChunkTimerIndex > refreshChunkTimer){
            refreshChunkTimerIndex = 0;
            QueueNewChunks();
            DisableFarChunks();
        }
    }

    //For the loading screen transition.
    private void LoadingCheck(){
        if(loading && chunkJobs.Count==0){
            loadingTime += Time.deltaTime;
            if(loadingTime>1f){
                loading = false;
                player.gameObject.SetActive(true);
                loadingScreen.SetActive(false);
                cursor.SetActive(true);
                menuCamera.SetActive(false);
            }
        }

    }

    public void QueueNewChunks(){
        Vector2Int chunkCoordinates = GetChunkCoordinates(player.transform.position.x, player.transform.position.z);
        for(int x = chunkCoordinates.x-drawDistance; x < chunkCoordinates.x+drawDistance; x++){
            for(int z = chunkCoordinates.y-drawDistance; z < chunkCoordinates.y+drawDistance; z++){
                if(ChunkExists(x,z)){
                    GetChunk(x,z).gameObject.SetActive(true);
                }else{
                    chunkJobs.Add(ScheduleChunkJob(x,z));
                }       
            }
        }
    }

    public void DisableFarChunks(){
        int playerX = (int) (player.transform.position.x/(chunkWidth*(3f/4f)));
        int playerZ = (int) (player.transform.position.z/(chunkWidth*Mathf.Sqrt(3)/2));

        foreach(ChunkData chunk in chunkDictionary.Values){
            if(Mathf.Abs(chunk.worldX - playerX) > drawDistance*1.5 || Mathf.Abs(chunk.worldZ - playerZ) > drawDistance *1.5){
                chunk.gameObject.SetActive(false);
            }
        }
    }

    //Check jobs for completion.
    //On completion, we can assign its calculated vertices to the chunk's mesh, and dispose of the intermediary data structures from the job.
    public void ProcessRunningJobs(){
        foreach(ChunkJobHandler job in chunkJobs){
            if(job.IsCompleted()){
                job.Complete();
                if(ChunkExists(job.x,job.z)){
                    ChunkData chunk = GetChunk(job.x,job.z);
                    chunk.GenerateMesh(job.vertexNativeList.ToArray(Allocator.Temp).ToArray(), job.uvNativeList.ToArray(Allocator.Temp).ToArray(), job.triangleNativeList.ToArray(Allocator.Temp).ToArray(), job.blockTypeMap);
                }else{
                    Debug.LogError("ERROR: Completed Job has no gameobject");
                }

                job.ClearMemory();
                chunkJobs.Remove(job);
                return;            
            }
        }
    }

    //Schedule job for given x and z coordinate chunk.
    //We can determine whether it should be a ChunkJob or RebuildChunkJob from whether there's an existing key for the coordinates in the chunkDictionary.
    public ChunkJobHandler ScheduleChunkJob(int x, int z)
    {
        //Rebuild Job:
        ChunkData chunkData;
        bool isRebuildJob = ChunkExists(x,z);

        if(isRebuildJob){
            chunkData = GetChunk(x,z);
        }else{
            //Generate first mesh:
            GameObject currentChunkObject = Instantiate(ChunkPrefab, transform.position, transform.rotation, transform);
            chunkData = currentChunkObject.GetComponent<ChunkData>();
            chunkData.worldX = x;
            chunkData.worldZ = z;

            currentChunkObject.name = chunkData.getIndexKey();
            chunkDictionary.Add(currentChunkObject.name, chunkData);

            float xPos = chunkWidth * x * (3f/4f);
            float zPos = chunkWidth * z * Mathf.Sqrt(3)/2 + x%2 * (chunkWidth%2) * Mathf.Sqrt(3)/4;

            currentChunkObject.transform.position = new Vector3(xPos, 0, zPos);

            if(isWireframe){
            currentChunkObject.GetComponent<MeshRenderer>().material = wireMeshMaterial;   
            }
        }

        NativeList<float3> vertices = new NativeList<float3>(12 * chunkWidth * chunkWidth * chunkHeight, Allocator.Persistent);
        NativeList<float2> uvs = new NativeList<float2>(12 * chunkWidth * chunkWidth * chunkHeight, Allocator.Persistent);
        NativeList<int> triangles = new NativeList<int>(60 * chunkWidth * chunkWidth * chunkHeight, Allocator.Persistent);
        NativeHashMap<float3, int> blockTypeMap = isRebuildJob ? chunkData.blockType : new NativeHashMap<float3, int>(chunkWidth * chunkWidth * chunkHeight, Allocator.Persistent);

        //get adjacent chunk data, if it exists. Otherwise set to an empty map.
        bool forwardChunkExists = ChunkExists(x,z+1) && chunkDictionary[getChunkKey(x, z+1)].isGenerated;
        bool leftChunkExists = ChunkExists(x-1,z) && chunkDictionary[getChunkKey(x-1, z)].isGenerated;
        bool rightChunkExists = ChunkExists(x+1,z) && chunkDictionary[getChunkKey(x+1, z)].isGenerated;
        bool backwardChunkExists = ChunkExists(x,z-1)  && chunkDictionary[getChunkKey(x, z-1)].isGenerated;

        NativeHashMap<float3, int> localForwardBlockTypeMap = forwardChunkExists ? chunkDictionary[getChunkKey(x, z+1)].blockType : new NativeHashMap<float3, int>(0, Allocator.TempJob);
        NativeHashMap<float3, int> localLeftChunkBlockTypeMap = leftChunkExists ? chunkDictionary[getChunkKey(x-1, z)].blockType : new NativeHashMap<float3, int>(0, Allocator.TempJob);
        NativeHashMap<float3, int> localRightBlockTypeMap = rightChunkExists ? chunkDictionary[getChunkKey(x+1, z)].blockType : new NativeHashMap<float3, int>(0, Allocator.TempJob);
        NativeHashMap<float3, int> localBackwardBlockTypeMap = backwardChunkExists ? chunkDictionary[getChunkKey(x, z-1)].blockType : new NativeHashMap<float3, int>(0, Allocator.TempJob);
        if(isRebuildJob){
            RebuildChunkJob chunkJob = new(){
                width = chunkWidth,
                height = chunkHeight,
                worldX = x,
                worldZ = z,
                scale = mapZoom,
                zScale = mapZScale,
                seed = seed,

                vertexNativeList = vertices,
                uvNativeList = uvs,
                triangleNativeList = triangles,
                blockTypeMap = blockTypeMap,

                forwardBlockTypeMap = localForwardBlockTypeMap,
                backBlockTypeMap = localBackwardBlockTypeMap,
                rightBlockTypeMap = localRightBlockTypeMap,
                leftBlockTypeMap = localLeftChunkBlockTypeMap
            };
            return new ChunkJobHandler(chunkJob, true, chunkJob.Schedule(), vertices, uvs, triangles, blockTypeMap, x, z);
        }else{
            ChunkJob chunkJob = new(){
                width = chunkWidth,
                height = chunkHeight,
                worldX = x,
                worldZ = z,
                scale = mapZoom,
                zScale = mapZScale,
                seed = seed,

                vertexNativeList = vertices,
                uvNativeList = uvs,
                triangleNativeList = triangles,
                blockTypeMap = blockTypeMap,

                forwardBlockTypeMap = localForwardBlockTypeMap,
                backBlockTypeMap = localBackwardBlockTypeMap,
                rightBlockTypeMap = localRightBlockTypeMap,
                leftBlockTypeMap = localLeftChunkBlockTypeMap
            };
            return new ChunkJobHandler(chunkJob, false, chunkJob.Schedule(), vertices, uvs, triangles, blockTypeMap, x, z);
        }
    }

    public ChunkData GetChunk(int xIndex, int zIndex)
    {
        ChunkData temp = null;
        chunkDictionary.TryGetValue(getChunkKey(xIndex,zIndex), out temp);
        return temp;
    }

    public bool ChunkExists(int xIndex, int zIndex){
        return chunkDictionary.ContainsKey(xIndex + "," + zIndex);
    }

    public String getChunkKey(int x, int z){
        return x + "," + z;
    }

    public Vector2Int GetChunkCoordinates(float x, float z){
        return new Vector2Int((int) (player.transform.position.x/(chunkWidth*(3f/4f))),(int) (player.transform.position.z/(chunkWidth*Mathf.Sqrt(3)/2)));
    }

    void OnApplicationQuit(){
        foreach(ChunkData chunk in chunkDictionary.Values){
            chunk.Dispose();
        }
    }

    //Given block type, a world position, and the chunk hit via raycast,
    //Determine which block to delete.
    //This was quite a bit of trouble to code. We need to account for things like:
    // - The interlapping nature of a grid of hexes. One x coordinate can correspond to two different rows in the chunk array.
    // - Placement into other chunks. If we click to add onto a border of a chunk, it should add to that chunk's neighbor.
    // - Placement into player collsion This results in the player falling out of the world. Not good!

    //Solving the above edge cases, the method determines a hex index for a chunk, and schedules a job to update the chunk's mesh (RebuildChunkJob)
    //Because we don't want any delay between player action and visual update, we force the job to complete and update the mesh accordingly.
    public void SetBlock(Vector3 rayHitPosition, ChunkData chunkHit, int blockType, bool secondCall){
        //if placing a block and block would be inside player, ignore:
        //This is just a dirty easy check to save us time if the player is in the middle of the hex.
        //We'll need to do a more thorough test later on once we've determined the adding hex's index.
        if(blockType > 0 && player.GetComponent<Collider>().bounds.Contains(rayHitPosition)){
            return;
        }

        Vector3 hexIndex = CalculateHexIndexInsideChunk(chunkHit, rayHitPosition, blockType);

        //Y bounds check:
        if(hexIndex.y == 0 || hexIndex.y == chunkHeight){
            return;
        }

        //Check for adjacent chunk action:
        if(hexIndex.x < 0 || hexIndex.x >= chunkWidth || hexIndex.z < 0 || hexIndex.z >= chunkWidth){
            //placing into adjacent block.
            if(blockType > 0 && !secondCall){
                int adjacentX = chunkHit.worldX;
                int adjacentZ = chunkHit.worldZ;

                if(hexIndex.x < 0){
                    adjacentX--;
                }else if(hexIndex.x >= chunkWidth){
                    adjacentX++;
                }

                if(hexIndex.z < 0){
                    adjacentZ--;
                }else if(hexIndex.z >= chunkWidth){
                    adjacentZ++;
                }

                if(ChunkExists(adjacentX,adjacentZ)){
                    SetBlock(rayHitPosition,GetChunk(adjacentX,adjacentZ),blockType,true);
                }
                return;
            }else{
                hexIndex.x = Mathf.Clamp(hexIndex.x,0,chunkWidth);
                hexIndex.z = Mathf.Clamp(hexIndex.z,0,chunkWidth);
            }
        }

        //final check for placement of block inside player:
        //Check the top, middle, and bottom hex of the player.
        //This should be sufficient to determine whether there's any overlap.
        if(blockType > 0){
            Vector3 playerHexIndexMid = CalculateHexIndexInsideChunk(chunkHit, player.position, 1);
            Vector3 playerHexIndexTop = CalculateHexIndexInsideChunk(chunkHit, player.position + Vector3.up*0.5f, 1);
            Vector3 playerHexIndexBot = CalculateHexIndexInsideChunk(chunkHit, player.position + Vector3.down*0.5f, 1);

            if(Vector3.Distance(playerHexIndexMid, hexIndex) < 1 || Vector3.Distance(playerHexIndexTop, hexIndex) < 1 || Vector3.Distance(playerHexIndexBot, hexIndex) < 1){
                return;
            }
        }

        //Finally, update the calculated index to the given block type. Schedule job to update mesh.
        chunkHit.blockType[hexIndex] = blockType;        
        ChunkJobHandler job = ScheduleChunkJob(chunkHit.worldX, chunkHit.worldZ);
        job.Complete();
        chunkHit.GenerateMesh(job.vertexNativeList.ToArray(Allocator.Temp).ToArray(), job.uvNativeList.ToArray(Allocator.Temp).ToArray(), job.triangleNativeList.ToArray(Allocator.Temp).ToArray(), job.blockTypeMap);
        job.ClearMemory();

        //if editing a chunk on the border, we need to update the adjacent chunk's visibility
        if(hexIndex.x == 0){
            chunkJobs.Add(ScheduleChunkJob(chunkHit.worldX-1, chunkHit.worldZ));
        }

        if(hexIndex.x == chunkWidth-1){
            chunkJobs.Add(ScheduleChunkJob(chunkHit.worldX+1, chunkHit.worldZ));
        }

        if(hexIndex.z == 0){
            chunkJobs.Add(ScheduleChunkJob(chunkHit.worldX, chunkHit.worldZ-1));
        }

        if(hexIndex.z == chunkWidth-1){
            chunkJobs.Add(ScheduleChunkJob(chunkHit.worldX, chunkHit.worldZ+1));
        }
    }

    //Method for determing the hex index of a chunk given a world position.
    //Once we have transitioned the world coordinates to hex coordinates, we know that the true hex is the one with the closest center.
    //So loop through the 9 closest and return the one with the least distance.
    private Vector3 CalculateHexIndexInsideChunk(ChunkData chunk, Vector3 position, int blockType){
        float chunkWorldX = chunk.worldX * chunkWidth*(3f/4f);
        float chunkWorldZ = chunk.worldZ * chunkWidth*Mathf.Sqrt(3)/2;

        float hitXCoord = position.x - chunkWorldX;
        float hitZCoord = position.z - chunkWorldZ;

        int indexX = Mathf.Clamp((int) Mathf.Floor(hitXCoord / (3f/4f)),0,chunkWidth);
        int indexY = (int) Mathf.Floor(position.y /0.5f);
        int indexZ = Mathf.Clamp((int) Mathf.Floor((hitZCoord + indexX%2*(Mathf.Sqrt(3)/4))/ (Mathf.Sqrt(3)/2)),0,chunkWidth);

        int closestX = indexX;
        int closestZ = indexZ;

        Vector2 remainderFloat = new(hitXCoord, hitZCoord);
        float closestDistance = Mathf.Infinity;
        for(int checkX = indexX -1; checkX <= indexX + 1; checkX++){
            for(int checkZ = indexZ -1; checkZ <= indexZ + 1;  checkZ++){
                float checkDistance = Vector2.Distance(remainderFloat,ConvertIndexToLocalHexPosition(checkX,checkZ));
                if(checkDistance < closestDistance){
                    bool satisfiesAddDeleteCheck = ((!chunk.blockType.ContainsKey(new float3(checkX,indexY,checkZ)) || chunk.blockType[new float3(checkX,indexY,checkZ)] == 0) && blockType > 0) || (blockType == 0 && chunk.blockType.ContainsKey(new float3(checkX,indexY,checkZ)) && chunk.blockType[new float3(checkX,indexY,checkZ)] > 0);
                    if(satisfiesAddDeleteCheck){
                        closestX = checkX;
                        closestZ = checkZ;
                        closestDistance = checkDistance;
                    }
                }
            }
        }
        return new Vector3(closestX,indexY, closestZ);
    }


    private Vector2 ConvertIndexToLocalHexPosition(int x, int z){
        return new Vector2(x*0.75f,z*Mathf.Sqrt(3)/2+x%2*Mathf.Sqrt(3)/4);
    }
}
