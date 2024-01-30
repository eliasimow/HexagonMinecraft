using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
For the loading Screen :)
*/
public class Spin : MonoBehaviour
{
    void Update()
    {
        transform.eulerAngles = transform.eulerAngles + new Vector3(0,0,Time.deltaTime*-45f);   
    }
}
