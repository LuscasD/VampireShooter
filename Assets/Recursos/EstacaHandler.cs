using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EstacaHandler : MonoBehaviour
{
    private Rigidbody rigidBody;
    private Vector3 impaledPosition;
    private Quaternion rotationImpaled;
    private bool isImpaled;
    private VampFisica vamFisica;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        vamFisica = GetComponentInParent<VampFisica>();
    }

    private void FixedUpdate()
    {
        if (isImpaled)
        {
            rigidBody.MovePosition(impaledPosition);
            rigidBody.MoveRotation(rotationImpaled);
        }
    }


    private void OnTriggerEnter(Collider other)
    {
       if (other.GetComponent<Estaca>())
        {
            rigidBody.isKinematic = true;
            impaledPosition = rigidBody.position;
            rotationImpaled = rigidBody.rotation;
            isImpaled = true;
            vamFisica.Impale();
        }
    }
}
