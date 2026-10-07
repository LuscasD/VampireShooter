using UnityEngine;

// Fica em cada osso do ragdoll: prende o osso na estaca quando o corpo é arremessado contra ela.
// Ossos vitais (peito e cabeça) matam o vampiro de vez; os outros ele consegue arrancar ao levantar.
public class EstacaHandler : MonoBehaviour
{
    [Tooltip("Peito e cabeça: ser perfurado aqui mata o vampiro de vez")]
    public bool vital;
    [Tooltip("Velocidade mínima do osso para a estaca perfurar")]
    public float velocidadeMinima = 2.5f;

    public bool Preso { get; private set; }

    private Rigidbody rigidBody;
    private RagDollScript ragDoll;
    private VampireScript vamFisica;
    private Vector3 impaledPosition;
    private Quaternion rotationImpaled;
    private float podePrenderEm;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        ragDoll = GetComponentInParent<RagDollScript>();
        vamFisica = GetComponentInParent<VampireScript>();
    }

    private void FixedUpdate()
    {
        if (Preso)
        {
            // Mantém o osso cravado mesmo se o ragdoll for reativado por outro tiro
            rigidBody.isKinematic = true;
            rigidBody.MovePosition(impaledPosition);
            rigidBody.MoveRotation(rotationImpaled);
        }
    }

    // Estacas podem ser trigger (atravessa) ou sólidas (crava onde bateu)
    private void OnTriggerEnter(Collider other)
    {
        TentarPrender(other, rigidBody.velocity.magnitude);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TentarPrender(collision.collider, collision.relativeVelocity.magnitude);
    }

    private void TentarPrender(Collider outro, float velocidade)
    {
        // Só perfura um corpo sendo arremessado (não um vampiro andando perto da estaca)
        if (Preso || !ragDoll.Ativo || ragDoll.Levantando || Time.time < podePrenderEm) return;
        if (outro.GetComponent<Estaca>() == null) return;
        if (velocidade < velocidadeMinima) return;

        rigidBody.isKinematic = true;
        impaledPosition = rigidBody.position;
        rotationImpaled = rigidBody.rotation;
        Preso = true;

        if (vital) vamFisica.morreuPraSempre = true;
    }

    // Arranca o osso da estaca com um puxão
    public void Soltar(Vector3 puxao)
    {
        if (!Preso) return;
        Preso = false;
        rigidBody.isKinematic = false;
        rigidBody.AddForce(puxao, ForceMode.VelocityChange);
        podePrenderEm = Time.time + 2f;
    }
}
