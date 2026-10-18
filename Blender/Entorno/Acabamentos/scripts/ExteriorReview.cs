using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class ExteriorReview
{
    static float[] V(Vector3 v) => new [] { v.x,v.y,v.z };
    const string Folder = "Blender/Entorno/Acabamentos/revisao";
    public static object Audit()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var data = new {
            scene = scene.path, dirty = scene.isDirty, playing = Application.isPlaying,
            lightmaps = LightmapSettings.lightmaps.Length,
            lights = transforms.Select(t => t.GetComponent<Light>()).Where(l => l != null).Select(l => new {
                path = PathOf(l.transform), pos = V(l.transform.position), rot = new [] { l.transform.rotation.x,l.transform.rotation.y,l.transform.rotation.z,l.transform.rotation.w },
                intensity = l.intensity, range = l.range, color = new [] { l.color.r,l.color.g,l.color.b,l.color.a }, type = l.type.ToString(), enabled = l.enabled
            }).ToArray(),
            colliders = transforms.SelectMany(t => t.GetComponents<Collider>()).Select(c => new {
                path = PathOf(c.transform), type = c.GetType().Name, enabled = c.enabled, trigger = c.isTrigger,
                center = V(c.bounds.center), size = V(c.bounds.size)
            }).ToArray(),
            targets = transforms.Where(t => t.name.Contains("Espada") || t.name.Contains("Placa -") ||
                t.name == "Instalacoes da fachada" || t.name == "Condutores pluviais" || t.name == "Gradis e portoes")
                .Select(t => new { path = PathOf(t), pos = V(t.position), rotation = V(t.eulerAngles), scale = V(t.localScale),
                    gi = GameObjectUtility.GetStaticEditorFlags(t.gameObject).ToString(),
                    boundsSize = t.GetComponent<Renderer>() != null ? V(t.GetComponent<Renderer>().bounds.size) : new float[3],
                    materials = t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>().sharedMaterials.Select(m => m == null ? "null" : AssetDatabase.GetAssetPath(m)).ToArray() : new string[0]
                }).ToArray()
        };
        Directory.CreateDirectory(Folder);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented,
            new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
        File.WriteAllText(Folder + "/audit-" + (File.Exists(Folder + "/audit-antes.json") ? "depois" : "antes") + ".json", json);
        return new { scene = scene.path, dirty = scene.isDirty, lightmaps = LightmapSettings.lightmaps.Length,
            lights = data.lights.Length, colliders = data.colliders.Length, targets = data.targets };
    }
    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    public static object Before() => Capture("antes");
    public static object After() => Capture("depois");
    public static object Verify()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/PrototipoNecroterio.unity")throw new InvalidOperationException("Edit Mode only.");
        Physics.SyncTransforms();
        var routes=new [] {
            ("entrada",new Vector3(-11.4f,0,-18.3f),new Vector3(-11.4f,0,-12.7f)),
            ("lateral-oeste",new Vector3(-16.6f,0,-17.3f),new Vector3(-16.6f,0,-8.8f)),
            ("fachada-arquivo",new Vector3(-9.1f,0,-13.75f),new Vector3(3.0f,0,-13.75f)),
            ("nicho-arquivo",new Vector3(4.8f,0,-13f),new Vector3(4.8f,0,-4.4f))
        };
        var checks=routes.Select(route=>{
            var blocked=new System.Collections.Generic.List<string>();
            int count=Mathf.CeilToInt(Vector3.Distance(route.Item2,route.Item3)/.25f);
            for(int i=0;i<=count;i++){
                var p=Vector3.Lerp(route.Item2,route.Item3,i/(float)count);
                foreach(var collider in Physics.OverlapCapsule(p+Vector3.up*.42f,p+Vector3.up*1.42f,.35f,~0,QueryTriggerInteraction.Ignore))
                    if(collider.transform.root.name!="Player")blocked.Add(PathOf(collider.transform));
            }
            return new { route=route.Item1, samples=count+1, blockedBy=blocked.Distinct().ToArray() };
        }).ToArray();
        var barriers=new [] {
            ("frontal",new Vector3(-11.4f,1,-18.4f),Vector3.back),
            ("lateral",new Vector3(-16.6f,1,-8.8f),Vector3.forward),
            ("leste",new Vector3(6.2f,1,-15.1f),Vector3.right),
            ("patio",new Vector3(18f,1,4.5f),Vector3.forward)
        }.Select(g=>new { gate=g.Item1, hits=Physics.SphereCastAll(g.Item2,.35f,g.Item3,1.5f,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>h.collider.transform.root.name!="Player").Select(h=>PathOf(h.collider.transform)).Distinct().ToArray() }).ToArray();
        var outside=GameObject.Find("Unclaimed - Necroterio (planta)/Area externa");
        var missing=outside.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh==null).Select(f=>PathOf(f.transform)).ToArray();
        var badMaterials=outside.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m==null || m.shader==null || m.shader.name.Contains("InternalError"))).Select(r=>PathOf(r.transform)).ToArray();
        var installed=outside.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith("Assets/Modelos/Entorno/Acabamentos/"))
            .Select(t=>new {name=t.name,path=PathOf(t),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),position=V(t.position)}).ToArray();
        var result=new { routes=checks, gates=barriers, missingMeshes=missing,badMaterials,installed,lightmaps=LightmapSettings.lightmaps.Length };
        File.WriteAllText(Folder+"/verificacao.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    static object Capture(string phase)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Capture only outside Play Mode.");
        var source = GameObject.Find("Player/Camera").GetComponent<Camera>();
        var points = new[] {
            ("entrada-player", new Vector3(-11.4f,1.65f,-16.1f),new Vector3(-11.375f,1.65f,-12.475f)),
            ("letreiro-perto",new Vector3(-11.375f,2.1f,-14.1f),new Vector3(-11.375f,3.08f,-12.5f)),
            ("interfone-perto",new Vector3(-9.7f,1.55f,-13.45f),new Vector3(-9.7f,1.3f,-12.5f)),
            ("canteiro-perto",new Vector3(-13.85f,1.35f,-13.9f),new Vector3(-13.9f,.85f,-12.78f)),
            ("pluvial-player",new Vector3(-14.15f,1.65f,-13.8f),new Vector3(-14.6f,1.85f,-12.5f)),
            ("condensadoras-player",new Vector3(5.85f,1.65f,-10.45f),new Vector3(3.9f,2.0f,-8.55f)),
            ("condensadora-perto",new Vector3(5.0f,2.05f,-9.6f),new Vector3(3.9f,2.05f,-9.2f)),
            ("agua-perto",new Vector3(4.8f,1.2f,-10.7f),new Vector3(3.7f,.8f,-10.0f)),
            ("portao-lateral-player",new Vector3(-16.6f,1.65f,-9.8f),new Vector3(-16.6f,1.1f,-8f)),
            ("cadeado-lateral-perto",new Vector3(-16.6f,1.18f,-8.48f),new Vector3(-16.6f,1.08f,-8.05f)),
            ("portao-leste-player",new Vector3(5.6f,1.65f,-15.1f),new Vector3(7f,1.05f,-14.62f)),
            ("cadeado-leste-perto",new Vector3(6.55f,1.16f,-14.65f),new Vector3(6.95f,1.05f,-14.62f)),
            ("pluvial-cobertura-inspecao",new Vector3(-14.1f,3.55f,-13.6f),new Vector3(-14.6f,3.25f,-12.475f)),
            ("filodendros-player",new Vector3(-4.4f,1.35f,-12.8f),new Vector3(-4.4f,.4f,-11.6f)),
            ("numero-perto",new Vector3(-12.55f,1.65f,-19.9f),new Vector3(-12.55f,1.55f,-19.15f)),
            ("nao-estacione-perto",new Vector3(20.1f,.7f,-20.0f),new Vector3(20.1f,.33f,-19.1f)),
            ("portaria-perto",new Vector3(14.6f,2.3f,-17.6f),new Vector3(13.4f,2.3f,-17.6f))
        };
        Directory.CreateDirectory(Folder + "/" + phase);
        var go = new GameObject("Temporary exterior review camera");
        var camera = go.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false;
        var extra = camera.GetUniversalAdditionalCameraData();
        var originalExtra = source.GetUniversalAdditionalCameraData();
        extra.renderPostProcessing = originalExtra.renderPostProcessing;
        extra.antialiasing = originalExtra.antialiasing;
        extra.renderShadows = originalExtra.renderShadows;
        var rt = new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        try {
            camera.targetTexture = rt;camera.aspect = 4f/3f;
            foreach (var p in points) {
                camera.transform.SetPositionAndRotation(p.Item2,Quaternion.LookRotation(p.Item3-p.Item2));
                camera.Render();camera.Render(); RenderTexture.active=rt;
                var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();
                File.WriteAllBytes(Folder+"/"+phase+"/"+p.Item1+".png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
        } finally {
            RenderTexture.active=old;camera.targetTexture=null;rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
        return new { phase, images=points.Length, fov=source.fieldOfView, sourcePost=originalExtra.renderPostProcessing };
    }
}
