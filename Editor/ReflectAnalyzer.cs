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
            var list = ParseMemberList_AssemblyDefinition();
            var uniqueset = new HashSet<string>(list);

            // inherited member check.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in asm.GetTypes()) // It seems that the GetTypes returns nested types.
                {
                    var typestr = type.GetIDString();
                    if (uniqueset.Contains("type " + typestr))
                    {
                        foreach (var member in type.GetMembers())
                        {
                            var dtype = member.DeclaringType;
                            var rtype = member.ReflectedType;
                            if (dtype != rtype)
                            {
                                // this means it is inherited.
                                bool isStatic = false;
                                string membertype = null;
                                var defmember = member;
                                if (member is PropertyInfo)
                                {
                                    membertype = "prop ";
                                    var pinfo = member as PropertyInfo;
                                    var gmethod = pinfo.GetGetMethod();
                                    if (gmethod != null)
                                    {
                                        isStatic = gmethod.IsStatic;
                                    }
                                    else
                                    {
                                        var smethod = pinfo.GetSetMethod();
                                        if (smethod != null)
                                        {
                                            isStatic = smethod.IsStatic;
                                        }
                                    }
                                }
                                else if (member is FieldInfo)
                                {
                                    membertype = "field ";
                                    var finfo = member as FieldInfo;
                                    isStatic = finfo.IsStatic;
                                }
                                else if (member is EventInfo)
                                {
                                    membertype = "event ";
                                    var einfo = member as EventInfo;
                                    var amethod = einfo.GetAddMethod();
                                    if (amethod != null)
                                    {
                                        isStatic = amethod.IsStatic;
                                    }
                                    else
                                    {
                                        var rmethod = einfo.GetRemoveMethod();
                                        if (rmethod != null)
                                        {
                                            isStatic = rmethod.IsStatic;
                                        }
                                    }
                                }
                                else if (member is Type)
                                {
                                    membertype = "type ";
                                    isStatic = true;
                                }
                                else if (member is ConstructorInfo)
                                {
                                    membertype = "ctor ";
                                    isStatic = true;
                                }
                                else if (member is MethodInfo)
                                {
                                    membertype = "func ";
                                    var minfo = member as MethodInfo;
                                    defmember = GetMethodDefinition(minfo);
                                    isStatic = minfo.IsStatic;
                                }

                                if (membertype != null && !isStatic && defmember != null)
                                {
                                    var sb = new System.Text.StringBuilder();
                                    sb.Append("member ");
                                    sb.Append(membertype);
                                    sb.Append("instance ");
                                    sb.Append(defmember.DeclaringType.GetIDString());
                                    sb.Append(" ");
                                    sb.Append(defmember.GetIDString());

                                    var dsig = sb.ToString();
                                    if (uniqueset.Contains(dsig))
                                    {
                                        sb.Clear();
                                        sb.Append("member ");
                                        sb.Append(membertype);
                                        sb.Append("instance ");
                                        sb.Append(rtype.GetIDString());
                                        sb.Append(" ");
                                        sb.Append(member.GetIDString());
                                        list.Add(sb.ToString());
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return list;
        }
        public static MethodInfo GetMethodDefinition(MethodInfo minfo)
        {
            var dtype = minfo.DeclaringType;

            if (dtype.IsGenericType)
            {
                var ddtype = dtype.GetGenericTypeDefinition();
                if (ddtype == dtype)
                {
                    return minfo;
                }
                else
                {
                    Dictionary<Type, Type> replacement = new Dictionary<Type, Type>();
                    var ddargs = ddtype.GetGenericArguments();
                    var dargs = dtype.GetGenericArguments();
                    for (int i = 0; i < dargs.Length && i < ddargs.Length; ++i)
                    {
                        replacement[ddargs[i]] = dargs[i];
                    }
                    var rsig = GetIDString(minfo);
                    foreach (var method in ddtype.GetMethods())
                    {
                        if (method.GetIDStringWithReplacement(replacement) == rsig)
                        {
                            return method;
                        }
                    }
                    Debug.LogError("Cannot find definition for " + dtype + "::" + minfo);
                    return null; // what the hell? not found?
                }
            }
            else
            {
                return minfo;
            }
        }

        public static List<string> ParseMemberList_AssemblyDefinition()
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

        public static List<string> ParseMemberList_AppDomain()
        {
            System.IO.Directory.CreateDirectory("EditorOutput/LuaPrecompile/TempAsms/iOS");
            var builtIOS = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings()
            {
                group = BuildTargetGroup.iOS, //BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                target = BuildTarget.iOS, //EditorUserBuildSettings.activeBuildTarget,
            }, "EditorOutput/LuaPrecompile/TempAsms/iOS");
            AppDomain iOSDomain = AppDomain.CreateDomain("iOSReflectAnalyze");
            var paths = UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyPaths(UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.SystemAssembly | UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.UnityEngine | UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.UserAssembly);
            foreach (var path in paths)
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(path);
                    iOSDomain.Load(bytes);
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to load " + path + " in iOSDomain");
                    // TODO: when we can not load a assembly, we could find the same one in CurrentDomain and load this existing one.
                    Debug.LogError(e);
                }
            }
            foreach (var asmfile in builtIOS.assemblies)
            {
                var path = "EditorOutput/LuaPrecompile/TempAsms/iOS/" + asmfile;
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(path);
                    iOSDomain.Load(bytes);
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to load " + path + " in iOSDomain");
                    Debug.LogError(e);
                }
            }
            List<string> iOSMemberList = ParseMemberListInAppDomain(iOSDomain);
            HashSet<string> iOSMemberSet = new HashSet<string>(iOSMemberList);
            AppDomain.Unload(iOSDomain);

            System.IO.Directory.CreateDirectory("EditorOutput/LuaPrecompile/TempAsms/Android");
            var builtAndroid = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(new UnityEditor.Build.Player.ScriptCompilationSettings()
            {
                group = BuildTargetGroup.Android, //BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                target = BuildTarget.Android, //EditorUserBuildSettings.activeBuildTarget,
            }, "EditorOutput/LuaPrecompile/TempAsms/Android");
            AppDomain androidDomain = AppDomain.CreateDomain("AndroidReflectAnalyze");
            foreach (var path in paths)
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(path);
                    androidDomain.Load(bytes);
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to load " + path + " in androidDomain");
                    Debug.LogError(e);
                }
            }
            foreach (var asmfile in builtAndroid.assemblies)
            {
                var path = "EditorOutput/LuaPrecompile/TempAsms/Android/" + asmfile;
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(path);
                    androidDomain.Load(bytes);
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to load " + path + " in androidDomain");
                    Debug.LogError(e);
                }
            }
            List<string> androidMemberList = ParseMemberListInAppDomain(androidDomain);
            HashSet<string> androidMemberSet = new HashSet<string>(androidMemberList);
            AppDomain.Unload(androidDomain);

            iOSMemberSet.IntersectWith(androidMemberSet);

            List<string> fulllist = new List<string>();
            foreach (var line in iOSMemberList)
            {
                if (iOSMemberSet.Contains(line))
                {
                    fulllist.Add(line);
                }
            }
            return fulllist;
        }
        private static List<string> ParseMemberListInAppDomain(AppDomain appdomain)
        {
            List<string> memberlist = new List<string>();
            foreach (var asm in appdomain.GetAssemblies())
            {
                foreach (var type in asm.GetTypes()) // It seems that the GetTypes returns nested types.
                {
                    memberlist.Add("type " + GetIDString(type));
                    foreach (var member in type.GetMembers())
                    {
                        bool isStatic = false;
                        string membertype = null;
                        if (member is PropertyInfo)
                        {
                            membertype = "prop ";
                            var pinfo = member as PropertyInfo;
                            var gmethod = pinfo.GetGetMethod();
                            if (gmethod != null)
                            {
                                isStatic = gmethod.IsStatic;
                            }
                            else
                            {
                                var smethod = pinfo.GetSetMethod();
                                if (smethod != null)
                                {
                                    isStatic = smethod.IsStatic;
                                }
                            }
                        }
                        else if (member is FieldInfo)
                        {
                            membertype = "field ";
                            var finfo = member as FieldInfo;
                            isStatic = finfo.IsStatic;
                        }
                        else if (member is EventInfo)
                        {
                            membertype = "event ";
                            var einfo = member as EventInfo;
                            var amethod = einfo.GetAddMethod();
                            if (amethod != null)
                            {
                                isStatic = amethod.IsStatic;
                            }
                            else
                            {
                                var rmethod = einfo.GetRemoveMethod();
                                if (rmethod != null)
                                {
                                    isStatic = rmethod.IsStatic;
                                }
                            }
                        }
                        else if (member is Type)
                        {
                            membertype = "type ";
                            isStatic = true;
                        }
                        else if (member is ConstructorInfo)
                        {
                            membertype = "ctor ";
                            isStatic = true;
                        }
                        else if (member is MethodBase)
                        {
                            membertype = "func ";
                            var minfo = member as MethodBase;
                            isStatic = minfo.IsStatic;
                        }

                        if (membertype != null)
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.Append("member ");
                            sb.Append(membertype);
                            sb.Append(isStatic ? "static " : "instance ");
                            sb.Append(type.GetIDString());
                            sb.Append(" ");
                            sb.Append(member.GetIDString());
                            memberlist.Add(sb.ToString());
                        }
                    }
                }
            }
            return memberlist;
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
                if (string.IsNullOrEmpty(type.Namespace))
                {
                    return type.Name;
                }
                else
                {
                    return type.Namespace + "." + type.Name;
                }
            }
        }
        private static string GetIDStringWithReplacement(this Type type, Dictionary<Type, Type> replacement)
        {
            if (type.IsArray)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(type.GetElementType().GetIDStringWithReplacement(replacement));
                sb.Append("[");
                sb.Append(',', type.GetArrayRank() - 1);
                sb.Append("]");
                return sb.ToString();
            }
            else if (type.IsPointer)
            {
                return type.GetElementType().GetIDStringWithReplacement(replacement) + "*";
            }
            else if (type.IsByRef)
            {
                return type.GetElementType().GetIDStringWithReplacement(replacement) + "&";
            }
            else if (type.IsGenericParameter)
            {
                Type rep;
                if (replacement != null && replacement.TryGetValue(type, out rep) && rep != null && rep != type)
                {
                    return rep.GetIDStringWithReplacement(replacement);
                }
                else
                {
                    return type.Name;
                }
            }
            else if (type.IsGenericTypeDefinition)
            {
                Type rep;
                if (replacement != null && replacement.TryGetValue(type, out rep) && rep != null && rep != type)
                {
                    return rep.GetIDStringWithReplacement(replacement);
                }
            }
            if (type.IsGenericType)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(type.GetNameString());
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
                        sb.Append(gpar.GetIDStringWithReplacement(replacement));
                    }
                    sb.Append(">");
                }
                return sb.ToString();
            }
            else
            {
                Type rep;
                if (replacement != null && replacement.TryGetValue(type, out rep) && rep != null && rep != type)
                {
                    return rep.GetIDStringWithReplacement(replacement);
                }
                else
                {
                    return type.GetNameString();
                }
            }
        }
        private static string GetIDStringWithReplacement(this MethodInfo method, Dictionary<Type, Type> replacement)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(method.Name);
            //if (!method.IsConstructor)
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
                        sb.Append(gpar.GetIDStringWithReplacement(replacement));
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
                sb.Append(par.ParameterType.GetIDStringWithReplacement(replacement));
            }
            sb.Append(")");
            // may be unnecessary
            //if (!method.IsConstructor)
            //{
            //    sb.Append(method.ReturnType.GetNameString());
            //}
            return sb.ToString();
        }
        public static string GetNameString(this Type type, bool canIgnoreGenericArgInGenericDef)
        {
            if (type.IsArray)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(type.GetElementType().GetNameString(canIgnoreGenericArgInGenericDef));
                sb.Append("[");
                sb.Append(',', type.GetArrayRank() - 1);
                sb.Append("]");
                return sb.ToString();
            }
            else if (type.IsPointer)
            {
                return type.GetElementType().GetNameString(canIgnoreGenericArgInGenericDef) + "*";
            }
            else if (type.IsByRef)
            {
                return type.GetElementType().GetNameString(canIgnoreGenericArgInGenericDef) + "&";
            }
            else if (type.IsGenericParameter)
            {
                return type.Name;
            }
            else if (type.IsGenericTypeDefinition && canIgnoreGenericArgInGenericDef)
            {
                return type.GetNameString();
            }
            else if (type.IsGenericType)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(type.GetNameString());
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
                        sb.Append(gpar.GetNameString(false));
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
        public static string GetIDString(this MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }
            else if (member is Type)
            {
                var type = member as Type;
                return type.GetNameString(true);
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
                            sb.Append(gpar.GetNameString(false));
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
                    sb.Append(par.ParameterType.GetNameString(false));
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
