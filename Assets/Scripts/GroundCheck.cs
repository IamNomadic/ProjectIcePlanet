using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    public bool Grounded;
    public Collider col;
    private void OnCollisionStay(Collision collision)
    {
        Grounded = true;
        foreach (ContactPoint contact in collision.contacts)
        {
            Debug.DrawRay(contact.point, contact.normal * 10, Color.white);
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        Grounded = false;
    }
    
}
