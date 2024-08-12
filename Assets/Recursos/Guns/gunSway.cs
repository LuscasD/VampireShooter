using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gunSway : MonoBehaviour
{
    [Header("Sway settings")]
    [SerializeField] private float smooth;
    [SerializeField] private float swayMultiplier;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Pega o input do mouse
        float mouseX = Input.GetAxisRaw("Mouse X") * swayMultiplier;
        float mouseY = Input.GetAxisRaw("Mouse Y") * swayMultiplier;

        //calcula a rotação do alvo

        Quaternion rotationX = Quaternion.AngleAxis(-mouseY, Vector3.right);
        Quaternion rotationY = Quaternion.AngleAxis(mouseX, Vector3.forward);

        Quaternion targetRotation = rotationX = rotationY;

        // rotaciona o alvo
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smooth * Time.deltaTime);
    }
}
