using UnityEditor;
using UnityEngine;

namespace Hyphen.Editor
{
    public static class HyphenPackageExporter
    {
        private const string PackageName = "HyphenLabel.unitypackage";
        private const string ExportPath = "Build/" + PackageName;

        [MenuItem("Hyphen/Export UnityPackage", false, 200)]
        public static void ExportPackage()
        {
            string[] assetPaths =
            {
                "Assets/Hyphen",
            };

            // Ensure the build directory exists
            System.IO.Directory.CreateDirectory("Build");

            AssetDatabase.ExportPackage(
                assetPaths,
                ExportPath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
            );

            Debug.Log($"[Hyphen] Package exported to {ExportPath}");
            EditorUtility.DisplayDialog(
                "Hyphen Package Exported",
                $"UnityPackage exported to:\n{System.IO.Path.GetFullPath(ExportPath)}",
                "OK");
        }
    }
}
