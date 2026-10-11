// Bathroom (WC): finish, fittings and wear. Fittings are prefabs modelled in Blender (Blender/WC, prefabs
// built by UnclaimedRecepcaoAssets into Assets/Modelos/WC); tiles and ceiling come from
// Blender/WC/scripts/texturas_wc.py. The door (frame, leaf, FolhaDePorta) is placed by Doorways.
// References and layout decisions: Blender/WC/referencias.md.
using UnityEditor;
using UnityEngine;

public static partial class UnclaimedMorgueGenerator
{
    private const string WCMatFolder = BaseFolder + "/WC", WCTextures = WCMatFolder + "/Texturas/";

    // Free interior (wall faces): 4.175 x 3.05 m, door in the south wall at x = -11.5.
    private const float WxMin = -14.65f, WxMax = -10.475f, WzMin = -7.025f, WzMax = -3.975f;
    // Fittings stand on the tile surface (finish panels sit FinishOffset in front of the walls).
    private const float WallMount = FinishOffset + 0.001f;

    // Floor porcelain 45 x 45, white wall tiles 30 x 45 up to 1.80 m (four full rows from the floor, so
    // the tile courses line up on every wall), washable paint above, PVC ceiling boards. Texture tiling
    // is in metres (finish panels carry world-space UVs).
    private static ZoneFinish WCFinish()
    {
        string f = WCMatFolder, w = WCTextures, t = RecepcaoTextures;
        return new ZoneFinish
        {
            Floor = Mat(f, "Piso porcelanato cinza", Color.white, 1f, 0f, w + "WC_Piso_Base.png", w + "WC_Piso_Normal.png", 1.35f,
                glossMap: w + "WC_Piso_MS.png"),
            Skirting = Mat(f, "Rodape porcelanato cinza", new Color(0.50f, 0.48f, 0.45f), 0.35f),
            Wainscot = Mat(f, "Azulejo branco", Color.white, 1f, 0f, w + "WC_Azulejo_Base.png", w + "WC_Azulejo_Normal.png", 0.9f,
                glossMap: w + "WC_Azulejo_MS.png"),
            WainscotHeight = 1.8f,
            UpperWall = Mat(f, "Pintura lavavel WC", new Color(0.97f, 0.97f, 0.95f), 0.25f, 0f, t + "Recepcao_Pintura_Base.png",
                t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
            Ceiling = Mat(f, "Forro PVC", Color.white, 1f, 0f, w + "WC_ForroPVC_Base.png", w + "WC_ForroPVC_Normal.png", 1f,
                glossMap: w + "WC_ForroPVC_MS.png"),
            Friso = Mat(f, "Perfil de acabamento", new Color(0.76f, 0.77f, 0.78f), 0.55f, 0.7f),   // aluminium tile edge
            CeilingTrim = Mat(f, "Moldura de PVC", new Color(0.93f, 0.93f, 0.91f), 0.45f),
        };
    }

    // Toilet against the north wall (centreline 0.45 m from the west wall) with the grab bar and the roll
    // dispenser on the side wall; basin, mirror, soap and towel dispensers on the east wall with the pedal
    // bin under the towels; the door's swing (south-east corner) stays clear.
    private static void BuildWC(Transform room)
    {
        Transform fit = Sub(room, "Loucas e metais"), acc = Sub(room, "Acessorios"), wear = Sub(room, "Marcas de uso");
        // Prefabs face -Z: on the west wall they turn to face east (yaw -90), on the east wall to face west (90).
        const float naParedeOeste = -90f, naParedeLeste = 90f;
        string wc(string prefab) => UnclaimedRecepcaoAssets.WCPrefabPath(prefab);
        if (WCProp(fit, wc("Vaso sanitario"), "Vaso sanitario", new Vector3(-14.20f, 0f, WzMax - 0.012f), 0f) == null)
            Block("Vaso sanitario", fit, -14.4f, -4.65f, -14.0f, -3.975f, 0, 0.45f, furnitureMat);
        WCProp(acc, wc("Barra de apoio"), "Barra de apoio", new Vector3(WxMin + WallMount, 0.75f, -4.62f), naParedeOeste);
        WCProp(acc, wc("Papeleira de rolao"), "Papeleira de rolao", new Vector3(WxMin + WallMount, 0.72f, -5.30f), naParedeOeste);
        if (WCProp(fit, wc("Lavatorio"), "Lavatorio", new Vector3(WxMax - WallMount, 0f, -5.25f), naParedeLeste) == null)
            Block("Pia do WC", fit, -10.9f, -5.5f, -10.475f, -5.0f, 0, 0.8f, furnitureMat);
        WCProp(acc, wc("Espelho"), "Espelho", new Vector3(WxMax - WallMount, 1.02f, -5.25f), naParedeLeste);
        WCProp(acc, wc("Saboneteira"), "Saboneteira", new Vector3(WxMax - WallMount, 1.05f, -4.80f), naParedeLeste);
        WCProp(acc, wc("Toalheiro de papel"), "Toalheiro de papel", new Vector3(WxMax - WallMount, 1.20f, -4.30f), naParedeLeste);
        WCProp(acc, UnclaimedRecepcaoAssets.PrefabPath("Lixeira"), "Lixeira", new Vector3(WxMax - 0.20f, 0f, -4.30f), naParedeLeste);

        // Wear, kept local: drain grate, grout darkened round the toilet foot (on the 45 cm tile grid),
        // limescale behind the tap, water marks in front of the basin.
        Decal("Ralo", wear, new Vector3(-12.30f, 0.0045f, -5.40f), Vector3.up, Vector3.right, Vector3.forward, 0.15f, 0.15f, drain);
        Material grime = Mat(WCMatFolder, "Decal - rejunte encardido", new Color(1f, 1f, 1f, 1f), 0.1f, 0f,
            WCTextures + "Decal_RejunteEncardido.png", transparent: true);
        Decal("Rejunte encardido - vaso", wear, new Vector3(-14.40f, 0.0045f, -4.50f), Vector3.up, Vector3.right, Vector3.forward, 0.9f, 0.9f, grime);
        Decal("Calcario - torneira", wear, new Vector3(WxMax - 0.006f, 0.87f, -5.25f), Vector3.left, Vector3.forward, Vector3.up, 0.32f, 0.12f, decalLimescale);
        Decal("Marcas de agua - lavatorio", wear, new Vector3(-10.95f, 0.0045f, -5.25f), Vector3.up, Vector3.forward, Vector3.right, 0.55f, 0.45f, decalFloorWear);
    }

    private static GameObject WCProp(Transform parent, string prefabPath, string name, Vector3 position, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Unclaimed: missing prefab " + prefabPath + "; run Tools > Unclaimed > Build Recepcao Prefabs.");
            return null;
        }
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return obj;
    }
}
