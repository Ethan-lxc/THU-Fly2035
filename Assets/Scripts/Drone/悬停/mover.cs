using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mover : MonoBehaviour
{
    // Start is called before the first frame update
    public float speed = 10;
    public Rigidbody2D rb;
   

    // Update is called once per frame
    void FixedUpdate()
    {
        float horizontal= Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        rb.velocity = new Vector2(horizontal * speed, vertical * speed);
    }
}
