using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
    public Rigidbody RB;
    Vector3 velocity;


    public void Move(Vector2 context)
    {
        Debug.Log("gmaing");
        velocity.x = context.x * 0.1f;
        velocity.z = context.y * 0.1f;
        transform.Translate(velocity * 0.5f);
    }
}
