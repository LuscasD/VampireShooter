using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RagDollScript : MonoBehaviour
{

    public class BoneTransform
    {
        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }
    }


    private Rigidbody[] _ragdollRigidbodies;
    private Animator animRagDoll;
    private NavMeshAgent ragDollAgent;

    public float tempoParaLevantar;

    private Transform ossoDaCintura;


    public bool vampiro;

    private BoneTransform[] _standUpBoneTransform;
    private BoneTransform[] _ragDollBoneTransform;
    private Transform[] _bones;

    // Start is called before the first frame update
    void Awake()
    {
        animRagDoll = GetComponent<Animator>();
        ragDollAgent = GetComponent<NavMeshAgent>();

        ossoDaCintura = animRagDoll.GetBoneTransform(HumanBodyBones.Hips);

        _bones = ossoDaCintura.GetComponentsInChildren<Transform>();
        _standUpBoneTransform = new BoneTransform[_bones.Length];
        _ragDollBoneTransform = new BoneTransform[_bones.Length];

        for (int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
        {
            _standUpBoneTransform[boneIndex] = new BoneTransform();
            _ragDollBoneTransform[boneIndex] = new BoneTransform();
        }

        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        DisabiltarRagdoll();
    }

 ///////////Recebe impacto em uma area e derruba com o RagDoll
    public void ReceberImpacto(Vector3 force, Vector3 hitPoint)
    {
        HabilitarRagdoll();

        Rigidbody rigAtingido = rigidBodyAcertado(hitPoint);
        rigAtingido.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);

        if (vampiro)
        {
            VampireScript script = GetComponent<VampireScript>();

            script.estadoAtual = VampireScript.VampireStates.Ragdoll;
            script.tempoParaLevantar = Random.Range(5, 10);
        }
        
    }
    private Rigidbody rigidBodyAcertado(Vector3 hitPoint)
    {
        Rigidbody rigidBodyMaisProxima = null;
        float distanciaMaisProxima = 0;

        foreach (var rigidbody in _ragdollRigidbodies)
        {
            float distancia = Vector3.Distance(rigidbody.position, hitPoint);

            if (rigidBodyMaisProxima == null || distancia < distanciaMaisProxima)
            {
                distanciaMaisProxima = distancia;
                rigidBodyMaisProxima = rigidbody;
            }
        }

        return rigidBodyMaisProxima;

    }

///////////////////////////////////////////////////////////////

    public void DisabiltarRagdoll()
    {
        foreach (var rigidBody in _ragdollRigidbodies)
        {
            rigidBody.isKinematic = true;

            animRagDoll.enabled = true;
            ragDollAgent.enabled = true;
        }
    }

    public void HabilitarRagdoll()
    {
        foreach (var rigidBody in _ragdollRigidbodies)
        {
            rigidBody.isKinematic = false;
        }

        animRagDoll.enabled = false;
        ragDollAgent.enabled = false;

    }

    //Alinha o modelo a nova posição depois do RagDoll
    public void AlinharPosição()
    {
        Vector3 posiçãoOriginalDaCintura = ossoDaCintura.position;
        transform.position = ossoDaCintura.position;

        //Garante que o personagem esta no chão
       if(Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo))
        {
            transform.position = new Vector3(transform.position.x, hitInfo.point.y, transform.position.z);
        }

        ossoDaCintura.position = posiçãoOriginalDaCintura;
    }

    public void PoplarTransformDosBones(BoneTransform[] boneTransform)
    {
        for(int boneIndex = 0; boneIndex < _bones.Length; boneIndex++)
        {
            boneTransform[boneIndex].Position = _bones[boneIndex].localPosition;
            boneTransform[boneIndex].Rotation = _bones[boneIndex].localRotation;
        }
    }
}
