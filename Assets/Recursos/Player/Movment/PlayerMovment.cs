using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovment : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 velocidade;
    public float speed = 8;

    private bool isGrounded;
    public float gravidade = -10;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }


    public void Movment(Vector2 input) 
    {
        Vector3 moveDirection = Vector3.zero;
        moveDirection.x = input.x;
        moveDirection.z = input.y;
        controller.Move(transform.TransformDirection(moveDirection) * speed * Time.deltaTime);

        velocidade.y += gravidade * Time.deltaTime;
        if(isGrounded && velocidade.y < 0)
        {
            velocidade.y = -2f;
        }

        controller.Move(velocidade * Time.deltaTime);
    }
}
