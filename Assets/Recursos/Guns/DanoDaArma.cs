using UnityEngine;

public class DanoDaArma : MonoBehaviour
{
    public int impacto;
    [Tooltip("Ângulo (graus) em que o vampiro é lançado para cima ao levar o tiro, em arco")]
    [Range(0f, 80f)] public float anguloDeLancamento = 25f;
    [Tooltip("Multiplicador do impacto pela distância do tiro (m). À queima-roupa o vampiro voa mais longe")]
    public AnimationCurve impactoPorDistancia = new AnimationCurve(
        new Keyframe(0f, 3f), new Keyframe(2f, 2.2f), new Keyframe(8f, 1f), new Keyframe(25f, 0.5f));
    public float forcaEmObjetos = 8f;
    public float shootDistance;
    [Tooltip("Tolerância: inimigos a essa distância da linha do tiro também são acertados")]
    public float raioDaBala = 0.15f;

    // VFX
    public GameObject bullethol;
    public GameObject bloodVFX;

    // Definida pela Gun antes de cada tiro (graus)
    [HideInInspector] public float dispersao;

    private Transform playerCamera;

    private void Start()
    {
        playerCamera = Camera.main.transform;
    }

    public void Tiro()
    {
        // Aplica a dispersão em um cone ao redor do centro da tela
        Vector2 desvio = Random.insideUnitCircle * Mathf.Tan(dispersao * Mathf.Deg2Rad);
        Vector3 direcao = (playerCamera.forward + playerCamera.right * desvio.x + playerCamera.up * desvio.y).normalized;

        Vector3 origem = playerCamera.position;
        bool acertou = Physics.Raycast(origem, direcao, out RaycastHit hitInfo, shootDistance, Camadas.SemPlayer, QueryTriggerInteraction.Ignore);
        RagDollScript fisica = acertou ? hitInfo.collider.GetComponentInParent<RagDollScript>() : null;

        // Bala "grossa": se passou raspando por um inimigo (antes de bater na parede), conta como acerto
        if (fisica == null && raioDaBala > 0f &&
            Physics.SphereCast(origem, raioDaBala, direcao, out RaycastHit raspao, acertou ? hitInfo.distance : shootDistance, Camadas.SemPlayer, QueryTriggerInteraction.Ignore))
        {
            RagDollScript inimigo = raspao.collider.GetComponentInParent<RagDollScript>();
            if (inimigo != null)
            {
                fisica = inimigo;
                hitInfo = raspao;
                acertou = true;
            }
        }

        if (!acertou) return;

        if (fisica != null)
        {
            // Lança em arco na direção do tiro (ignora se o tiro veio de cima ou de baixo);
            // quanto mais perto, mais forte
            Vector3 horizontal = new Vector3(direcao.x, 0f, direcao.z).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, horizontal);
            Vector3 direcaoDoImpacto = Quaternion.AngleAxis(-anguloDeLancamento, lado) * horizontal;
            float forca = impacto * impactoPorDistancia.Evaluate(hitInfo.distance);
            fisica.ReceberImpacto(forca * direcaoDoImpacto, hitInfo.point);

            Instanciar(bloodVFX, hitInfo);
            return;
        }

        // Empurra objetos com física; superfícies fixas (chão, paredes...) ganham buraco de bala
        if (hitInfo.rigidbody != null)
            hitInfo.rigidbody.AddForceAtPosition(direcao * forcaEmObjetos, hitInfo.point, ForceMode.Impulse);
        else
            Instanciar(bullethol, hitInfo);
    }

    // Cria o efeito alinhado com a superfície atingida (chão, parede ou teto), com um giro aleatório
    private void Instanciar(GameObject prefab, RaycastHit hit)
    {
        if (prefab == null) return;
        Quaternion rotacao = Quaternion.AngleAxis(Random.Range(0f, 360f), hit.normal) * Quaternion.FromToRotation(Vector3.up, hit.normal);
        Instantiate(prefab, hit.point + hit.normal * 0.025f, rotacao);
    }
}
