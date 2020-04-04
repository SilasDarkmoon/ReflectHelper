using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Capstones.UnityEngineEx;

using Object = UnityEngine.Object;

namespace Capstones.UnityEditorEx
{
    [Serializable]
    public class InterAppDomainCaller
    {
        public AppDomain MainDomain;
        public string[] MainDomainAsms;

        public void InvokePlugin()
        {
            var mainDomain = MainDomain;
            var curdomain = AppDomain.CurrentDomain;

            HashSet<string> mainDomainAsms = new HashSet<string>();
            if (MainDomainAsms != null)
            {
                mainDomainAsms.UnionWith(MainDomainAsms);
            }

            MethodInfo pluginEntry = null;
            foreach (var asm in curdomain.GetAssemblies())
            {
                if (!mainDomainAsms.Contains(asm.FullName))
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if ((pluginEntry = type.GetMethod("Main", new Type[0])) != null)
                        {
                            if (pluginEntry.IsStatic)
                            {
                                break;
                            }
                            else
                            {
                                pluginEntry = null;
                            }
                        }
                    }
                }
                if (pluginEntry != null)
                {
                    break;
                }
            }

            if (pluginEntry != null)
            {
                var cb = (CrossAppDomainDelegate)Delegate.CreateDelegate(typeof(CrossAppDomainDelegate), pluginEntry);
                mainDomain.DoCallBack(cb);
            }
            else
            {
                Debug.LogError("Cannot find Entry Point for the Plugin.");
            }
        }
    }

    public class CSharpConsole : EditorWindow
    {
        [MenuItem("Lua/C# Console", priority = 300020)]
        static void Init()
        {
            GetWindow(typeof(CSharpConsole)).titleContent = new GUIContent("C# Console");
        }

        public string Command;
        public Vector2 Scroll;
        private static int Index;

        void OnGUI()
        {
            Scroll = EditorGUILayout.BeginScrollView(Scroll);
            Command = EditorGUILayout.TextArea(Command);
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Run!"))
            {
                // Fix Command.


                var index = Index;
                string dllname = "CSharpConsolePlugin" + index;
                var src = System.IO.Path.GetFullPath("EditorOutput/Intermediate/" + dllname + ".cs");
                var tar = System.IO.Path.GetFullPath("EditorOutput/Intermediate/" + dllname + ".dll");
                PlatDependant.WriteAllText(src, Command);

                var compiler = new Microsoft.CSharp.CSharpCodeProvider();
                var compilerOption = new System.CodeDom.Compiler.CompilerParameters();
                compilerOption.IncludeDebugInformation = true;
                compilerOption.GenerateExecutable = false;
                compilerOption.OutputAssembly = tar;
                compilerOption.CompilerOptions = "-nostdlib";
                //compilerOption.ReferencedAssemblies.Add(@"C:\Program Files\Unity2019\Editor\Data\NetStandard\ref\2.0.0\netstandard.dll");
                //compilerOption.ReferencedAssemblies.Add(@"C:\Program Files\Unity2019\Editor\Data\Managed\UnityEngine.dll");
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var loc = asm.Location;
                        //Debug.Log(loc);
                        compilerOption.ReferencedAssemblies.Add(asm.Location);
                    }
                    catch (Exception e)
                    {
                        //PlatDependant.LogError(e);
                    }
                }
                var compileresult = compiler.CompileAssemblyFromFile(compilerOption, src);

                //var builder = new UnityEditor.Compilation.AssemblyBuilder("EditorOutput/Intermediate/Temp.dll", "EditorOutput/Intermediate/Temp.cs");
                //builder.Build();

                if (compileresult.Errors.Count > 0)
                {
                    foreach (var error in compileresult.Errors)
                    {
                        Debug.LogError(error);
                    }
                    return;
                }

                ++Index;
                var plugin = AppDomain.CurrentDomain.Load(PlatDependant.ReadAllBytes(tar));
                MethodInfo pluginEntry = null;
                foreach (var type in plugin.GetTypes())
                {
                    if ((pluginEntry = type.GetMethod("Main", new Type[0])) != null)
                    {
                        if (pluginEntry.IsStatic)
                        {
                            break;
                        }
                        else
                        {
                            pluginEntry = null;
                        }
                    }
                }
                if (pluginEntry != null)
                {
                    pluginEntry.Invoke(null, new object[0]);
                }
            }
        }

//        [MenuItem("Test/Lua/Console")]
//        public static void TestConsole()
//        {
//            string command =
//@"
//namespace TestNamespace
//{
//    public static class Entry
//    {
//        public static void Main()
//        {
//            UnityEngine.Debug.LogError(1);
//        }
//    }
//}
//";
//            var compiler = new Microsoft.CSharp.CSharpCodeProvider();
//            var compilerOption = new System.CodeDom.Compiler.CompilerParameters();
//            var src = System.IO.Path.GetFullPath("EditorOutput/Intermediate/Temp.cs");
//            //PlatDependant.WriteAllText(src, command);
//            var tar = System.IO.Path.GetFullPath("EditorOutput/Intermediate/Temp.dll");
//            compilerOption.IncludeDebugInformation = true;
//            compilerOption.GenerateExecutable = false;
//            compilerOption.OutputAssembly = tar;
//            //compilerOption.CompilerOptions = "-nostdlib";
//            //compilerOption.ReferencedAssemblies.Add(@"C:\Program Files\Unity2019\Editor\Data\NetStandard\ref\2.0.0\netstandard.dll");
//            compilerOption.ReferencedAssemblies.Add(@"C:\Program Files\Unity2019\Editor\Data\Managed\UnityEngine.dll");
//            //foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
//            //{
//            //    try
//            //    {
//            //        var loc = asm.Location;
//            //        compilerOption.ReferencedAssemblies.Add(asm.Location);
//            //    }
//            //    catch (Exception e) { PlatDependant.LogError(e); }
//            //}
//            var compileresult = compiler.CompileAssemblyFromFile(compilerOption, src);

//            //var builder = new UnityEditor.Compilation.AssemblyBuilder("EditorOutput/Intermediate/Temp.dll", "EditorOutput/Intermediate/Temp.cs");
//            //builder.Build();

//            if (compileresult.Errors.Count > 0)
//            {
//                foreach (var error in compileresult.Errors)
//                {
//                    Debug.LogError(error);
//                }
//                return;
//            }

//            var ad = AppDomain.CreateDomain("Plugin");
//            try
//            {
//                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
//                {
//                    try
//                    {
//                        var loc = asm.Location;
//                        Debug.LogError(loc);
//                        ad.Load(PlatDependant.ReadAllBytes(loc));
//                    }
//                    catch (Exception e) { PlatDependant.LogError(e); }
//                }
//                ad.Load(PlatDependant.ReadAllBytes(@"C:\Program Files\Unity2019\Editor\Data\Managed\UnityEditor.dll"));

//                var caller = new InterAppDomainCaller() { MainDomain = AppDomain.CurrentDomain };
//                var asms = new List<string>();
//                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
//                {
//                    asms.Add(asm.FullName);
//                }
//                caller.MainDomainAsms = asms.ToArray();

//                var plugin = AppDomain.CurrentDomain.Load(PlatDependant.ReadAllBytes(tar));
//                MethodInfo pluginEntry = null;
//                foreach (var type in plugin.GetTypes())
//                {
//                    if ((pluginEntry = type.GetMethod("Main", new Type[0])) != null)
//                    {
//                        if (pluginEntry.IsStatic)
//                        {
//                            break;
//                        }
//                        else
//                        {
//                            pluginEntry = null;
//                        }
//                    }
//                }
//                if (pluginEntry != null)
//                {
//                    pluginEntry.Invoke(null, new object[0]);
//                }

//                //ad.DoCallBack(caller.InvokePlugin);
//            }
//            finally
//            {
//                AppDomain.Unload(ad);
//            }
//        }
    }
}