using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
public static class CorrecoesReview
{
    const string Dir="Blender/Recebimento/revisao/correcoes";
    static Transform[] All()=>UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.gameObject.scene==EditorSceneManager.GetActiveScene()).ToArray();
    public static object Baseline(){Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"/baseline.json",Snapshot());return new{saved=true};}
    static string Snapshot()=>Newtonsoft.Json.JsonConvert.SerializeObject(All().SelectMany(t=>t.GetComponents<Light>()).OrderBy(l=>l.name).Select(l=>new {l.name,p=l.transform.position.ToString("F4"),l.intensity,l.range,color=new[]{l.color.r,l.color.g,l.color.b},l.enabled}));
    public static object Fixtures()
    {
        if(Application.isPlaying)throw new Exception("Edit mode only");
        var all=All();
        var ceiling=all.Single(t=>t.name=="Luminarias - Recebimento de corpos");
        foreach(Transform child in ceiling.Cast<Transform>().ToArray())Undo.DestroyObjectImmediate(child.gameObject);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(UnclaimedRecepcaoAssets.FittingPrefab));
        int i=0;foreach(float x in new[]{5.6f,10.4f}){
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,ceiling);Undo.RegisterCreatedObjectUndo(go,"Receiving fixture");go.name="Luminaria "+(++i);go.transform.SetPositionAndRotation(new Vector3(x,3.197f,8.4f),Quaternion.identity);
        }
        var wp=AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(UnclaimedRecepcaoAssets.WallLampPrefab));
        foreach(var item in new[]{("Refletor - porta do patio",new Vector3(12.853f,3f,8.375f),Vector3.right),("Refletor - patio norte",new Vector3(11f,3f,11.603f),Vector3.forward),("Luminaria - agenda de coleta",new Vector3(12.853f,2.32f,10.15f),Vector3.right)}){
            var old=all.Single(t=>t!=null&&t.name==item.Item1);var parent=old.parent;Undo.DestroyObjectImmediate(old.gameObject);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(wp,parent);Undo.RegisterCreatedObjectUndo(go,"Receiving wall fixture");go.name=item.Item1;go.transform.SetPositionAndRotation(item.Item2,Quaternion.LookRotation(-item.Item3));
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());return new{fixtures=5,lightsUnchanged=Snapshot()==File.ReadAllText(Dir+"/baseline.json")};
    }
    public static object Capture()
    {
        Directory.CreateDirectory(Dir);
        var source=GameObject.Find("Player/Camera").GetComponent<Camera>();
        var go=new GameObject("Temporary correction review");var c=go.AddComponent<Camera>();c.CopyFrom(source);c.enabled=false;c.useOcclusionCulling=false;
        c.GetUniversalAdditionalCameraData().renderPostProcessing=source.GetUniversalAdditionalCameraData().renderPostProcessing;
        var views=new[]{
            ("maca",new Vector3(9.55f,1.52f,6.9f),new Vector3(8,0.87f,8.375f)),
            ("lavagem",new Vector3(5.6f,1.55f,6.8f),new Vector3(3.9f,.75f,8.4f)),
            ("mesa-auxiliar",new Vector3(11.15f,1.35f,9.95f),new Vector3(12.18f,.64f,10.95f)),
            ("epi",new Vector3(7.25f,1.65f,10.05f),new Vector3(7.85f,1.35f,11.365f)),
            ("luminarias",new Vector3(7.1f,1.6f,9.9f),new Vector3(8f,2.6f,8.4f)),
            ("cobertura",new Vector3(15.7f,2.7f,5.9f),new Vector3(13.2f,1.8f,7.7f))};
        var rt=new RenderTexture(1440,1080,24);var prev=RenderTexture.active;
        try{c.targetTexture=rt;c.aspect=4f/3;c.fieldOfView=55;
            foreach(var v in views){c.transform.SetPositionAndRotation(v.Item2,Quaternion.LookRotation(v.Item3-v.Item2));c.Render();c.Render();RenderTexture.active=rt;var im=new Texture2D(1440,1080,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,1440,1080),0,0);im.Apply();File.WriteAllBytes(Dir+"/"+v.Item1+".png",im.EncodeToPNG());UnityEngine.Object.DestroyImmediate(im);}
        }finally{RenderTexture.active=prev;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        var bad=All().SelectMany(t=>t.GetComponents<Renderer>()).Where(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name.Contains("InternalError"))).Select(r=>r.name).ToArray();
        var result=new{views=views.Length,badMaterials=bad,lightsUnchanged=Snapshot()==File.ReadAllText(Dir+"/baseline.json")};File.WriteAllText(Dir+"/verificacao.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));return result;
    }
}
