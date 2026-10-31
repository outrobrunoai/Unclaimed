using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
public static class PatioReview
{
    const string Dir="Blender/PatioRecebimento/revisao";
    public static object Before()=>Capture("antes");
    public static object After()=>Capture("depois");
    public static object Audit()
    {
        var scene=EditorSceneManager.GetActiveScene();
        var ts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var data=new{scene=scene.path,scene.isDirty,lightmaps=LightmapSettings.lightmaps.Length,
            lights=ts.SelectMany(t=>t.GetComponents<Light>()).Select(l=>new{l.name,pos=l.transform.position.ToString(),l.intensity,l.range,l.enabled}).ToArray(),
            gameplay=ts.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c!=null).Select(c=>new{path=PathOf(c.transform),type=c.GetType().FullName,serialized=EditorJsonUtility.ToJson(c)}).ToArray(),
            old=ts.Where(t=>t.name=="Cacamba"||t.name=="Paletes"||t.name=="Armarios de servico").Select(t=>new{t.name,pos=t.position.ToString()}).ToArray()};
        Directory.CreateDirectory(Dir);
        string phase=GameObject.Find("Unclaimed - Necroterio (planta)/Area externa/Patio do recebimento")==null?"antes":"depois";
        File.WriteAllText(Dir+"/audit-"+phase+".json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
        return new{scene=scene.path,lights=data.lights.Length,gameplay=data.gameplay.Length};
    }
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    public static object Verify()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        var root=GameObject.Find("Unclaimed - Necroterio (planta)/Area externa/Patio do recebimento");
        if(root==null)throw new InvalidOperationException("Patio missing");Physics.SyncTransforms();
        var routes=new[]{
            ("portao-patio",new Vector3(18,0,5.9f),new Vector3(18,0,12.1f)),
            ("ligacao-recebimento",new Vector3(11.9f,0,8.375f),new Vector3(15.5f,0,8.375f)),
            ("circulacao-norte",new Vector3(13.8f,0,12.1f),new Vector3(19,0,12.1f)),
            ("recuo",new Vector3(10.2f,0,12.1f),new Vector3(13.8f,0,12.1f)),
            ("frente-portas",new Vector3(14.6f,0,6.2f),new Vector3(14.6f,0,11.5f)),
            ("agenda-coleta",new Vector3(14.6f,0,10.15f),new Vector3(13.6f,0,10.15f))};
        var checks=routes.Select(route=>{
            int n=Mathf.CeilToInt(Vector3.Distance(route.Item2,route.Item3)/.2f);var blocked=new List<string>();
            for(int i=0;i<=n;i++){
                Vector3 p=Vector3.Lerp(route.Item2,route.Item3,i/(float)n);
                foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.42f,p+Vector3.up*1.42f,.35f,~0,QueryTriggerInteraction.Ignore))
                    if(c.transform.root.name!="Player")blocked.Add(PathOf(c.transform));
            }return new{route=route.Item1,samples=n+1,blockedBy=blocked.Distinct().ToArray()};
        }).ToArray();
        var blockers=Physics.OverlapBox(new Vector3(17.8f,.9f,9.4f),new Vector3(1.15f,.8f,2.5f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
            .Where(c=>c.transform.root.name!="Player").Select(c=>PathOf(c.transform)).ToArray();
        var map=AssetDatabase.LoadAssetAtPath<MapaAbrigoChuva>("Assets/UnclaimedPrototype/Clima/MapaAbrigoChuva.asset");
        Vector3 covered=new Vector3(14,1.65f,8.375f),open=new Vector3(15.3f,1.65f,8.375f);map.Amostrar(covered,out float roof,out _);
        var missing=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh==null).Select(f=>f.name).ToArray();
        var bad=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name.Contains("InternalError"))).Select(r=>r.name).ToArray();
        var data=new{routes=checks,vehicleEnvelope=new{width=2.3f,length=5f,blockedBy=blockers},rain=new{underCanopy=map.Exposicao(covered),outdoors=map.Exposicao(open),roofHeight=roof,exteriorDepthUnderCanopy=map.ProfundidadeInterna(covered)},
            missingMeshes=missing,badMaterials=bad,instances=root.GetComponentsInChildren<Transform>().Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)),lightmaps=LightmapSettings.lightmaps.Length};
        File.WriteAllText(Dir+"/verificacao.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));return data;
    }
    static object Capture(string phase)
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        Directory.CreateDirectory(Dir+"/"+phase);
        var source=GameObject.Find("Player/Camera").GetComponent<Camera>();
        var go=new GameObject("Temporary patio review camera");var c=go.AddComponent<Camera>();c.CopyFrom(source);c.enabled=false;c.useOcclusionCulling=false;
        var ex=c.GetUniversalAdditionalCameraData();var se=source.GetUniversalAdditionalCameraData();ex.renderPostProcessing=se.renderPostProcessing;ex.antialiasing=se.antialiasing;ex.renderShadows=se.renderShadows;
        var views=new[]{
            ("acesso",new Vector3(18.0f,1.65f,6.2f),new Vector3(13.6f,1.2f,9f)),
            ("porta",new Vector3(15.7f,1.65f,9.6f),new Vector3(12.8f,1.4f,8.375f)),
            ("apoio",new Vector3(15.7f,1.65f,11.8f),new Vector3(19.4f,1f,14.3f)),
            ("saida",new Vector3(13.4f,1.65f,8.375f),new Vector3(18.4f,1.1f,11.6f)),
            ("armario",new Vector3(18.6f,1.65f,9.0f),new Vector3(20.55f,.75f,10.6f)),
            ("paleteira",new Vector3(14.6f,1.45f,12.8f),new Vector3(16.1f,.55f,14.30f)),
            ("cobertura-inspecao",new Vector3(16.0f,3.6f,6.0f),new Vector3(13.6f,2.7f,8.375f))};
        var rt=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        try{c.targetTexture=rt;c.aspect=4f/3;c.fieldOfView=60;
            foreach(var v in views){c.transform.SetPositionAndRotation(v.Item2,Quaternion.LookRotation(v.Item3-v.Item2));c.Render();c.Render();RenderTexture.active=rt;
                var im=new Texture2D(1440,1080,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,1440,1080),0,0);im.Apply();File.WriteAllBytes(Dir+"/"+phase+"/"+v.Item1+".png",im.EncodeToPNG());UnityEngine.Object.DestroyImmediate(im);}
        }finally{RenderTexture.active=previous;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        return new{phase,images=views.Length};
    }
}
