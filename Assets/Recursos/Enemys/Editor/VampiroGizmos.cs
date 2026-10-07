using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Desenha na Scene View a visão, o estado, os alvos e a patrulha do vampiro,
// além dos barulhos que o player faz
public static class VampiroGizmos
{
    static readonly Color corPatrulha = new Color(0.3f, 0.9f, 0.4f);
    static readonly Color corSuspeito = new Color(1f, 0.85f, 0.1f);
    static readonly Color corInvestigando = new Color(1f, 0.5f, 0.1f);
    static readonly Color corPerseguindo = new Color(1f, 0.15f, 0.15f);
    static readonly Color corCaido = new Color(0.6f, 0.6f, 0.6f);
    static readonly Color corBarulho = new Color(0.3f, 0.8f, 1f);

    static GUIStyle estiloTexto;

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void Desenhar(VampireScript v, GizmoType tipo)
    {
        bool selecionado = (tipo & GizmoType.Selected) != 0;
        bool jogando = Application.isPlaying;
        bool ativo = !jogando || v.EstadoAtual < VampireScript.VampireStates.Ragdoll;
        Color cor = CorDoEstado(v);
        Vector3 olho = PosicaoDosOlhos(v);
        Vector3 pe = v.transform.position + Vector3.up * 0.05f;

        if (ativo) DesenharCone(v, olho, pe, cor, selecionado);

        if (jogando)
        {
            DesenharLinhaAtePlayer(v, olho);
            DesenharAlvos(v, pe);
            if (selecionado) DesenharCaminho(v);
        }

        if (selecionado || !jogando) DesenharPatrulha(v, jogando);
        DesenharRotulo(v, olho, cor, jogando);

        // Barulhos são globais: desenha só uma vez (pelo primeiro vampiro)
        if (jogando && v == Object.FindObjectOfType<VampireScript>()) DesenharBarulhos();
    }

    static Color CorDoEstado(VampireScript v)
    {
        if (!Application.isPlaying) return corPatrulha;
        switch (v.EstadoAtual)
        {
            case VampireScript.VampireStates.Suspeito: return corSuspeito;
            case VampireScript.VampireStates.Investigando: return corInvestigando;
            case VampireScript.VampireStates.Perseguindo: return corPerseguindo;
            case VampireScript.VampireStates.Patrulhando: return corPatrulha;
            default: return corCaido;
        }
    }

    static Vector3 PosicaoDosOlhos(VampireScript v)
    {
        if (v.Olhos != null) return v.Olhos.position;
        var anim = v.GetComponent<Animator>();
        Transform cabeca = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        return cabeca != null ? cabeca.position : v.transform.position + Vector3.up * 1.7f;
    }

    static Color ComAlfa(Color c, float a)
    {
        c.a = a;
        return c;
    }

    ///////////////////////////////////////////////////////////////

    static void DesenharCone(VampireScript v, Vector3 olho, Vector3 pe, Color cor, bool selecionado)
    {
        Vector3 frente = v.transform.forward;
        frente.y = 0f;
        frente.Normalize();
        float meio = v.anguloDeVisao * 0.5f;
        Vector3 borda = Quaternion.AngleAxis(-meio, Vector3.up) * frente;
        Vector3 bordaCentral = Quaternion.AngleAxis(-meio * 0.5f, Vector3.up) * frente;
        float alcance = v.alcanceDaVisao;

        // Preenchimento fica mais forte conforme o medidor de detecção enche
        float preenchimento = 0.05f + v.Deteccao * 0.15f;
        Handles.color = ComAlfa(cor, preenchimento);
        Handles.DrawSolidArc(olho, Vector3.up, borda, v.anguloDeVisao, alcance);
        // Visão central (detecta mais rápido)
        Handles.color = ComAlfa(cor, preenchimento * 0.8f);
        Handles.DrawSolidArc(olho, Vector3.up, bordaCentral, v.anguloDeVisao * 0.5f, alcance);

        // Contorno na altura dos olhos
        Handles.color = ComAlfa(cor, selecionado ? 0.9f : 0.5f);
        Handles.DrawWireArc(olho, Vector3.up, borda, v.anguloDeVisao, alcance);
        Handles.DrawLine(olho, olho + borda * alcance);
        Handles.DrawLine(olho, olho + Quaternion.AngleAxis(meio, Vector3.up) * frente * alcance);

        // Projeção no chão, para ler melhor em perspectiva
        Handles.color = ComAlfa(cor, 0.25f);
        Handles.DrawWireArc(pe, Vector3.up, borda, v.anguloDeVisao, alcance);
        Handles.DrawDottedLine(pe, pe + borda * alcance, 4f);
        Handles.DrawDottedLine(pe, pe + Quaternion.AngleAxis(meio, Vector3.up) * frente * alcance, 4f);

        // Distância em que percebe na hora, mesmo fora do cone
        Handles.color = ComAlfa(corPerseguindo, 0.6f);
        Handles.DrawWireDisc(pe, Vector3.up, v.distanciaInstantanea);
    }

    static void DesenharLinhaAtePlayer(VampireScript v, Vector3 olho)
    {
        if (v.playerTransform == null || v.EstadoAtual >= VampireScript.VampireStates.Ragdoll) return;

        var cc = v.playerTransform.GetComponent<CharacterController>();
        Vector3 player = cc != null ? v.playerTransform.TransformPoint(cc.center) : v.playerTransform.position;
        if (Vector3.Distance(olho, player) > v.alcanceDaVisao) return;

        if (v.VendoPlayer)
        {
            Handles.color = corPerseguindo;
            Handles.DrawAAPolyLine(4f, olho, player);
        }
        else
        {
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawDottedLine(olho, player, 3f);
        }
    }

    static void DesenharAlvos(VampireScript v, Vector3 pe)
    {
        switch (v.EstadoAtual)
        {
            case VampireScript.VampireStates.Suspeito:
            case VampireScript.VampireStates.Perseguindo:
                Handles.color = corPerseguindo;
                Handles.DrawWireDisc(v.UltimaPosicaoConhecida, Vector3.up, 0.4f);
                Handles.DrawDottedLine(pe, v.UltimaPosicaoConhecida, 3f);
                Handles.Label(v.UltimaPosicaoConhecida + Vector3.up * 0.5f, "última posição vista", Estilo());
                break;

            case VampireScript.VampireStates.Investigando:
                Handles.color = corInvestigando;
                Handles.DrawWireDisc(v.PontoInvestigar, Vector3.up, 0.6f);
                Handles.DrawWireDisc(v.PontoInvestigar, Vector3.up, 0.3f);
                Handles.DrawDottedLine(pe, v.PontoInvestigar, 3f);
                Handles.Label(v.PontoInvestigar + Vector3.up * 0.5f, "investigar", Estilo());
                break;
        }
    }

    static void DesenharCaminho(VampireScript v)
    {
        var agent = v.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.hasPath) return;

        Vector3[] cantos = agent.path.corners;
        for (int i = 0; i < cantos.Length; i++) cantos[i] += Vector3.up * 0.1f;
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Handles.DrawAAPolyLine(3f, cantos);
    }

    static void DesenharPatrulha(VampireScript v, bool jogando)
    {
        var pontos = v.pontosDePatrulha;
        if (pontos != null && pontos.Length > 0)
        {
            Handles.color = ComAlfa(corPatrulha, 0.8f);
            for (int i = 0; i < pontos.Length; i++)
            {
                if (pontos[i] == null) continue;
                Vector3 a = pontos[i].position;
                Transform proximo = pontos[(i + 1) % pontos.Length];
                if (proximo != null && pontos.Length > 1) Handles.DrawDottedLine(a, proximo.position, 5f);
                Handles.DrawWireDisc(a, Vector3.up, 0.35f);
                Handles.Label(a + Vector3.up * 0.4f, "P" + (i + 1), Estilo());
            }
            return;
        }

        // Sem pontos de patrulha: mostra o posto e para onde ele fica olhando
        Vector3 posto = jogando ? v.PosicaoDoPosto : v.transform.position;
        Vector3 direcao = (jogando ? v.RotacaoDoPosto : v.transform.rotation) * Vector3.forward;
        Handles.color = ComAlfa(corPatrulha, 0.8f);
        Handles.DrawWireDisc(posto + Vector3.up * 0.05f, Vector3.up, 0.5f);
        Handles.ArrowHandleCap(0, posto + Vector3.up * 0.05f, Quaternion.LookRotation(direcao), 1.2f, EventType.Repaint);
        if (jogando) Handles.Label(posto + Vector3.up * 0.3f, "posto", Estilo());
    }

    static void DesenharRotulo(VampireScript v, Vector3 olho, Color cor, bool jogando)
    {
        Vector3 posicao = olho + Vector3.up * 1f;
        string texto = jogando ? "<b>" + v.EstadoAtual + "</b>" : "<b>" + v.name + "</b>";
        if (jogando && v.EstadoAtual < VampireScript.VampireStates.Ragdoll)
            texto += "\ndetecção " + Mathf.RoundToInt(v.Deteccao * 100f) + "%";
        Handles.Label(posicao, texto, Estilo());

        if (!jogando || v.EstadoAtual >= VampireScript.VampireStates.Ragdoll) return;

        // Barra do medidor de detecção
        Camera cam = Camera.current;
        if (cam == null || cam.WorldToViewportPoint(posicao).z < 0f) return;
        Handles.BeginGUI();
        Vector2 tela = HandleUtility.WorldToGUIPoint(posicao);
        Rect fundo = new Rect(tela.x - 30f, tela.y - 12f, 60f, 6f);
        EditorGUI.DrawRect(fundo, new Color(0f, 0f, 0f, 0.7f));
        EditorGUI.DrawRect(new Rect(fundo.x + 1f, fundo.y + 1f, (fundo.width - 2f) * v.Deteccao, fundo.height - 2f), cor);
        Handles.EndGUI();
    }

    static void DesenharBarulhos()
    {
        foreach (var barulho in Barulho.Recentes)
        {
            float idade = Time.time - barulho.tempo;
            if (idade < 0f || idade > 2f) continue;

            // Onda que se expande até o raio do barulho e some
            float raio = barulho.raio * Mathf.Clamp01(idade / 0.4f);
            float alfa = 1f - idade / 2f;
            Handles.color = ComAlfa(corBarulho, alfa * 0.08f);
            Handles.DrawSolidDisc(barulho.posicao, Vector3.up, raio);
            Handles.color = ComAlfa(corBarulho, alfa);
            Handles.DrawWireDisc(barulho.posicao, Vector3.up, raio);
            Handles.Label(barulho.posicao + Vector3.up * 0.5f, "barulho " + barulho.raio.ToString("0") + "m", Estilo());
        }
    }

    static GUIStyle Estilo()
    {
        if (estiloTexto == null)
        {
            var fundo = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            fundo.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            fundo.Apply();
            estiloTexto = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                fontSize = 11,
                padding = new RectOffset(4, 4, 2, 2),
            };
            estiloTexto.normal.textColor = Color.white;
            estiloTexto.normal.background = fundo;
        }
        return estiloTexto;
    }
}
