using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OlharPlayer : MonoBehaviour
{
    public Camera cam;
    private float rotacaoX;

    public float sensibilidadeX = 30f;
    public float sensibilidadeY = 30f;

    public void FirstPerson(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;

        //Calcula a rotação da camera para cima e para baixo
        rotacaoX -= (mouseY * Time.deltaTime)*sensibilidadeY;
        rotacaoX = Mathf.Clamp(rotacaoX, -80f, 80f);

        //Aplica o calculo na camera
        cam.transform.localRotation = Quaternion.Euler(rotacaoX, 0, 0);
        //Rotaciona o player junto da camera
        transform.Rotate(Vector3.up * (mouseX * Time.deltaTime) * sensibilidadeX);
    }
 
}
