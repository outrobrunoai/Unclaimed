using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Inspection only: temporary camera is removed in finally; no lighting or scene edits.
public static class LetreiroReview
{
    const string Folder="docs/direcao-arte/integracao-a";
    const string Sign="Unclaimed - Necroterio (planta)/Area externa/Fachada/Placa - identificacao";
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static string IdentityOf(Transform t)=>(t.parent==null?"":IdentityOf(t.parent)+"/")+t.name+"["+t.GetSiblingIndex()+"]";
    public static object Before()=>Capture("antes");
    public static object After()=>Capture("depois");
    public static object Audit()=>Inspect("final");
    static object Inspect(string phase)
    {
        var s=SceneManager.GetActiveScene();
        if(Application.isPlaying || s.path!="Assets/Scenes/PrototipoNecroterio.unity")throw new InvalidOperationException("Open prototype outside Play Mode.");
        var all=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var plate=GameObject.Find(Sign);
        if(plate==null)throw new InvalidOperationException("Main sign missing.");
        var components=all.Where(t=>!PathOf(t).StartsWith(Sign+"/")).SelectMany(t=>t.GetComponents<Component>())
            .Where(c=>c!=null).ToDictionary(c=>IdentityOf(c.transform)+"|"+c.GetType().FullName+"|"+Array.IndexOf(c.GetComponents<Component>(),c), c=>EditorJsonUtility.ToJson(c));
        var data=new {scene=s.path,dirty=s.isDirty,playing=Application.isPlaying,
            roots=s.GetRootGameObjects().Select(g=>g.name).ToArray(),componentsOutsideSign=components,
            lights=all.Sum(t=>t.GetComponents<Light>().Length),colliders=all.Sum(t=>t.GetComponents<Collider>().Length),
            volumes=all.Where(t=>t.GetComponent<Volume>()!=null).Select(t=>new {path=PathOf(t),volume=EditorJsonUtility.ToJson(t.GetComponent<Volume>()),profile=t.GetComponent<Volume>().sharedProfile!=null?EditorJsonUtility.ToJson(t.GetComponent<Volume>().sharedProfile):null}).ToArray(),
            lightmaps=LightmapSettings.lightmaps.Select(l=>new {color=AssetDatabase.GetAssetPath(l.lightmapColor),dir=AssetDatabase.GetAssetPath(l.lightmapDir),mask=AssetDatabase.GetAssetPath(l.shadowMask)}).ToArray(),
            sign=plate.GetComponentsInChildren<Renderer>(true).Select(r=>new {path=PathOf(r.transform),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject),bounds=r.bounds.ToString(),materials=r.sharedMaterials.Select(m=>new {path=AssetDatabase.GetAssetPath(m),shader=m.shader.name,color=m.GetColor("_BaseColor").ToString(),smooth=m.GetFloat("_Smoothness"),metal=m.GetFloat("_Metallic"),emission=m.IsKeywordEnabled("_EMISSION"),texture=AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"))}).ToArray()}).ToArray()};
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder+"/audit-"+phase+".json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
        return new {data.scene,data.dirty,data.playing,data.lights,data.colliders,data.sign};
    }
    static object Capture(string phase)
    {
        Inspect(phase);
        var source=GameObject.Find("Player/Camera").GetComponent<Camera>();
        var original=source.GetUniversalAdditionalCameraData();
        Directory.CreateDirectory(Folder+"/"+phase);
        var go=new GameObject("Temporary main sign review camera");
        var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
        var extra=camera.GetUniversalAdditionalCameraData();
        extra.renderPostProcessing=original.renderPostProcessing;extra.antialiasing=original.antialiasing;
        extra.antialiasingQuality=original.antialiasingQuality;extra.renderShadows=original.renderShadows;
        extra.volumeLayerMask=original.volumeLayerMask;extra.volumeTrigger=original.volumeTrigger;
        extra.dithering=original.dithering;extra.stopNaN=original.stopNaN;
        var views=new[]{
            ("frente-player",new Vector3(-11.4f,1.65f,-16.1f),new Vector3(-11.375f,1.65f,-12.475f)),
            ("angulo-player",new Vector3(-13.8f,1.65f,-15.4f),new Vector3(-11.375f,3.08f,-12.5f)),
            ("perto-inspecao",new Vector3(-11.375f,2.1f,-14.1f),new Vector3(-11.375f,3.08f,-12.5f))};
        var rt=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);
        var old=RenderTexture.active;
        try{
            camera.targetTexture=rt;camera.aspect=4f/3f;
            foreach(var p in views){
                camera.transform.SetPositionAndRotation(p.Item2,Quaternion.LookRotation(p.Item3-p.Item2));
                camera.Render();camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+phase+"/"+p.Item1+".png",image.EncodeToPNG());}
                finally{UnityEngine.Object.DestroyImmediate(image);}
            }
        }finally{RenderTexture.active=old;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        return new {phase,images=views.Length,sourceFov=source.fieldOfView,post=original.renderPostProcessing};
    }
}
