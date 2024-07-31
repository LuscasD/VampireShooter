using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class DanoDaArma : MonoBehaviour
{
    public int impacto;
    public int impactoParacima;

    public float distanciaDoTiro;

    private Transform playerCamera;

    private void Start()
    {
        //Define a camera do player como a camera main
        playerCamera = Camera.main.transform;
    }

    public void Tiro()
    {
        Ray gunRay = new Ray(playerCamera.position, playerCamera.forward);
        
        if(Physics.Raycast(gunRay, out RaycastHit hitInfo, distanciaDoTiro))
        {
            VampFisica fisica = hitInfo.collider.GetComponentInParent<VampFisica>();

            if(fisica != null)
            {
                Vector3 direcaoDoImpacto = fisica.transform.position - playerCamera.transform.position;
                direcaoDoImpacto.y = impactoParacima;
                direcaoDoImpacto.Normalize();

                Vector3 force = impacto * direcaoDoImpacto;
                fisica.TriggerRagdoll(force, hitInfo.point);
            }

        }
    }
}
