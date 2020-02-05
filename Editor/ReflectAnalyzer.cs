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
    public static class ReflectAnalyzer
    {
        public static List<string> ParseMemberList()
        {
            System.IO.Directory.CreateDirectory("EditorOutput/LuaPrecompile/TempAsms/iOS");
            var builtIOS = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings()
            {
                group = BuildTargetGroup.iOS, //BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                target = BuildTarget.iOS, //EditorUserBuildSettings.activeBuildTarget,
            }, "EditorOutput/LuaPrecompile/TempAsms/iOS");
            List<string> iOSMemberList = new List<string>();
            foreach (var asmfile in builtIOS.assemblies)
            {
                var path = "EditorOutput/LuaPrecompile/TempAsms/iOS/" + asmfile;
                ParseMemberList(iOSMemberList, path);
            }
            HashSet<string> iOSMemberSet = new HashSet<string>(iOSMemberList);

            System.IO.Directory.CreateDirectory("EditorOutput/LuaPrecompile/TempAsms/Android");
            var builtAndroid = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings()
            {
                group = BuildTargetGroup.Android, //BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                target = BuildTarget.Android, //EditorUserBuildSettings.activeBuildTarget,
            }, "EditorOutput/LuaPrecompile/TempAsms/Android");
            List<string> androidMemberList = new List<string>();
            foreach (var asmfile in builtAndroid.assemblies)
            {
                var path = "EditorOutput/LuaPrecompile/TempAsms/Android/" + asmfile;
                ParseMemberList(androidMemberList, path);
            }
            HashSet<string> androidMemberSet = new HashSet<string>(androidMemberList);
            
            iOSMemberSet.IntersectWith(androidMemberSet);

            List<string> fulllist = new List<string>();
            foreach (var line in iOSMemberList)
            {
                if (iOSMemberSet.Contains(line))
                {
                    fulllist.Add(line);
                }
            }
            var paths = UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyPaths(UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.SystemAssembly | UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.UnityEngine | UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.UserAssembly);
            foreach (var path in paths)
            {
                ParseMemberList(fulllist, path);
            }
            return fulllist;
        }

        public static List<string> ParseMemberList(string asmfile)
        {
            List<string> members = new List<string>();
            ParseMemberList(members, asmfile);
            return members;
        }
        private static void ParseMemberList(List<string> members, string asmfile)
        {
            if (System.IO.File.Exists(asmfile))
            {
                using (var asmdef = AssemblyDefinition.ReadAssembly(asmfile))
                {
                    foreach (var module in asmdef.Modules)
                    {
                        foreach (var type in module.Types)
                        {
                            ParseTypeMemberList(members, type);
                        }
                    }
                }
            }
        }
        private static void ParseTypeMemberList(List<string> members, TypeDefinition type)
        {
            if (!type.IsNestedPublic && !type.IsPublic)
            {
                return;
            }
            var line = "type " + type.GetIDString();
            members.Add(line);

            foreach (var prop in type.Properties)
            {
                bool isPublic = false;
                bool isStatic = false;
                if (prop.GetMethod != null)
                {
                    isStatic |= prop.GetMethod.IsStatic;
                    isPublic |= prop.GetMethod.IsPublic;
                }
                if (prop.SetMethod != null)
                {
                    isStatic |= prop.SetMethod.IsStatic;
                    isPublic |= prop.SetMethod.IsPublic;
                }
                if (!isPublic)
                {
                    continue;
                }
                var sb = new System.Text.StringBuilder();
                sb.Append("member prop ");
                sb.Append(isStatic ? "static " : "instance ");
                sb.Append(type.GetIDString());
                sb.Append(" ");
                sb.Append(prop.Name);
                members.Add(sb.ToString());
            }
            foreach (var field in type.Fields)
            {
                if (!field.IsPublic)
                {
                    continue;
                }
                bool isStatic = field.IsStatic;
                var sb = new System.Text.StringBuilder();
                sb.Append("member field ");
                sb.Append(isStatic ? "static " : "instance ");
                sb.Append(type.GetIDString());
                sb.Append(" ");
                sb.Append(field.Name);
                members.Add(sb.ToString());
            }
            foreach (var evt in type.Events)
            {
                bool isPublic = false;
                bool isStatic = false;
                if (evt.AddMethod != null)
                {
                    isStatic |= evt.AddMethod.IsStatic;
                    isPublic |= evt.AddMethod.IsPublic;
                }
                if (evt.RemoveMethod != null)
                {
                    isStatic |= evt.RemoveMethod.IsStatic;
                    isPublic |= evt.RemoveMethod.IsPublic;
                }
                if (!isPublic)
                {
                    continue;
                }
                var sb = new System.Text.StringBuilder();
                sb.Append("member event ");
                sb.Append(isStatic ? "static " : "instance ");
                sb.Append(type.GetIDString());
                sb.Append(" ");
                sb.Append(evt.Name);
                members.Add(sb.ToString());
            }
            foreach (var method in type.Methods)
            {
                if (!method.IsPublic)
                {
                    continue;
                }
                bool isStatic = method.IsStatic;
                var sb = new System.Text.StringBuilder();
                sb.Append("member ");
                if (method.IsConstructor)
                {
                    sb.Append("ctor ");
                    isStatic = true;
                }
                else
                {
                    sb.Append("func ");
                }
                sb.Append(isStatic ? "static " : "instance ");
                sb.Append(type.GetIDString());
                sb.Append(" ");
                sb.Append(method.GetIDString());
                members.Add(sb.ToString());
            }
            foreach (var ntype in type.NestedTypes)
            {
                if (!ntype.IsNestedPublic && !ntype.IsPublic)
                {
                    continue;
                }
                var sb = new System.Text.StringBuilder();
                sb.Append("member type static ");
                sb.Append(type.GetIDString());
                sb.Append(" ");
                sb.Append(ntype.GetIDString());
                members.Add(sb.ToString());
            }

            foreach (var ntype in type.NestedTypes)
            {
                ParseTypeMemberList(members, ntype);
            }
        }
        private static string GetIDString(this TypeDefinition type)
        {
            return type.FullName.Replace('/', '+');
        }
        private static string GetIDString(this TypeReference type)
        {
            if (type.IsArray)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(type.GetElementType().GetIDString());
                sb.Append("[");
                var atype = type as ArrayType;
                if (atype != null)
                {
                    sb.Append(',', atype.Rank - 1);
                }
                sb.Append("]");
                return sb.ToString(); 
            }
            else if (type.IsGenericParameter)
            {
                return type.Name;
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(type.FullName.Replace('/', '+'));
                var gpars = type.GenericParameters;
                if (gpars != null && gpars.Count > 0)
                {
                    sb.Append("<");
                    int parcnt = 0;
                    foreach (var gpar in gpars)
                    {
                        if (++parcnt != 1)
                        {
                            sb.Append(",");
                        }
                        sb.Append(gpar.GetIDString());
                    }
                    sb.Append(">");
                }
                return sb.ToString();
            }
        }
        private static string GetIDString(this MethodDefinition method)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(method.Name);
            var gpars = method.GenericParameters;
            if (gpars != null && gpars.Count > 0)
            {
                sb.Append("<");
                int gparcnt = 0;
                foreach (var gpar in gpars)
                {
                    if (++gparcnt != 1)
                    {
                        sb.Append(",");
                    }
                    sb.Append(gpar.GetIDString());
                }
                sb.Append(">");
            }
            sb.Append("(");
            int parcnt = 0;
            foreach (var par in method.Parameters)
            {
                if (++parcnt != 1)
                {
                    sb.Append(",");
                }
                sb.Append(par.ParameterType.GetIDString());
            }
            sb.Append(")");
            // may be unnecessary
            //if (!method.IsConstructor)
            //{
            //    sb.Append(method.ReturnType.GetNameString());
            //}
            return sb.ToString();
        }

        public static string GetNameString(this Type type)
        {
            if (type == null)
            {
                return null;
            }
            if (type.IsNested)
            {
                return type.DeclaringType.GetNameString() + "+" + type.Name;
            }
            else
            {
                return type.Namespace + "." + type.Name;
            }
        }
        public static string GetIDString(this MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }
            else if (member is Type)
            {
                var type = member as Type;
                if (type.IsArray)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(type.GetElementType().GetIDString());
                    sb.Append("[");
                    sb.Append(',', type.GetArrayRank() - 1);
                    sb.Append("]");
                    return sb.ToString();
                }
                else if (type.IsPointer)
                {
                    return type.GetElementType().GetIDString() + "*";
                }
                else if (type.IsByRef)
                {
                    return type.GetElementType().GetIDString() + "&";
                }
                else if (type.IsGenericParameter)
                {
                    return type.Name;
                }
                else if (type.IsGenericTypeDefinition)
                {
                    return type.GetNameString();
                }
                else if (type.IsGenericType)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(type.GetGenericTypeDefinition().GetIDString());
                    var gpars = type.GetGenericArguments();
                    if (gpars != null && gpars.Length > 0)
                    {
                        sb.Append("<");
                        int parcnt = 0;
                        foreach (var gpar in gpars)
                        {
                            if (++parcnt != 1)
                            {
                                sb.Append(",");
                            }
                            sb.Append(gpar.GetIDString());
                        }
                        sb.Append(">");
                    }
                    return sb.ToString();
                }
                else
                {
                    return type.GetNameString();
                }
            }
            else if (member is MethodBase)
            {
                var method = member as MethodBase;
                var sb = new System.Text.StringBuilder();
                sb.Append(method.Name);
                if (!method.IsConstructor)
                {
                    var gpars = method.GetGenericArguments();
                    if (gpars != null && gpars.Length > 0)
                    {
                        sb.Append("<");
                        int gparcnt = 0;
                        foreach (var gpar in gpars)
                        {
                            if (++gparcnt != 1)
                            {
                                sb.Append(",");
                            }
                            sb.Append(gpar.GetIDString());
                        }
                        sb.Append(">");
                    }
                }
                sb.Append("(");
                int parcnt = 0;
                foreach (var par in method.GetParameters())
                {
                    if (++parcnt != 1)
                    {
                        sb.Append(",");
                    }
                    sb.Append(par.ParameterType.GetIDString());
                }
                sb.Append(")");
                // may be unnecessary
                //if (!method.IsConstructor)
                //{
                //    sb.Append(method.ReturnType.GetNameString());
                //}
                return sb.ToString();
            }
            else
            {
                return member.Name;
            }
        }
    }
}
