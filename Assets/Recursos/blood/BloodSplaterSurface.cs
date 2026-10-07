using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Quando as partículas de sangue batem no cenário, projeta manchas (Decal Projector) na superfície.
// O decal fica colado na superfície e é cortado onde ela acaba, sem flutuar nem passar da borda.
public class BloodSplaterSurface : MonoBehaviour
{
    public DecalProjector decalDeSangue;
    public Material[] manchasDeChao;
    public Material[] manchasDeParede;

    [Header("Tamanho (m)")]
    public Vector2 tamanhoNoChao = new Vector2(0.7f, 1.6f);
    public Vector2 tamanhoNaParede = new Vector2(0.6f, 1.3f);
    // Espessura da caixa de projeção. Fina demais e ela não alcança a superfície.
    public float profundidade = 0.5f;

    [Header("Quantidade")]
    public int maximoPorEfeito = 6;
    public float distanciaMinima = 0.4f;
    public static int maximoNaCena = 80;

    // As mais antigas somem quando passa do limite
    private static readonly Queue<GameObject> manchasNaCena = new Queue<GameObject>();

    private ParticleSystem ps;
    private readonly List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
    private readonly List<Vector3> criadas = new List<Vector3>();

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void OnParticleCollision(GameObject other)
    {
        int numCollisionEvents = ps.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numCollisionEvents && criadas.Count < maximoPorEfeito; i++)
        {
            Vector3 pos = collisionEvents[i].intersection;
            Vector3 normal = collisionEvents[i].normal.normalized;
            GrudarNaSuperficie(ref pos, ref normal);
            if (MuitoPerto(pos)) continue;

            // Chão/teto ou parede
            bool horizontal = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.7f;
            Criar(pos, normal, horizontal);
            criadas.Add(pos);
        }
    }

    // O ponto da colisão da partícula fica afastado da superfície (raio da partícula),
    // então procura o ponto exato da superfície para o decal ficar centrado nela.
    private void GrudarNaSuperficie(ref Vector3 pos, ref Vector3 normal)
    {
        if (Physics.Raycast(pos + normal * 0.3f, -normal, out RaycastHit hit, 1f, Camadas.SemPlayer, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point;
            normal = hit.normal;
        }
    }

    private bool MuitoPerto(Vector3 pos)
    {
        foreach (var p in criadas)
            if ((p - pos).sqrMagnitude < distanciaMinima * distanciaMinima) return true;
        return false;
    }

    private void Criar(Vector3 pos, Vector3 normal, bool horizontal)
    {
        Material[] opcoes = horizontal ? manchasDeChao : manchasDeParede;
        if (decalDeSangue == null || opcoes == null || opcoes.Length == 0) return;

        Vector2 faixa = horizontal ? tamanhoNoChao : tamanhoNaParede;
        float tamanho = Random.Range(faixa.x, faixa.y);

        // O decal projeta ao longo do seu eixo Z, para dentro da superfície.
        // No chão gira livre; na parede fica "em pé" para o sangue escorrer para baixo.
        Quaternion rotacao = horizontal
            ? Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Quaternion.LookRotation(-normal, Vector3.forward)
            : Quaternion.AngleAxis(Random.Range(-12f, 12f), normal) * Quaternion.LookRotation(-normal, Vector3.up);

        DecalProjector decal = Instantiate(decalDeSangue, pos, rotacao);
        decal.material = opcoes[Random.Range(0, opcoes.Length)];
        decal.size = new Vector3(tamanho, horizontal ? tamanho : tamanho * 1.3f, profundidade);
        decal.pivot = Vector3.zero;

        manchasNaCena.Enqueue(decal.gameObject);
        while (manchasNaCena.Count > maximoNaCena)
        {
            GameObject antiga = manchasNaCena.Dequeue();
            if (antiga != null) Destroy(antiga);
        }
    }
}
