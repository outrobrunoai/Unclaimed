// Review tooling for the light rain (Play Mode only): records a fixed walk from the Player camera
// and measures rendering cost with the rain group on/off under identical conditions. Editor-only.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Profiling;

public class RevisaoChuva : MonoBehaviour
{
    // time (s), x, z, yaw, pitch — a walk through the checks asked for the rain review.
    static readonly float[,] Walk =
    {
        { 0, -11.4f, -8.8f, 180, 2 },     // reception, looking out through the glass door
        { 3, -11.4f, -10.4f, 180, 2 },
        { 6, -11.4f, -12.5f, 180, 4 },    // threshold: leaving the covered area
        { 8.5f, -11.3f, -14.4f, 200, 6 },
        { 11, -11.0f, -15.4f, 232, 4 },   // garden post
        { 14, -10.9f, -15.9f, 192, 40 },  // ground and puddle up close
        { 16.5f, -10.4f, -16.6f, 150, 12 },
        { 20, -7.0f, -17.6f, 70, 4 },     // front and side of the building
        { 23, -6.8f, -17.4f, -25, 3 },
        { 26.5f, -10.8f, -14.6f, -8, 3 },
        { 29, -11.4f, -12.5f, 0, 2 },     // entering the covered area
        { 32, -11.4f, -10.2f, 25, 0 },    // inside: no rain or splashes
        { 35, -11.4f, -9.2f, 175, 3 },    // look back outside from inside
    };

    public string pasta;
    public int largura = 1280, altura = 720, fps = 30;
    public string resultado = "";
    public bool terminou;

    Transform player, cam;
    CharacterController controller;
    Movimento movimento;

    void Prepare()
    {
        var p = GameObject.Find("Player");
        player = p.transform;
        movimento = p.GetComponent<Movimento>();
        controller = p.GetComponent<CharacterController>();
        cam = p.GetComponentInChildren<Camera>().transform;
        if (movimento) movimento.enabled = false;
        if (controller) controller.enabled = false;
        var rain = FindAnyObjectByType<ChuvaLeve>();
        if (rain) rain.alvo = null;
        var entrance = GameObject.Find("Unclaimed - Necroterio (planta)/Portas/Entrada principal");
        if (entrance) foreach (var door in entrance.GetComponentsInChildren<FolhaDePorta>()) door.Abrir();
    }

    void Pose(float t)
    {
        int n = Walk.GetLength(0), i = 0;
        while (i < n - 2 && t > Walk[i + 1, 0]) i++;
        float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Walk[i, 0], Walk[i + 1, 0], t));
        float x = Mathf.Lerp(Walk[i, 1], Walk[i + 1, 1], k), z = Mathf.Lerp(Walk[i, 2], Walk[i + 1, 2], k);
        float yaw = Mathf.LerpAngle(Walk[i, 3], Walk[i + 1, 3], k), pitch = Mathf.Lerp(Walk[i, 4], Walk[i + 1, 4], k);
        player.SetPositionAndRotation(new Vector3(x, player.position.y, z), Quaternion.Euler(0, yaw, 0));
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    public IEnumerator Gravar()
    {
        Prepare();
        Directory.CreateDirectory(pasta);
        float duration = Walk[Walk.GetLength(0) - 1, 0];
        var camera = cam.GetComponent<Camera>();
        var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
        Pose(0);
        yield return new WaitForSeconds(1.5f); // let the rain fill the volume around the start
        Time.captureFramerate = fps;
        int frames = Mathf.CeilToInt(duration * fps);
        for (int f = 0; f <= frames; f++)
        {
            Pose(f / (float)fps);
            yield return new WaitForEndOfFrame();
            var old = camera.targetTexture;
            camera.targetTexture = rt; camera.Render(); camera.targetTexture = old;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, largura, altura), 0, 0); RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(pasta, $"f{f:0000}.jpg"), tex.EncodeToJPG(92));
        }
        Time.captureFramerate = 0;
        rt.Release(); Destroy(rt); Destroy(tex);
        resultado = $"{frames + 1} frames";
        terminou = true;
    }

    public IEnumerator GravarComAudio(float[] roteiro)
    {
        terminou = false;
        resultado = "";
        if (roteiro == null || roteiro.Length % 5 != 0 || fps <= 0)
            throw new ArgumentException("Roteiro deve conter passos de cinco valores e fps deve ser positivo.");
        for (int s = 0; s < roteiro.Length; s += 5)
        {
            if (roteiro[s] < 0 || roteiro[s] > 6 || roteiro[s] != Mathf.Floor(roteiro[s]) ||
                float.IsNaN(roteiro[s]) || float.IsNaN(roteiro[s + 4]) ||
                float.IsInfinity(roteiro[s + 4]) || roteiro[s + 4] < 0)
                throw new ArgumentException("Tipo ou duração inválida no roteiro.");
        }
        Prepare();
        cam.localRotation = Quaternion.identity;
        Directory.CreateDirectory(pasta);
        yield return new WaitForSeconds(1.5f);
        int canais = CanaisAudio(), taxa = AudioSettings.outputSampleRate, quadros = 0;
        var camera = cam.GetComponent<Camera>();
        var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
        var audio = new List<float[]>();
        var log = new StringBuilder();
        long amostras = 0;
        bool capturando = false;
        try
        {
            Time.captureFramerate = fps;
            capturando = AudioRenderer.Start();
            if (!capturando) throw new InvalidOperationException("Não foi possível iniciar a captura do áudio do motor.");
            for (int s = 0; s < roteiro.Length; s += 5)
            {
                int tipo = (int)roteiro[s];
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F6}s: tipo {1}, x {2}, z {3}, yaw {4}, segundos {5}",
                    quadros / (double)fps, tipo, roteiro[s + 1], roteiro[s + 2], roteiro[s + 3], roteiro[s + 4]));
                Vector3 inicio = player.position;
                float yaw = player.eulerAngles.y;
                Vector3 destino = new Vector3(roteiro[s + 1], inicio.y, roteiro[s + 2]);
                if (tipo >= 2 && tipo <= 6)
                {
                    string caminho = "Unclaimed - Necroterio (planta)/Portas/" + (tipo == 4 ? "Porta WC" : "Entrada principal");
                    var porta = GameObject.Find(caminho);
                    if (!porta) throw new InvalidOperationException("Porta não encontrada: " + caminho);
                    // 5 = only the left leaf open, 6 = only the right one.
                    foreach (var folha in porta.GetComponentsInChildren<FolhaDePorta>(true))
                    {
                        bool esquerda = folha.name.Contains("esquerda");
                        bool abrir = tipo == 3 || tipo == 4 || (tipo == 5 && esquerda) || (tipo == 6 && !esquerda);
                        if (abrir) folha.Abrir(); else folha.Fechar();
                    }
                }
                int total = Mathf.CeilToInt(roteiro[s + 4] * fps);
                if (tipo == 0 && total == 0)
                    player.SetPositionAndRotation(destino, Quaternion.Euler(0, roteiro[s + 3], 0));
                for (int f = 0; f < total; f++)
                {
                    if (tipo == 0)
                    {
                        float k = Mathf.Clamp01((f + 1f) / (fps * roteiro[s + 4]));
                        player.SetPositionAndRotation(Vector3.Lerp(inicio, destino, k),
                            Quaternion.Euler(0, Mathf.LerpAngle(yaw, roteiro[s + 3], k), 0));
                    }
                    cam.localRotation = Quaternion.identity;
                    yield return new WaitForEndOfFrame();
                    int n = AudioRenderer.GetSampleCountForCaptureFrame();
                    if (n > 0)
                    {
                        using (var buffer = new NativeArray<float>(n * canais, Allocator.Temp))
                        {
                            if (!AudioRenderer.Render(buffer)) throw new InvalidOperationException("Falha ao renderizar áudio.");
                            audio.Add(buffer.ToArray());
                        }
                        amostras += n;
                    }
                    var oldTarget = camera.targetTexture;
                    var oldActive = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = rt;
                        camera.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
                        tex.Apply();
                    }
                    finally
                    {
                        camera.targetTexture = oldTarget;
                        RenderTexture.active = oldActive;
                    }
                    File.WriteAllBytes(Path.Combine(pasta, $"f{quadros:0000}.jpg"), tex.EncodeToJPG(92));
                    quadros++;
                }
            }
        }
        finally
        {
            if (capturando) AudioRenderer.Stop();
            Time.captureFramerate = 0;
            rt.Release(); Destroy(rt); Destroy(tex);
            File.WriteAllText(Path.Combine(pasta, "roteiro.txt"), log.ToString());
        }
        GravarWav(Path.Combine(pasta, "audio.wav"), audio, amostras, canais, taxa);
        resultado = $"{quadros} quadros, {amostras} amostras por canal, {canais} canais, {taxa} Hz";
        terminou = true;
    }

    static int CanaisAudio()
    {
        switch (AudioSettings.speakerMode)
        {
            case AudioSpeakerMode.Mono: return 1;
            case AudioSpeakerMode.Stereo:
            case AudioSpeakerMode.Prologic: return 2;
            case AudioSpeakerMode.Quad: return 4;
            case AudioSpeakerMode.Surround: return 5;
            case AudioSpeakerMode.Mode5point1: return 6;
            case AudioSpeakerMode.Mode7point1: return 8;
            default: throw new NotSupportedException("Configuração de canais não suportada para captura WAV.");
        }
    }

    static void GravarWav(string caminho, List<float[]> blocos, long amostras, int canais, int taxa)
    {
        long bytes = amostras * canais * 2;
        if (bytes > uint.MaxValue - 36) throw new IOException("Gravação excede o limite do WAV PCM.");
        using (var writer = new BinaryWriter(File.Create(caminho)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write((uint)(36 + bytes));
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)canais); writer.Write(taxa);
            writer.Write(taxa * canais * 2); writer.Write((short)(canais * 2)); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write((uint)bytes);
            foreach (var bloco in blocos)
                foreach (float valor in bloco)
                {
                    float v = float.IsNaN(valor) ? 0 : Mathf.Clamp(valor, -1, 1);
                    writer.Write((short)Mathf.RoundToInt(v * (v < 0 ? 32768 : 32767)));
                }
        }
    }

    // Audio check: walks the camera along segments (x0, z0, x1, z1, seconds; seconds < 0 = stand at x0,z0
    // with a 3 cm sway) and logs the rain volume/low-pass cutoff every 0.25 s.
    public IEnumerator TrilhaAudio(float[] segmentos)
    {
        Prepare();
        var rain = FindAnyObjectByType<ChuvaLeve>();
        var sb = new StringBuilder();
        for (int s = 0; s < segmentos.Length; s += 5)
        {
            float duration = Mathf.Abs(segmentos[s + 4]);
            bool stand = segmentos[s + 4] < 0;
            sb.AppendLine($"-- trecho {s / 5 + 1}: ({segmentos[s]}, {segmentos[s + 1]}) -> ({segmentos[s + 2]}, {segmentos[s + 3]}) {(stand ? "parado" : duration + " s")}");
            float t = 0, nextLog = 0;
            while (t <= duration)
            {
                float k = stand ? 0 : t / duration;
                float sway = stand ? Mathf.Sin(t * 5) * 0.03f : 0;
                Vector3 head = new Vector3(Mathf.Lerp(segmentos[s], segmentos[s + 2], k), 0, Mathf.Lerp(segmentos[s + 1], segmentos[s + 3], k) + sway);
                player.position = new Vector3(head.x, player.position.y, head.z) - new Vector3(cam.position.x - player.position.x, 0, cam.position.z - player.position.z);
                if (t >= nextLog)
                {
                    nextLog += 0.25f;
                    sb.AppendLine($"t {t:F2} cam ({cam.position.x:F2}, {cam.position.z:F2}) prof {rain.mapa.ProfundidadeInterna(cam.position):F2} vol {rain.audioChuva.volume:F3} corte {rain.filtroAudio.cutoffFrequency:F0}");
                }
                yield return null;
                t += Time.deltaTime;
            }
        }
        resultado = sb.ToString();
        terminou = true;
    }

    // Per-layer probe: steps of (tipo, x, z, segundos). tipo 0 = stand camera at x,z; 2 = close both entrance
    // leaves; 3 = open both; 5 = open only the left leaf; 6 = open only the right leaf. Logs every layer.
    public IEnumerator SondarCamadas(float[] passos)
    {
        Prepare();
        var rain = FindAnyObjectByType<ChuvaLeve>();
        var entrance = GameObject.Find("Unclaimed - Necroterio (planta)/Portas/Entrada principal");
        var leaves = entrance ? entrance.GetComponentsInChildren<FolhaDePorta>() : new FolhaDePorta[0];
        var sb = new StringBuilder();
        for (int s = 0; s < passos.Length; s += 4)
        {
            int tipo = (int)passos[s];
            if (tipo == 0)
            {
                Vector3 head = new Vector3(passos[s + 1], 0, passos[s + 2]);
                player.position = new Vector3(head.x, player.position.y, head.z) - new Vector3(cam.position.x - player.position.x, 0, cam.position.z - player.position.z);
            }
            foreach (var leaf in leaves)
            {
                bool left = leaf.name.Contains("esquerda");
                if (tipo == 2) leaf.Fechar();
                if (tipo == 3 || (tipo == 5 && left) || (tipo == 6 && !left)) leaf.Abrir();
                if ((tipo == 5 && !left) || (tipo == 6 && left)) leaf.Fechar();
            }
            yield return new WaitForSeconds(passos[s + 3]);
            sb.Append($"passo {s / 4 + 1} tipo {tipo} cam ({cam.position.x:F2}, {cam.position.z:F3})");
            foreach (var leaf in leaves) sb.Append($" {leaf.name} {leaf.Abertura:F0}°");
            sb.Append($" | externa {rain.audioChuva.volume:F3}");
            if (rain.audioInterno) sb.Append($" | interna {rain.audioInterno.volume:F3}");
            foreach (var portal in rain.portais)
                if (portal && portal.fonte)
                    sb.Append($" | portal {portal.fonte.volume:F3} corte {(portal.filtro ? portal.filtro.cutoffFrequency : 0):F0} fonte ({portal.fonte.transform.position.x:F2}, {portal.fonte.transform.position.z:F3})");
            sb.AppendLine();
        }
        resultado = sb.ToString();
        terminou = true;
    }

    // Same pose, same resolution: average frame time and render time with the rain group on and off.
    public IEnumerator Medir(float[] poses, int quadros)
    {
        Prepare();
        var rainRoot = FindAnyObjectByType<ChuvaLeve>().gameObject;
        var camera = cam.GetComponent<Camera>();
        var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
        string[] markers = { "ParticleSystem.Update", "ParticleSystem.ScheduleGeometryJobs", "ChuvaLeve.Update" };
        var sb = new StringBuilder();
        sb.AppendLine($"Resolucao {largura}x{altura}, {quadros} quadros por medida, Editor (Play Mode), {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}");
        for (int p = 0; p < poses.Length; p += 5)
        {
            player.SetPositionAndRotation(new Vector3(poses[p + 1], player.position.y, poses[p + 2]), Quaternion.Euler(0, poses[p + 3], 0));
            cam.localRotation = Quaternion.Euler(poses[p + 4], 0, 0);
            foreach (bool on in new[] { true, false, true, false })
            {
                rainRoot.SetActive(on);
                yield return new WaitForSeconds(1.2f);
                double render = 0, frame = 0;
                var recs = new List<Recorder>();
                foreach (var m in markers) { var r = Recorder.Get(m); if (r.isValid) { r.enabled = true; recs.Add(r); } }
                long[] sums = new long[recs.Count];
                for (int f = 0; f < quadros; f++)
                {
                    yield return new WaitForEndOfFrame();
                    frame += Time.unscaledDeltaTime * 1000;
                    for (int r = 0; r < recs.Count; r++) sums[r] += recs[r].elapsedNanoseconds;
                    var sw = Stopwatch.StartNew();
                    var old = camera.targetTexture;
                    camera.targetTexture = rt; camera.Render(); camera.targetTexture = old;
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); tex.Apply(); RenderTexture.active = null; // GPU sync
                    render += sw.Elapsed.TotalMilliseconds;
                }
                sb.Append($"pose {poses[p]:0} chuva {(on ? "ON " : "OFF")}: render {render / quadros:F2} ms, quadro {frame / quadros:F2} ms");
                for (int r = 0; r < recs.Count; r++) sb.Append($", {recs[r].sampleBlockCount}x {markers[r]} {sums[r] / 1e6 / quadros:F3} ms");
                if (on) { var c = rainRoot.GetComponent<ChuvaLeve>(); sb.Append($", particulas {c.gotas.particleCount}+{c.particulasRespingos.particleCount}+{c.particulasOndulacoes.particleCount}"); }
                sb.AppendLine();
                foreach (var r in recs) r.enabled = false;
            }
        }
        rainRoot.SetActive(true);
        rt.Release(); Destroy(rt); Destroy(tex);
        resultado = sb.ToString();
        terminou = true;
    }
}
#endif
