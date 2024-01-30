using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//The basic FPS functionality of this script was taken from the tutorial:
//https://www.sharpcoderblog.com/blog/unity-3d-fps-controller 
//That said, the methods specific to this game, CheckBlockActions and AttemptQueueBlockUpdate, were part of my programming.
public class FPSController : MonoBehaviour
{
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    [HideInInspector]
    public bool canMove = true;

    public Vector3 currentRotation;

    public WorldGenerator worldGenerator;
    private LayerMask layerMask = (1 << 0);
    private ChunkData[] adjacentChunks;
    private ChunkData targetedChunk;
    private int maxDistance = 20;

    int playerX;
    int playerZ;
    public Material highlightedMaterial;
    public Material normalMaterial;
    private int SelectedBlock = 1;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // We are grounded, so recalculate move direction based on axes
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        // Press Left Shift to run
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpSpeed;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        // Apply gravity. Gravity is multiplied by deltaTime twice (once here, and once below
        // when the moveDirection is multiplied by deltaTime). This is because gravity should be applied
        // as an acceleration (ms^-2)
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        // Move the controller
        characterController.Move(moveDirection * Time.deltaTime);

        // Player and Camera rotation
        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }

        CheckBlockActions();
    }

    void CheckBlockActions(){
        if(Input.mouseScrollDelta.y>0){
            SelectedBlock++;
            if(SelectedBlock>10) SelectedBlock = 1;
        } else if(Input.mouseScrollDelta.y<0){
            SelectedBlock--;
            if(SelectedBlock<1) SelectedBlock = 10;
        }

        if(Input.GetButtonDown("Fire1")){
            AttemptQueueBlockUpdate(0);
        }else if(Input.GetButtonDown("Fire2")){
            AttemptQueueBlockUpdate(SelectedBlock);
        }
    }

    void AttemptQueueBlockUpdate(int blockType){
        RaycastHit rayHit;
        Vector3 direction = playerCamera.transform.rotation.normalized * Vector3.forward;
        if(Physics.Raycast(playerCamera.transform.position, direction, out rayHit, maxDistance, layerMask)){
            ChunkData chunk = rayHit.transform.GetComponent<ChunkData>();
            Vector3 roughlyCenterHex = rayHit.point;
            if(blockType == 0){
                //hitting block to destroy, so reverse normal to reach its middle
                roughlyCenterHex -= new Vector3(rayHit.normal.x * 0.5f, rayHit.normal.y * 0.1f, rayHit.normal.z * 0.5f);
            }else{
                //hitting neighbor block, add normal to reach desired add index
                roughlyCenterHex += new Vector3(rayHit.normal.x * 0.5f, rayHit.normal.y * 0.1f, rayHit.normal.z * 0.5f);

            }
            worldGenerator.SetBlock(roughlyCenterHex, chunk,blockType,false);
        }
    }
}