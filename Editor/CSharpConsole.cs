using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using Capstones.UnityEngineEx;

using Object = UnityEngine.Object;

namespace Capstones.UnityEditorEx
{
    public class CSharpConsole : EditorWindow
    {
#if MOD_CAPSLUA
        [MenuItem("Lua/C# Console", priority = 300020)]
#endif
        [MenuItem("Tools/C# Console", priority = 100010)]
        static void Init()
        {
            GetWindow(typeof(CSharpConsole)).titleContent = new GUIContent("C# Console");
        }

        public string Command;
        public string Result;
        //public Vector2 CommandScroll;
        public Vector2 ResultScroll;
        private static int Index;
        private static HashSet<string> PreClass = new HashSet<string>()
        {
            "public", "internal", "static", "abstract", "sealed", "ref"
        };
        private static List<string> CommonUsing = new List<string>()
        {
            "using System;",
            "using System.Collections;",
            "using System.Collections.Generic;",
            "using UnityEngine;",
            "using UnityEditor;",
            "using Capstones.UnityEngineEx;",
            "using Capstones.UnityEditorEx;",
            "using Capstones.Net;",

            "using Capstones.LuaLib;",
            "using Capstones.LuaWrap;",
            "using Capstones.LuaExt;",
            "using lua = Capstones.LuaLib.LuaCoreLib;",
            "using lual = Capstones.LuaLib.LuaAuxLib;",
            "using luae = Capstones.LuaLib.LuaLibEx;",

            "using Object = UnityEngine.Object;",
        };

        void OnGUI()
        {
            //CommandScroll = EditorGUILayout.BeginScrollView(CommandScroll, GUILayout.MaxHeight(500));
            Command = EditorGUILayout.TextArea(Command, GUILayout.MaxHeight(500));
            //EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Run!"))
            {
                var index = Index;
                string dllname = "CSharpConsolePlugin" + index;
                var src = System.IO.Path.GetFullPath("EditorOutput/Intermediate/" + dllname + ".cs");
                var tar = System.IO.Path.GetFullPath("EditorOutput/Intermediate/" + dllname + ".dll");

                try
                {
                    // Fix Command.
                    bool isRawCommands = true;
                    bool hasReturn = false;
                    int classline = -1;
                    List<string> usingLines = new List<string>();
                    var lines = Command.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; ++i)
                    { // find class def
                        var line = lines[i].Trim();
                        if (line.StartsWith("namespace "))
                        {
                            isRawCommands = false;
                            break;
                        }
                        if (line.StartsWith("//"))
                        {
                            continue;
                        }
                        while (line.StartsWith("["))
                        {
                            var endIndex = line.IndexOf(']');
                            while (endIndex < 0)
                            {
                                line = lines[++i].Trim();
                                endIndex = line.IndexOf(']');
                            }
                            line = line.Substring(endIndex + 1).TrimStart();
                        }

                        var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int j = 0; j < parts.Length; ++j)
                        {
                            var part = parts[j];
                            if (part == "class" || part == "struct")
                            {
                                bool isvalid = true;
                                for (int k = 0; k < j; ++k)
                                {
                                    var prepart = parts[k];
                                    if (!PreClass.Contains(prepart))
                                    {
                                        isvalid = false;
                                        break;
                                    }
                                }
                                if (isvalid)
                                {
                                    classline = i;
                                    isRawCommands = false;
                                }
                                break;
                            }
                        }
                        if (!isRawCommands)
                        {
                            break;
                        }
                    }
                    if (classline >= 0)
                    { // find if it is valid before class def
                        for (int i = 0; i < classline; ++i)
                        {
                            var line = lines[i].Trim();
                            while (line.StartsWith("["))
                            {
                                var endIndex = line.IndexOf(']');
                                while (endIndex < 0)
                                {
                                    line = lines[++i].Trim();
                                    endIndex = line.IndexOf(']');
                                }
                                line = line.Substring(endIndex + 1).TrimStart();
                            }
                            if (string.IsNullOrEmpty(line))
                            {
                                continue;
                            }
                            if (line.StartsWith("namespace "))
                            {
                                continue;
                            }
                            if (line.StartsWith("using "))
                            {
                                continue;
                            }
                            if (line.StartsWith("//"))
                            {
                                continue;
                            }
                            if (line.StartsWith("{"))
                            {
                                continue;
                            }
                            if (line.EndsWith("}"))
                            {
                                continue;
                            }
                            isRawCommands = true;
                            break;
                        }
                    }
                    if (isRawCommands)
                    {
                        usingLines.AddRange(CommonUsing);
                        HashSet<string> usingset = new HashSet<string>(usingLines);
                        for (int i = 0; i < lines.Length; ++i)
                        { // find using.
                            var line = lines[i].Trim();
                            if (string.IsNullOrEmpty(line))
                            {
                                continue;
                            }
                            if (line.StartsWith("using "))
                            {
                                if (usingset.Add(line))
                                {
                                    usingLines.Add(line);
                                }
                                lines[i] = "";
                                continue;
                            }
                            break;
                        }
                        for (int i = 0; i < lines.Length; ++i)
                        { // find return.
                            var line = lines[i].Trim();
                            if (line.StartsWith("return "))
                            {
                                hasReturn = true;
                                break;
                            }
                        }
                    }
                    if (isRawCommands)
                    {
                        PlatDependant.WriteAllLines(src, usingLines);
                        using (var sw = PlatDependant.OpenAppendText(src))
                        {
                            sw.Write("class ");
                            sw.Write(dllname);
                            sw.WriteLine();
                            sw.WriteLine("{");
                            sw.Write("public static ");
                            if (hasReturn)
                            {
                                sw.Write("object ");
                            }
                            else
                            {
                                sw.Write("void ");
                            }
                            sw.Write("Main()");
                            sw.WriteLine();
                            sw.WriteLine("{");
                            sw.WriteLine(Command);
                            sw.WriteLine("}");
                            sw.WriteLine("}");
                        }
                    }
                    else
                    {
                        PlatDependant.WriteAllText(src, Command);
                    }

                    var compiler = new Microsoft.CSharp.CSharpCodeProvider();
                    var compilerOption = new System.CodeDom.Compiler.CompilerParameters();
                    compilerOption.IncludeDebugInformation = false;
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
                            compilerOption.ReferencedAssemblies.Add(loc);
                        }
                        catch (Exception e)
                        {
                            //PlatDependant.LogError(e);
                        }
                    }
                    var compileresult = compiler.CompileAssemblyFromFile(compilerOption, src);

                    //var builder = new UnityEditor.Compilation.AssemblyBuilder("EditorOutput/Intermediate/Temp.dll", "EditorOutput/Intermediate/Temp.cs");
                    //builder.Build();

                    Result = null;
                    if (compileresult.Errors.Count > 0)
                    {
                        foreach (var error in compileresult.Errors)
                        {
                            Debug.LogError(error);
                            if (Result == null)
                            {
                                Result = error.ToString();
                            }
                        }
                    }

                    if (Result == null)
                    {
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
                            var result = pluginEntry.Invoke(null, new object[0]);
                            if (result == null)
                            {
                                Result = "<No Result>";
                            }
                            else
                            {
                                Result = result.GetType().ToString() + ": " + result.ToString();
                            }
                        }
                        else
                        {
                            Result = "EntryPoint not found. EntryPoint is a public static method named Main() or you should just type raw commands";
                        }
                    }
                }
                finally
                {
                    PlatDependant.DeleteFile(src);
                    PlatDependant.DeleteFile(tar);
                }
            }
            ResultScroll = EditorGUILayout.BeginScrollView(ResultScroll);
            EditorGUILayout.TextArea(Result);
            EditorGUILayout.EndScrollView();
        }
    }
}