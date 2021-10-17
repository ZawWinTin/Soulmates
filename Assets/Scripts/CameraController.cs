using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform playerTrasform;
    public Rigidbody2D playerRigidbody2D;

    // LateUpdate for Camera
    void LateUpdate()
    {
        //Don't follow Player when player fall down
        if (playerRigidbody2D.gravityScale == 0)
        {
            //Make camera follow to player without moving z position
            transform.position = new Vector3(playerTrasform.position.x, playerTrasform.position.y, transform.position.z);
        }            
    }
}
