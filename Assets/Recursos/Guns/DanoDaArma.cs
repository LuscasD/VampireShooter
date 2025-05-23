using System.Collections;
using System.Collections.Generic;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine;

public class DanoDaArma : MonoBehaviour
{
    public int impacto;
    public int impactoParacima;

    private float tempoQueOMouseFoiSegurado;

    public float shootDistance;

    private Transform playerCamera;

    // VFX
    public GameObject bullethol;
    public GameObject bloodVFX;

    public AudioManager audioManager;

    private void Start()
    {
        //Define a camera do player como a camera main
        playerCamera = Camera.main.transform;
    }

    public void Tiro()
    {
        Ray gunRay = new Ray(playerCamera.position, playerCamera.forward);

        audioManager.Play("tiro");

        if (Physics.Raycast(gunRay, out RaycastHit hitInfo, shootDistance))
        {
            if (hitInfo.transform.gameObject.tag == "Inimigo")
            {
                RagDollScript fisica = hitInfo.collider.GetComponentInParent<RagDollScript>();

                if (fisica != null)
                {
                    //Calcula dire��o do impacto
                    Vector3 direcaoDoImpacto = fisica.transform.position - playerCamera.transform.position;
                    direcaoDoImpacto.y = impactoParacima;
                    direcaoDoImpacto.Normalize();

                    Vector3 force = impacto * direcaoDoImpacto;
                    fisica.ReceberImpacto(force, hitInfo.point);

                    // instancia o sangue
                    GameObject blood = Instantiate(bloodVFX, hitInfo.point + (hitInfo.normal * 0.025f), Quaternion.identity) as GameObject;
                    blood.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
                }
            }

            // instancia buraco de bala
            if (hitInfo.transform.gameObject.tag == "cenario")
            {
                GameObject decalObject = Instantiate(bullethol, hitInfo.point + (hitInfo.normal * 0.025f), Quaternion.identity) as GameObject;
                decalObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            }

        }
    }
}
