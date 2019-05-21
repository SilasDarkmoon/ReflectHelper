using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;

using Object = UnityEngine.Object;

namespace Capstones.UnityEditorEx
{
    public static class ReflectHelper
    {
        private class AssemblyInfo
        {
            public string Name;
            public Assembly AsmRuntime;
            public UnityEditor.Compilation.Assembly AsmCompile;
            public AssemblyDefinition AsmMonoDefinition;
            public Mono.Cecil.Cil.ISymbolReader AsmSymbolReader;
        }
        private static readonly Dictionary<string, AssemblyInfo> _Assemblies = new Dictionary<string, AssemblyInfo>();

        private static readonly MdbReaderProvider _MdbProvider = new MdbReaderProvider();
        private static readonly PdbReaderProvider _PdbProvider = new PdbReaderProvider();

        public static bool LoadSymbols()
        {
            if (_Assemblies.Count > 0)
            {
                return false;
            }

            foreach (var asm in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
            {
                _Assemblies[asm.name] = new AssemblyInfo() { Name = asm.name, AsmCompile = asm };
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name;
                AssemblyInfo info;
                if (_Assemblies.TryGetValue(name, out info))
                {
                    info.AsmRuntime = asm;
                    try
                    {
                        info.AsmMonoDefinition = AssemblyDefinition.ReadAssembly(asm.Location);
                    }
                    catch //(Exception e)
                    {
                        //Debug.LogException(e);
                    }
                    try
                    {
                        var afile = asm.Location;
                        string sfile = afile + ".mdb";
                        bool exist = false;
                        if (!(exist = System.IO.File.Exists(sfile)))
                        {
                            sfile = afile + ".pdb";
                            if (!(exist = System.IO.File.Exists(sfile)))
                            {
                                var ext = System.IO.Path.GetExtension(afile);
                                if (ext != null)
                                {
                                    var pfile = afile.Substring(0, afile.Length - ext.Length);
                                    sfile = pfile + ".mdb";
                                    if (!(exist = System.IO.File.Exists(sfile)))
                                    {
                                        sfile = pfile + ".pdb";
                                        if (!(exist = System.IO.File.Exists(sfile)))
                                        {
                                            //continue;
                                        }
                                    }
                                }
                            }
                        }

                        if (exist)
                        {
                            if (sfile.EndsWith(".pdb"))
                            {
                                info.AsmSymbolReader = _PdbProvider.GetSymbolReader(info.AsmMonoDefinition.MainModule, sfile);
                            }
                            else if (sfile.EndsWith(".mdb"))
                            {
                                info.AsmSymbolReader = _MdbProvider.GetSymbolReader(info.AsmMonoDefinition.MainModule, sfile);
                            }
                        }
                        //else
                        //{
                        //    continue;
                        //}
                    }
                    catch //(Exception e)
                    {
                        //Debug.LogException(e);
                    }
                }
            }

            return true;
        }

        public static bool UnloadSymbols()
        {
            if (_Assemblies.Count == 0)
            {
                return false;
            }

            foreach (var info in _Assemblies.Values)
            {
                try
                {
                    if (info.AsmSymbolReader != null)
                    {
                        info.AsmSymbolReader.Dispose();
                    }
                }
                catch //(Exception e)
                {
                    //Debug.LogException(e);
                }
                try
                {
                    if (info.AsmMonoDefinition != null)
                    {
                        info.AsmMonoDefinition.Dispose();
                    }
                }
                catch //(Exception e)
                {
                    //Debug.LogException(e);
                }
            }
            _Assemblies.Clear();

            return true;
        }

        /// <summary>
        /// Is the asm written by user?
        /// </summary>
        public static bool IsUserScript(this Assembly asm)
        {
            if (asm != null)
            {
                var name = asm.GetName().Name;
                AssemblyInfo info;
                if (_Assemblies.TryGetValue(name, out info))
                {
                    if (info.AsmSymbolReader != null)
                    {
                        if (info.AsmCompile.sourceFiles != null && info.AsmCompile.sourceFiles.Length > 0)
                        {
                            var file = info.AsmCompile.sourceFiles[0];
                            var package = CapsModEditor.GetPackageNameFromPath(file);
                            if (string.IsNullOrEmpty(package))
                            {
                                var assetdir = System.IO.Path.GetFullPath("Assets");
                                return file.StartsWith(assetdir, StringComparison.InvariantCultureIgnoreCase)
                                    && file.Length > assetdir.Length + 1
                                    && (file[assetdir.Length] == '/' || file[assetdir.Length] == '\\');
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
        /// <summary>
        /// Is the asm a dll in project? (exclude base .net assembly, base Unity assembly, dll in packages that not belong to user)
        /// </summary>
        public static bool IsUserPlugin(this Assembly asm)
        {
            if (asm != null)
            {
                try
                {
                    var file = asm.Location;
                    var assetdir = System.IO.Path.GetFullPath("Assets");
                    if (file.StartsWith(assetdir, StringComparison.InvariantCultureIgnoreCase)
                        && file.Length > assetdir.Length + 1 && (file[assetdir.Length] == '/' || file[assetdir.Length] == '\\')
                        || !string.IsNullOrEmpty(CapsModEditor.GetPackageNameFromPath(file)))
                    {
                        return true;
                    }
                }
                catch //(Exception e)
                {
                    //Debug.LogException(e);
                }
            }

            return false;
        }
        /// <summary>
        /// Is the asm written by user or is the asm a dll in project
        /// </summary>
        public static bool IsUserOrPluginScript(this Assembly asm)
        {
            return IsUserScript(asm) || IsUserPlugin(asm);
        }

        public enum ScriptCategory
        {
            Plugin = 0,
            Client = 1,
            Editor = 2,
        }
        public static ScriptCategory GetScriptCategory(this Assembly asm)
        {
            // user script
            if (asm != null)
            {
                var name = asm.GetName().Name;
                AssemblyInfo info;
                if (_Assemblies.TryGetValue(name, out info))
                {
                    // Editor?
                    if ((info.AsmCompile.flags & UnityEditor.Compilation.AssemblyFlags.EditorAssembly) != 0)
                    {
                        return ScriptCategory.Editor;
                    }

                    if (info.AsmSymbolReader != null)
                    {
                        if (info.AsmCompile.sourceFiles != null && info.AsmCompile.sourceFiles.Length > 0)
                        {
                            var file = info.AsmCompile.sourceFiles[0];
                            var package = CapsModEditor.GetPackageNameFromPath(file);
                            if (string.IsNullOrEmpty(package))
                            {
                                var assetdir = System.IO.Path.GetFullPath("Assets");
                                if (file.StartsWith(assetdir, StringComparison.InvariantCultureIgnoreCase)
                                    && file.Length > assetdir.Length + 1
                                    && (file[assetdir.Length] == '/' || file[assetdir.Length] == '\\'))
                                {
                                    var asset = file.Substring(assetdir.Length + 1);
                                    if (asset.StartsWith("Plugins/", StringComparison.InvariantCultureIgnoreCase)
                                        || asset.StartsWith("Standard Assets/", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        return ScriptCategory.Plugin;
                                    }
                                    else
                                    {
                                        return ScriptCategory.Client;
                                    }
                                }
                            }
                            else
                            {
                                return ScriptCategory.Plugin;
                            }
                        }
                    }
                }

                // user plugin dll
                try
                {
                    var file = asm.Location;
                    var package = CapsModEditor.GetPackageNameFromPath(file);
                    string asset = null;
                    if (!string.IsNullOrEmpty(package))
                    {
                        var part = file.Substring(CapsModEditor.GetPackageRoot(package).Length);
                        asset = "Packages/" + package + part;
                    }
                    else
                    {
                        var assetdir = System.IO.Path.GetFullPath("Assets");
                        if (file.StartsWith(assetdir, StringComparison.InvariantCultureIgnoreCase)
                            && file.Length > assetdir.Length + 1 && (file[assetdir.Length] == '/' || file[assetdir.Length] == '\\'))
                        {
                            asset = file.Substring(assetdir.Length - "Assets".Length);
                        }
                    }
                    if (asset != null)
                    {
                        UnityEditor.PluginImporter pim = AssetImporter.GetAtPath(asset) as UnityEditor.PluginImporter;
                        if (pim != null)
                        {
                            if (pim.GetCompatibleWithEditor()
                                && !pim.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux)
                                && !pim.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64)
                                && !pim.GetCompatibleWithPlatform(BuildTarget.StandaloneLinuxUniversal)
                                && !pim.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX)
                                && !pim.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows)
                                && !pim.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                                )
                            {
                                return ScriptCategory.Editor;
                            }
                        }
                        return ScriptCategory.Plugin;
                    }

                    if (System.IO.Path.GetFileName(file).Contains("UnityEditor."))
                    {
                        return ScriptCategory.Editor;
                    }
                    var appdir = System.IO.Path.GetDirectoryName(EditorApplication.applicationPath);
                    if (file.StartsWith(appdir, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var sub = file.Substring(appdir.Length);
                        if (sub.Length > 1 && (sub[0] == '/' || sub[0] == '\\'))
                        {
                            sub = sub.Substring(1);
                            if (System.IO.Path.GetDirectoryName(sub).Replace('\\', '/').Contains("/Editor/"))
                            {
                                return ScriptCategory.Editor;
                            }
                        }
                    }
                }
                catch //(Exception e)
                {
                    //Debug.LogException(e);
                }
            }

            // all others are considered as Plugin.
            return ScriptCategory.Plugin;
        }

        private static string GetScriptSource(Type type, AssemblyDefinition ad, Mono.Cecil.Cil.ISymbolReader dr)
        {
            var td = ad.MainModule.GetType(type.Namespace, type.Name);
            if (td != null)
            {
                var methods = td.Methods;
                if (methods != null)
                {
                    foreach (var method in methods)
                    {
                        if (method.Body != null && method.Body.Instructions != null)
                        {
                            var di = dr.Read(method);
                            if (di != null)
                            {
                                foreach (var ins in method.Body.Instructions)
                                {
                                    var sp = di.GetSequencePoint(ins);
                                    if (sp != null && sp.Document != null && !string.IsNullOrEmpty(sp.Document.Url))
                                    {
                                        var file = sp.Document.Url;
                                        //if (System.IO.File.Exists(file)) // Do we need the file must exist.
                                        {
                                            return file;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
        public static string GetScriptSource(this Type type)
        {
            if (type != null)
            {
                var asm = type.Assembly;
                var asmname = asm.GetName().Name;
                AssemblyInfo info;
                if (_Assemblies.TryGetValue(asmname, out info))
                {
                    var ad = info.AsmMonoDefinition;
                    var dr = info.AsmSymbolReader;
                    if (ad != null && dr != null)
                    {
                        string src = null;
                        var ptype = type;
                        while (string.IsNullOrEmpty(src) && ptype != null)
                        {
                            src = GetScriptSource(ptype, ad, dr);
                            ptype = ptype.DeclaringType;
                        }
                        return src;
                    }
                }
            }

            return null;
        }
        private static string GetScriptMod(Type type, AssemblyDefinition ad, Mono.Cecil.Cil.ISymbolReader dr)
        {
            var src = GetScriptSource(type, ad, dr);
            if (!string.IsNullOrEmpty(src))
            {
                var asset = CapsEditorUtils.GetAssetNameFromPath(src);
                if (!string.IsNullOrEmpty(asset))
                {
                    return CapsModEditor.GetAssetModName(asset);
                }
            }
            return "";
        }
        public static string GetScriptMod(this Type type)
        {
            var src = GetScriptSource(type);
            if (!string.IsNullOrEmpty(src))
            {
                var asset = CapsEditorUtils.GetAssetNameFromPath(src);
                if (!string.IsNullOrEmpty(asset))
                {
                    return CapsModEditor.GetAssetModName(asset);
                }
            }
            return "";
        }
        private static string GetScriptSource(MethodBase m, AssemblyDefinition ad, Mono.Cecil.Cil.ISymbolReader dr)
        {
            var type = m.DeclaringType;
            var td = ad.MainModule.GetType(type.Namespace, type.Name);
            if (td != null)
            {
                var methods = td.Methods;
                if (methods != null)
                {
                    foreach (var method in methods)
                    {
                        if (method.Name == m.Name)
                        {
                            if (method.Body != null && method.Body.Instructions != null)
                            {
                                var di = dr.Read(method);
                                if (di != null)
                                {
                                    var curdir = System.IO.Path.GetFullPath(".");
                                    foreach (var ins in method.Body.Instructions)
                                    {
                                        var sp = di.GetSequencePoint(ins);
                                        if (sp != null && sp.Document != null && !string.IsNullOrEmpty(sp.Document.Url))
                                        {
                                            var file = sp.Document.Url;
                                            //if (System.IO.File.Exists(file)) // Do we need the file must exist.
                                            {
                                                return file;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
        public static string GetScriptSource(this MethodBase m)
        {
            if (m != null)
            {
                var asm = m.DeclaringType.Assembly;
                var asmname = asm.GetName().Name;
                AssemblyInfo info;
                if (_Assemblies.TryGetValue(asmname, out info))
                {
                    var ad = info.AsmMonoDefinition;
                    var dr = info.AsmSymbolReader;
                    if (ad != null && dr != null)
                    {
                        return GetScriptSource(m, ad, dr);
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// not recommend. use System.Type to get its mod name instead. we donot like partial class which is splitted into different mods. 
        /// </summary>
        public static string GetScriptMod(this MemberInfo member)
        {
            if (member is Type)
            {
                return GetScriptMod(member as Type);
            }
            if (member is MethodBase)
            {
                var src = GetScriptSource(member as MethodBase);
                if (!string.IsNullOrEmpty(src))
                {
                    var asset = CapsEditorUtils.GetAssetNameFromPath(src);
                    if (!string.IsNullOrEmpty(asset))
                    {
                        return CapsModEditor.GetAssetModName(asset);
                    }
                }
                return GetScriptMod(member.DeclaringType);
            }
            else if (member is PropertyInfo)
            {
                var pi = member as PropertyInfo;
                var gm = pi.GetGetMethod();
                if (gm != null)
                {
                    return GetScriptMod(gm);
                }
                var sm = pi.GetSetMethod();
                if (sm != null)
                {
                    return GetScriptMod(sm);
                }
                return GetScriptMod(member.DeclaringType);
            }
            else
            {
                return GetScriptMod(member.DeclaringType);
            }
        }
    }
}