using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerInputActions : MonoBehaviour
{
    private Movement Input;
    
    public CharacterMovement cM;// MovementScript

    // Start is called before the first frame update
    void OnEnable()
    {

        
        if (Input == null)
        {
            Input = new Movement();
            //Input.Move.Move.performed += i => cM.Move(i.ReadValue<Vector2>());
         
        }

        Input.Enable();
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
