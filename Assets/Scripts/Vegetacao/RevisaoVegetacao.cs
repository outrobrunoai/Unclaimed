// Editor-only Play Mode tooling. Rendering measurements include a synchronous one-pixel GPU readback.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class RevisaoVegetacao : MonoBehaviour
{
    public string pasta = "CapturasVegetacao";
    public int largura = 1280, altura = 720, fps = 30;
    [TextArea] public string resultado = "";
    public bool terminou;

    Transform player, cam;
    Camera cameraAlvo;
    Movimento movimento;
    CharacterController controller;
    bool movimentoAtivo, controllerAtivo, ocupada;
    Vector3 posicaoPlayer, posicaoCamera;
    Quaternion rotacaoPlayer, rotacaoCamera;
    int capturaAnterior;
    RenderTexture rt;
    Texture2D tex;

    void Prepare(bool gravar)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("A revisão só funciona em Play Mode.");
        if (ocupada) throw new InvalidOperationException("Já existe uma revisão em andamento.");
        if (largura <= 0 || altura <= 0 || (gravar && fps <= 0)) throw new ArgumentException("Resolução e fps devem ser positivos.");
        var p = GameObject.Find("Player");
        if (!p || !p.GetComponentInChildren<Camera>()) throw new InvalidOperationException("Player com câmera não encontrado.");
        player = p.transform;
        cameraAlvo = p.GetComponentInChildren<Camera>();
        cam = cameraAlvo.transform;
        movimento = p.GetComponent<Movimento>();
        controller = p.GetComponent<CharacterController>();
        movimentoAtivo = movimento && movimento.enabled;
        controllerAtivo = controller && controller.enabled;
        posicaoPlayer = player.position; rotacaoPlayer = player.rotation;
        posicaoCamera = cam.localPosition; rotacaoCamera = cam.localRotation;
        capturaAnterior = Time.captureFramerate;
        ocupada = true;
        terminou = false; resultado = "";
        if (movimento) movimento.enabled = false;
        if (controller) controller.enabled = false;
    }

    void Restaurar()
    {
        if (!ocupada) return;
        Time.captureFramerate = capturaAnterior;
        if (player) player.SetPositionAndRotation(posicaoPlayer, rotacaoPlayer);
        if (cam) { cam.localPosition = posicaoCamera; cam.localRotation = rotacaoCamera; }
        if (controller) controller.enabled = controllerAtivo;
        if (movimento) movimento.enabled = movimentoAtivo;
        if (rt) { rt.Release(); Destroy(rt); rt = null; }
        if (tex) { Destroy(tex); tex = null; }
        ocupada = false;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        Restaurar();
    }

    static void Validar(float[] valores, int passo, int minimo)
    {
        if (valores == null || valores.Length < passo * minimo || valores.Length % passo != 0)
            throw new ArgumentException("Quantidade de valores inválida para o roteiro.");
        foreach (float v in valores)
            if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException("O roteiro deve conter valores finitos.");
    }

    void DefinirPose(float x, float z, float yaw, float pitch, float olho = 0)
    {
        player.SetPositionAndRotation(new Vector3(x, posicaoPlayer.y, z), Quaternion.Euler(0, yaw, 0));
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);
        // Nonpositive eye height means the original Player eye height. Positive values are metres above Player.
        cam.localPosition = posicaoCamera;
        if (olho > 0)
        {
            Vector3 world = cam.position;
            world.y = player.position.y + olho;
            cam.position = world;
        }
    }

    void LerQuadro(int w, int h)
    {
        var targetAnterior = cameraAlvo.targetTexture;
        var activeAnterior = RenderTexture.active;
        try
        {
            cameraAlvo.targetTexture = rt;
            cameraAlvo.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
        }
        finally
        {
            cameraAlvo.targetTexture = targetAnterior;
            RenderTexture.active = activeAnterior;
        }
    }

    // Rows: id, x, z, yaw, pitch. Two measurements per pose, without changing rain or scene state.
    public IEnumerator Medir(float[] poses, int quadros)
    {
        Validar(poses, 5, 1);
        if (quadros <= 0) throw new ArgumentException("Número de quadros deve ser positivo.");
        Prepare(false);
        try
        {
            rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
            tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var log = new StringBuilder();
            log.AppendLine($"{largura}x{altura}, {quadros} quadros/medida, cena atual, Editor Play Mode, {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}");
            log.AppendLine("Render inclui câmera adicional + leitura GPU; quadro inclui o custo da própria medição.");
            for (int p = 0; p < poses.Length; p += 5)
            {
                DefinirPose(poses[p + 1], poses[p + 2], poses[p + 3], poses[p + 4]);
                for (int repeticao = 0; repeticao < 2; repeticao++)
                {
                    yield return new WaitForSecondsRealtime(1.2f);
                    double render = 0, frame = 0;
                    for (int f = 0; f < quadros; f++)
                    {
                        yield return new WaitForEndOfFrame();
                        frame += Time.unscaledDeltaTime * 1000;
                        var sw = Stopwatch.StartNew();
                        LerQuadro(1, 1);
                        render += sw.Elapsed.TotalMilliseconds;
                    }
                    log.AppendLine($"pose {poses[p]:0}, repetição {repeticao + 1}: render {render / quadros:F2} ms, quadro {frame / quadros:F2} ms");
                }
            }
            resultado = log.ToString();
            terminou = true;
        }
        finally { Restaurar(); }
    }

    // Rows: t(seconds), x, z, yaw, pitch, eye height (<= 0 uses Player's original height).
    // Strictly increasing times, starting at zero; intermediate poses use smooth interpolation.
    public IEnumerator Gravar(float[] percurso)
    {
        Validar(percurso, 6, 2);
        if (percurso[0] != 0) throw new ArgumentException("O percurso deve começar em t=0.");
        for (int i = 6; i < percurso.Length; i += 6)
            if (percurso[i] <= percurso[i - 6]) throw new ArgumentException("Tempos do percurso devem ser crescentes.");
        if (string.IsNullOrWhiteSpace(pasta)) throw new ArgumentException("Informe a pasta de captura.");
        Prepare(true);
        try
        {
            Directory.CreateDirectory(pasta);
            rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
            tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
            float olhoOriginal = cameraAlvo.transform.position.y - player.position.y;
            DefinirPose(percurso[1], percurso[2], percurso[3], percurso[4], percurso[5]);
            yield return new WaitForSecondsRealtime(1.5f);
            Time.captureFramerate = fps;
            int frames = Mathf.CeilToInt(percurso[percurso.Length - 6] * fps), segment = 0;
            for (int f = 0; f <= frames; f++)
            {
                float t = Mathf.Min(f / (float)fps, percurso[percurso.Length - 6]);
                while (segment < percurso.Length - 12 && t > percurso[segment + 6]) segment += 6;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(percurso[segment], percurso[segment + 6], t));
                float olhoA = percurso[segment + 5] > 0 ? percurso[segment + 5] : olhoOriginal;
                float olhoB = percurso[segment + 11] > 0 ? percurso[segment + 11] : olhoOriginal;
                DefinirPose(Mathf.Lerp(percurso[segment + 1], percurso[segment + 7], k),
                    Mathf.Lerp(percurso[segment + 2], percurso[segment + 8], k),
                    Mathf.LerpAngle(percurso[segment + 3], percurso[segment + 9], k),
                    Mathf.Lerp(percurso[segment + 4], percurso[segment + 10], k), Mathf.Lerp(olhoA, olhoB, k));
                yield return new WaitForEndOfFrame();
                LerQuadro(largura, altura);
                File.WriteAllBytes(Path.Combine(pasta, $"f{f:00000}.jpg"), tex.EncodeToJPG(92));
            }
            resultado = $"{frames + 1} quadros JPG, {largura}x{altura}, {fps} fps, pasta {Path.GetFullPath(pasta)}";
            terminou = true;
        }
        finally { Restaurar(); }
    }
}
#endif
