using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class UnclaimedDesempenhoBuild
{
    [MenuItem("Tools/Unclaimed/Desempenho/Build de medicao (Development)")]
    public static void BuildDevelopment() => Build(true);

    [MenuItem("Tools/Unclaimed/Desempenho/Build de medicao (Release)")]
    public static void BuildRelease() => Build(false);

    public static string Build(bool development)
    {
        // Pass an explicit scene list without changing EditorBuildSettings.
        PlayerSettings.enableFrameTimingStats = true;
        string raiz = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string pasta = Path.Combine(raiz, "Builds", "Medicao", development ? "dev" : "rel");
        string caminho = Path.Combine(pasta, "Unclaimed.x86_64");
        try
        {
            Directory.CreateDirectory(pasta);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/PrototipoNecroterio.unity" },
                locationPathName = caminho,
                target = BuildTarget.StandaloneLinux64,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report == null) throw new InvalidOperationException("BuildPipeline returned no BuildReport.");
            BuildSummary resumo = report.summary;
            var texto = new StringBuilder();
            texto.Append("Build de medição ").Append(development ? "Development" : "Release")
                .Append(": ").Append(resumo.result).Append(" | ").Append(caminho)
                .Append(" | tamanho: ").Append(resumo.totalSize).Append(" bytes")
                .Append(" | duração: ").Append(resumo.totalTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" s | erros: ").Append(resumo.totalErrors);
            // Include actionable errors from the report, not only their count.
            foreach (BuildStep passo in report.steps)
                foreach (BuildStepMessage mensagem in passo.messages)
                    if (mensagem.type == LogType.Error || mensagem.type == LogType.Exception || mensagem.type == LogType.Assert)
                        texto.AppendLine().Append(passo.name).Append(": ").Append(mensagem.content);
            string resultado = texto.ToString();
            if (resumo.result == BuildResult.Succeeded) Debug.Log(resultado);
            else Debug.LogError(resultado);
            return resultado;
        }
        catch (Exception erro)
        {
            string resultado = "Build de medição falhou | " + caminho + " | " + erro;
            Debug.LogError(resultado);
            return resultado;
        }
    }
}
