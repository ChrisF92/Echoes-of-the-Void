using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace EchoesOfTheVoid.EditorTools
{
    public static class ScaffoldAssemblies
    {
        private const string RootNamespace = "EchoesOfTheVoid"; // Change if you prefer a different root namespace

        [MenuItem("Tools/Echoes Of The Void/Generate Scripts Structure + Asmdefs")]
        public static void Generate() => GenerateInternal(false);

        [MenuItem("Tools/Echoes Of The Void/Generate Scripts Structure + Asmdefs (Overwrite)")]
        public static void GenerateOverwrite() => GenerateInternal(true);

        private static void GenerateInternal(bool overwrite)
        {
            var dataPath = Application.dataPath;
            var scriptsRoot = Path.Combine(dataPath, "Scripts");

            // Ensure folders
            EnsureFolder(scriptsRoot);
            EnsureFolder(Path.Combine(scriptsRoot, "Core"));
            EnsureFolder(Path.Combine(scriptsRoot, "Combat"));
            EnsureFolder(Path.Combine(scriptsRoot, "UI"));
            EnsureFolder(Path.Combine(scriptsRoot, "UI", "UITK"));
            EnsureFolder(Path.Combine(scriptsRoot, "Items"));
            EnsureFolder(Path.Combine(scriptsRoot, "Skills"));

            // Detect available external assemblies (e.g., UGUI)
            var availableAssemblies = CompilationPipeline.GetAssemblies().Select(a => a.name).ToHashSet();
            bool hasUGUI = availableAssemblies.Contains("UnityEngine.UI");

            // Define assembly names
            string coreAsm = $"{RootNamespace}.Core";
            string combatAsm = $"{RootNamespace}.Combat";
            string uiAsm = $"{RootNamespace}.UI";
            string uiUITKAsm = $"{RootNamespace}.UI.UITK";
            string itemsAsm = $"{RootNamespace}.Items";
            string skillsAsm = $"{RootNamespace}.Skills";

            // Write asmdefs with minimal, explicit references
            WriteAsmdef(Path.Combine(scriptsRoot, "Core", $"{coreAsm}.asmdef"),
                coreAsm, RootNamespace, new string[] { }, overwrite);

            WriteAsmdef(Path.Combine(scriptsRoot, "Combat", $"{combatAsm}.asmdef"),
                combatAsm, RootNamespace, new[] { coreAsm }, overwrite);

            var uiRefs = new List<string> { coreAsm };
            if (hasUGUI)
                uiRefs.Add("UnityEngine.UI"); // Only add UGUI if present

            WriteAsmdef(Path.Combine(scriptsRoot, "UI", $"{uiAsm}.asmdef"),
                uiAsm, RootNamespace, uiRefs, overwrite);

            WriteAsmdef(Path.Combine(scriptsRoot, "UI", "UITK", $"{uiUITKAsm}.asmdef"),
                uiUITKAsm, RootNamespace, new[] { coreAsm }, overwrite);

            WriteAsmdef(Path.Combine(scriptsRoot, "Items", $"{itemsAsm}.asmdef"),
                itemsAsm, RootNamespace, new[] { coreAsm }, overwrite);

            WriteAsmdef(Path.Combine(scriptsRoot, "Skills", $"{skillsAsm}.asmdef"),
                skillsAsm, RootNamespace, new[] { coreAsm }, overwrite);

            AssetDatabase.Refresh();

            Debug.Log(
                $"Echoes Of The Void: Folder structure and asmdefs generated. " +
                (hasUGUI ? "UGUI detected and referenced by UI asmdef." : "UGUI not detected; UI asmdef excludes UnityEngine.UI reference."));
        }

        private static void EnsureFolder(string fullPath)
        {
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }

        private static void WriteAsmdef(string fullPath, string name, string rootNamespace, IEnumerable<string> references, bool overwrite)
        {
            if (File.Exists(fullPath) && !overwrite)
                return;

            var json = BuildAsmdefJson(name, rootNamespace, references);
            File.WriteAllText(fullPath, json);
        }

        private static string BuildAsmdefJson(string name, string rootNamespace, IEnumerable<string> references)
        {
            string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var refs = string.Join(", ", (references ?? Enumerable.Empty<string>()).Select(r => $"\"{Esc(r)}\""));

            return "{\n" +
                   $"  \"name\": \"{Esc(name)}\",\n" +
                   $"  \"rootNamespace\": \"{Esc(rootNamespace)}\",\n" +
                   $"  \"references\": [{refs}],\n" +
                   "  \"includePlatforms\": [],\n" +
                   "  \"excludePlatforms\": [],\n" +
                   "  \"allowUnsafeCode\": false,\n" +
                   "  \"overrideReferences\": false,\n" +
                   "  \"precompiledReferences\": [],\n" +
                   "  \"autoReferenced\": true,\n" +
                   "  \"defineConstraints\": [],\n" +
                   "  \"versionDefines\": [],\n" +
                   "  \"noEngineReferences\": false\n" +
                   "}\n";
        }
    }
}

