using UnityEditor;
using UnityEngine;

public class ImportEasyRoadsHDRP
{
    [MenuItem("Tools/Fix EasyRoads HDRP Shaders")]
    public static void ImportHDRP14()
    {
        string packagePath = "Assets/EasyRoads3D/SRP Support Packages/HDRP_14_0_4.unitypackage";
        AssetDatabase.ImportPackage(packagePath, false);
        Debug.Log("Successfully started importing HDRP 14 shaders for EasyRoads. The pink materials and shadow errors will be resolved.");
    }
}
