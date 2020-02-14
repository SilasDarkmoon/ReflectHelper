using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;
using Mono.Cecil;
using Mono.Cecil.Cil;

using Object = UnityEngine.Object;

namespace Capstones.UnityEditorEx
{
    public static class LuaHotFixCodeInjector
    {
        #region Load or Unload Assemblies
        private class LoadedAssembly : IDisposable
        {
            public AssemblyDefinition Asm;
            public string Path;
            public bool Dirty;

            public LoadedAssembly(string path)
            {
                Path = path;
                Load();
            }

            public void Load()
            {
                if (!string.IsNullOrEmpty(Path))
                {
                    if (System.IO.File.Exists(Path))
                    {
                        Asm = AssemblyDefinition.ReadAssembly(Path);
                    }
                }
            }
            public void Dispose()
            {
                if (Asm != null)
                {
                    if (Dirty)
                    {
                        Asm.Write(Path + ".tmp");
                    }
                    Asm.Dispose();
                    Asm = null;
                    if (Dirty)
                    {
                        System.IO.File.Delete(Path);
                        System.IO.File.Move(Path + ".tmp", Path);
                    }
                    Dirty = false;
                }
            }
        }
        private static Dictionary<string, LoadedAssembly> _LoadedAsms = new Dictionary<string, LoadedAssembly>();

        private static string _AssembliesDirectory;
        public static string AssembliesDirectory
        {
            get { return _AssembliesDirectory ?? "Library/ScriptAssemblies/"; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _AssembliesDirectory = null;
                }
                else
                {
                    if (!value.EndsWith("/") && !value.EndsWith("\\"))
                    {
                        value += "/";
                    }
                    _AssembliesDirectory = value;
                }
            }
        }

        internal static AssemblyDefinition GetAssembly(string name)
        {
            if (name.EndsWith(".dll"))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }
            LoadedAssembly asminfo;
            if (_LoadedAsms.TryGetValue(name, out asminfo))
            {
                return asminfo.Asm;
            }
            return null;
        }
        internal static AssemblyDefinition GetOrLoadAssembly(string name)
        {
            var path = name;
            if (name.EndsWith(".dll"))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }
            LoadedAssembly asminfo;
            if (_LoadedAsms.TryGetValue(name, out asminfo))
            {
                return asminfo.Asm;
            }
            return LoadAssembly(path);
        }
        public static void TryLoadAssembly(string name)
        {
            GetOrLoadAssembly(name);
        }
        private static AssemblyDefinition LoadAssembly(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                string path;
                if (name.EndsWith(".dll"))
                { // surpose this is a full path.
                    path = name;
                    name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (System.IO.Path.GetFileName(path) == path)
                    { // this mean we only get a file name, need folder.
                        path = AssembliesDirectory + path;
                    }
                }
                else
                {
                    path = AssembliesDirectory + name + ".dll";
                }
                var asminfo = _LoadedAsms[name] = new LoadedAssembly(path);
                return asminfo.Asm;
            }
            return null;
        }
        public static void LoadAssemblies()
        {
            var files = System.IO.Directory.GetFiles(AssembliesDirectory);
            for (int i = 0; i < files.Length; ++i)
            {
                var file = files[i];
                if (file.EndsWith(".dll"))
                {
                    LoadAssembly(System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }
        }
        public static void UnloadAssemblies()
        {
            foreach (var kvp in _LoadedAsms)
            {
                kvp.Value.Dispose();
            }
            _LoadedAsms.Clear();
        }
        public static void MarkDirty(string name)
        {
            if (name.EndsWith(".dll"))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }
            LoadedAssembly asminfo;
            if (_LoadedAsms.TryGetValue(name, out asminfo))
            {
                asminfo.Dirty = true;
            }
        }
        #endregion

        #region Mono.Cecil Extensions
        internal static MethodReference GetReference(this MethodDefinition method, GenericInstanceType type)
        {
            MethodReference mref = new MethodReference(method.Name, method.ReturnType, type);
            foreach (var par in method.Parameters)
            {
                mref.Parameters.Add(par);
            }
            if (!method.IsStatic)
            {
                mref.HasThis = true;
            }
            return mref;
        }
        internal static MethodReference GetReference(this MethodDefinition method)
        {
            MethodReference mref = new MethodReference(method.Name, method.ReturnType, method.DeclaringType);
            foreach (var par in method.Parameters)
            {
                mref.Parameters.Add(par);
            }
            if (!method.IsStatic)
            {
                mref.HasThis = true;
            }
            return mref;
        }
        internal static MethodReference GetReference(this MethodDefinition method, GenericInstanceType type, ModuleDefinition inModule)
        {
            MethodReference mref = inModule.ImportReference(method);
            mref.DeclaringType = inModule.ImportReference(type);
            if (!method.IsStatic)
            {
                mref.HasThis = true;
            }
            return mref;
        }
        internal static MethodReference GetReference(this MethodDefinition method, ModuleDefinition inModule)
        {
            MethodReference mref = inModule.ImportReference(method);
            if (!method.IsStatic)
            {
                mref.HasThis = true;
            }
            return mref;
        }
        internal static List<MethodDefinition> GetMethods(this TypeDefinition type, string name)
        {
            List<MethodDefinition> list = new List<MethodDefinition>();
            foreach (var method in type.Methods)
            {
                if (method.Name == name)
                {
                    list.Add(method);
                }
            }
            return list;
        }
        internal static MethodDefinition GetMethod(this TypeDefinition type, string name)
        {
            var methods = GetMethods(type, name);
            if (methods.Count > 0)
            {
                return methods[0];
            }
            return null;
        }
        internal static MethodDefinition GetMethod(this TypeDefinition type, string name, int paramCnt)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name == name && method.Parameters.Count == paramCnt)
                {
                    return method;
                }
            }
            return null;
        }
        internal static MethodDefinition GetMethod(this TypeDefinition type, string name, params TypeReference[] pars)
        {
            pars = pars ?? new TypeReference[0];
            foreach (var method in type.Methods)
            {
                if (method.Name == name)
                {
                    if (method.Parameters.Count == pars.Length)
                    {
                        bool match = true;
                        for (int i = 0; i < pars.Length; ++i)
                        {
                            if (pars[i] != method.Parameters[i].ParameterType)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            return method;
                        }
                    }
                }
            }
            return null;
        }
        internal static FieldDefinition GetField(this TypeDefinition type, string name)
        {
            foreach (var field in type.Fields)
            {
                if (field.Name == name)
                {
                    return field;
                }
            }
            return null;
        }
        internal static PropertyDefinition GetProperty(this TypeDefinition type, string name)
        {
            foreach (var prop in type.Properties)
            {
                if (prop.Name == name)
                {
                    return prop;
                }
            }
            return null;
        }
        internal static void AddRange<T>(this Mono.Collections.Generic.Collection<T> collection, IEnumerable<T> values)
        {
            foreach (var val in values)
            {
                collection.Add(val);
            }
        }
        internal static void AddRange<T>(this Mono.Collections.Generic.Collection<T> collection, params T[] values)
        {
            AddRange(collection, (IEnumerable<T>)values);
        }
        internal static void InsertRange<T>(this Mono.Collections.Generic.Collection<T> collection, int index, IEnumerable<T> values)
        {
            foreach (var val in values)
            {
                collection.Insert(index++, val);
            }
        }
        internal static void InsertRange<T>(this Mono.Collections.Generic.Collection<T> collection, int index, params T[] values)
        {
            InsertRange(collection, index, (IEnumerable<T>)values);
        }
        #endregion

        public static void GenerateByRefUtils()
        {
            var compiler = new Microsoft.CSharp.CSharpCodeProvider();
            var compilerOption = new System.CodeDom.Compiler.CompilerParameters();
            var root = CapsModEditor.GetPackageOrModRoot(CapsEditorUtils.__MOD__);
            var src = root + "/ByRefUtils~/ByRefUtils.cs";
            var tar = root + "/ByRefUtils~/ByRefUtils.dll";
            compilerOption.OutputAssembly = tar;
            var compileresult = compiler.CompileAssemblyFromFile(compilerOption, src);

            if (compileresult.Errors.Count > 0)
            {
                foreach (var error in compileresult.Errors)
                {
                    Debug.LogError(error);
                }
                return;
            }

            var asm = LoadAssembly(tar);
            var asminfo = _LoadedAsms["ByRefUtils"];

            var type = asm.MainModule.GetType("Capstones.UnityEngineEx.ByRefUtils");
            TypeDefinition reftype = null;
            foreach (var ntype in type.NestedTypes)
            {
                if (ntype.Name == "Ref`1")
                {
                    reftype = ntype;
                    break;
                }
            }
            var field = new FieldDefinition("_Ref", FieldAttributes.Private, asm.MainModule.TypeSystem.IntPtr);
            reftype.Fields.Add(field);
            var selfreftype = new GenericInstanceType(reftype);
            selfreftype.GenericArguments.Add(reftype.GenericParameters[0]);

            {
                var method = reftype.GetMethod("GetRef");
                for (int i = method.Body.Instructions.Count - 1; i >= 0; --i)
                {
                    method.Body.Instructions.RemoveAt(i);
                }

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldfld, field);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = reftype.GetMethod("SetRef");
                for (int i = method.Body.Instructions.Count - 1; i >= 0; --i)
                {
                    method.Body.Instructions.RemoveAt(i);
                }

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldarg_1);
                emitter.Emit(OpCodes.Stfld, field);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = type.GetMethod("RefEquals");
                for (int i = method.Body.Instructions.Count - 1; i >= 0; --i)
                {
                    method.Body.Instructions.RemoveAt(i);
                }

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldarg_1);
                emitter.Emit(OpCodes.Ceq);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = type.GetMethod("GetEmptyRef");
                for (int i = method.Body.Instructions.Count - 1; i >= 0; --i)
                {
                    method.Body.Instructions.RemoveAt(i);
                }

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldnull);
                emitter.Emit(OpCodes.Ret);
            }

            asminfo.Dirty = true;
            asminfo.Dispose();
            _LoadedAsms.Remove("ByRefUtils");
        }

        internal static TypeDefinition GetLuaPack(int paramCnt)
        {
            if (paramCnt < 0)
            {
                return null;
            }
            else
            {
                var luadll = GetOrLoadAssembly("CapsLua");
                var typename = "Capstones.LuaWrap.LuaPack";
                if (paramCnt > 0)
                {
                    typename += "`" + paramCnt;
                }
                var existing = luadll.MainModule.GetType(typename);
                if (existing == null)
                {
                    var objectType = luadll.MainModule.TypeSystem.Object;
                    var valueType = new TypeReference("System", "ValueType", luadll.MainModule, objectType.Scope);
                    var voidType = luadll.MainModule.TypeSystem.Void;
                    var intType = luadll.MainModule.TypeSystem.Int32;
                    var intPtrType = luadll.MainModule.TypeSystem.IntPtr;
                    var injecttype = new TypeDefinition("Capstones.LuaWrap", "LuaPack`" + paramCnt, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, valueType);
                    injecttype.Interfaces.Add(new InterfaceImplementation(luadll.MainModule.GetType("Capstones.LuaWrap.ILuaPack")));
                    { // attributes
                        var defaultMemberCtor = new MethodReference(".ctor", voidType, new TypeReference("System.Reflection", "DefaultMemberAttribute", luadll.MainModule, objectType.Scope)) { HasThis = true };
                        defaultMemberCtor.Parameters.Add(new ParameterDefinition(luadll.MainModule.TypeSystem.String));
                        var defaultMemberAttr = new CustomAttribute(defaultMemberCtor);
                        defaultMemberAttr.ConstructorArguments.Add(new CustomAttributeArgument(luadll.MainModule.TypeSystem.String, "Item"));
                        injecttype.CustomAttributes.Add(defaultMemberAttr);
                    }
                    for (int i = 0; i < paramCnt; ++i)
                    {
                        injecttype.GenericParameters.Add(new GenericParameter("T" + i, injecttype));
                        injecttype.Fields.Add(new FieldDefinition("t" + i, FieldAttributes.Public, injecttype.GenericParameters[i]));
                    }
                    luadll.MainModule.Types.Add(injecttype);

                    var selfType = new GenericInstanceType(injecttype);
                    for (int i = 0; i < paramCnt; ++i)
                    {
                        selfType.GenericArguments.Add(injecttype.GenericParameters[i]);
                    }
                    var indexerListDefType = luadll.MainModule.GetType("Capstones.LuaWrap.LuaPackIndexAccessorList`1");
                    GenericInstanceType indexerListType = new GenericInstanceType(indexerListDefType);
                    indexerListType.GenericArguments.Add(selfType);
                    { // _IndexAccessors
                        var iafield = new FieldDefinition("_IndexAccessors", FieldAttributes.Private | FieldAttributes.Static, indexerListType);
                        injecttype.Fields.Add(iafield);
                    }
                    GenericInstanceType indexerRef;
                    { // Intermediate class for _IndexAccessors
                        var iantype = new TypeDefinition("", "<>indexer", TypeAttributes.NestedPrivate | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed | TypeAttributes.Abstract, objectType);
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            iantype.GenericParameters.Add(new GenericParameter("T" + i, iantype));
                        }
                        injecttype.NestedTypes.Add(iantype);
                        var parentRef = new GenericInstanceType(injecttype);
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            parentRef.GenericArguments.Add(iantype.GenericParameters[i]);
                        }
                        { // Intermediate class Methods
                            for (int i = 0; i < paramCnt; ++i)
                            {
                                { // getter
                                    var getter = new MethodDefinition("getter" + i, MethodAttributes.Static | MethodAttributes.Assembly | MethodAttributes.HideBySig, objectType);
                                    getter.Parameters.Add(new ParameterDefinition("thiz", ParameterAttributes.None, new ByReferenceType(parentRef)));
                                    var emitter = getter.Body.GetILProcessor();
                                    emitter.Emit(OpCodes.Ldarg_0);
                                    emitter.Emit(OpCodes.Ldfld, new FieldReference("t" + i, iantype.GenericParameters[i], parentRef));
                                    emitter.Emit(OpCodes.Box, iantype.GenericParameters[i]);
                                    emitter.Emit(OpCodes.Ret);
                                    iantype.Methods.Add(getter);
                                }
                                { // setter
                                    var setter = new MethodDefinition("setter" + i, MethodAttributes.Static | MethodAttributes.Assembly | MethodAttributes.HideBySig, voidType);
                                    setter.Parameters.Add(new ParameterDefinition("thiz", ParameterAttributes.None, new ByReferenceType(parentRef)));
                                    setter.Parameters.Add(new ParameterDefinition("val", ParameterAttributes.None, objectType));
                                    var emitter = setter.Body.GetILProcessor();
                                    emitter.Emit(OpCodes.Ldarg_0);
                                    emitter.Emit(OpCodes.Ldarg_1);
                                    emitter.Emit(OpCodes.Unbox_Any, iantype.GenericParameters[i]);
                                    emitter.Emit(OpCodes.Stfld, new FieldReference("t" + i, iantype.GenericParameters[i], parentRef));
                                    emitter.Emit(OpCodes.Ret);
                                    iantype.Methods.Add(setter);
                                }
                            }
                        }
                        indexerRef = new GenericInstanceType(iantype);
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            indexerRef.GenericArguments.Add(injecttype.GenericParameters[i]);
                        }
                    }
                    var accessorDefType = luadll.MainModule.GetType("Capstones.LuaWrap.LuaPackIndexAccessor`1");
                    { // .cctor
                        TypeDefinition getterDefType = null;
                        TypeDefinition setterDefType = null;
                        foreach (var ntdef in accessorDefType.NestedTypes)
                        {
                            if (ntdef.Name == "DelGetter")
                            {
                                getterDefType = ntdef;
                            }
                            else if (ntdef.Name == "DelSetter")
                            {
                                setterDefType = ntdef;
                            }
                        }
                        var getterType = new GenericInstanceType(getterDefType);
                        getterType.GenericArguments.Add(selfType);
                        var setterType = new GenericInstanceType(setterDefType);
                        setterType.GenericArguments.Add(selfType);
                        var getterCtor = new MethodReference(".ctor", voidType, getterType) { HasThis = true };
                        getterCtor.Parameters.Add(getterDefType.Methods[0].Parameters[0]);
                        getterCtor.Parameters.Add(getterDefType.Methods[0].Parameters[1]);
                        var setterCtor = new MethodReference(".ctor", voidType, setterType) { HasThis = true };
                        setterCtor.Parameters.Add(setterDefType.Methods[0].Parameters[0]);
                        setterCtor.Parameters.Add(setterDefType.Methods[0].Parameters[1]);
                        var addMethod = new MethodReference("Add", voidType, indexerListType) { HasThis = true };
                        var addGetterType = new GenericInstanceType(getterDefType);
                        addGetterType.GenericArguments.Add(indexerListType.ElementType.GenericParameters[0]);
                        addMethod.Parameters.Add(new ParameterDefinition(addGetterType));
                        var addSetterType = new GenericInstanceType(setterDefType);
                        addSetterType.GenericArguments.Add(indexerListType.ElementType.GenericParameters[0]);
                        addMethod.Parameters.Add(new ParameterDefinition(addSetterType));

                        var cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static, voidType);
                        var emitter = cctor.Body.GetILProcessor();
                        emitter.Emit(OpCodes.Newobj, new MethodReference(".ctor", voidType, indexerListType) { HasThis = true });

                        for (int i = 0; i < paramCnt; ++i)
                        {
                            emitter.Emit(OpCodes.Dup);
                            emitter.Emit(OpCodes.Ldnull);
                            var getterMethod = new MethodReference("getter" + i, objectType, indexerRef) { HasThis = false };
                            getterMethod.Parameters.Add(new ParameterDefinition(new ByReferenceType(selfType)));
                            emitter.Emit(OpCodes.Ldftn, getterMethod);
                            emitter.Emit(OpCodes.Newobj, getterCtor);
                            emitter.Emit(OpCodes.Ldnull);
                            var setterMethod = new MethodReference("setter" + i, voidType, indexerRef) { HasThis = false };
                            setterMethod.Parameters.Add(new ParameterDefinition(new ByReferenceType(selfType)));
                            setterMethod.Parameters.Add(new ParameterDefinition(objectType));
                            emitter.Emit(OpCodes.Ldftn, setterMethod);
                            emitter.Emit(OpCodes.Newobj, setterCtor);
                            emitter.Emit(OpCodes.Callvirt, addMethod);
                        }
                        emitter.Emit(OpCodes.Stsfld, new FieldReference("_IndexAccessors", indexerListType, selfType));
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Methods.Add(cctor);
                    }
                    { // ctor
                        var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, voidType);
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            ctor.Parameters.Add(new ParameterDefinition("p" + i, ParameterAttributes.None, injecttype.GenericParameters[i]));
                        }
                        var emitter = ctor.Body.GetILProcessor();
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            emitter.Emit(OpCodes.Ldarg_0);
                            emitter.Emit(OpCodes.Ldarg_S, (byte)(i + 1));
                            emitter.Emit(OpCodes.Stfld, new FieldReference("t" + i, injecttype.GenericParameters[i], selfType));
                        }
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Methods.Add(ctor);
                    }
                    { // Deconstruct
                        var dtor = new MethodDefinition("Deconstruct", MethodAttributes.Public | MethodAttributes.HideBySig, voidType);
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            dtor.Parameters.Add(new ParameterDefinition("o" + i, ParameterAttributes.Out, new ByReferenceType(injecttype.GenericParameters[i])));
                        }
                        var emitter = dtor.Body.GetILProcessor();
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            emitter.Emit(OpCodes.Ldarg_S, (byte)(i + 1));
                            emitter.Emit(OpCodes.Ldarg_0);
                            emitter.Emit(OpCodes.Ldfld, new FieldReference("t" + i, injecttype.GenericParameters[i], selfType));
                            emitter.Emit(OpCodes.Stobj, injecttype.GenericParameters[i]);
                        }
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Methods.Add(dtor);
                    }
                    var luahubtype = luadll.MainModule.GetType("Capstones.LuaLib.LuaHub");
                    MethodDefinition getLuaDef = null;
                    MethodDefinition pushLuaDef = null;
                    for (int i = 0; i < luahubtype.Methods.Count; ++i)
                    {
                        var method = luahubtype.Methods[i];
                        if (method.GenericParameters.Count == 1)
                        {
                            if (method.Name == "GetLua")
                            {
                                if (method.Parameters.Count == 3)
                                {
                                    getLuaDef = method;
                                }
                            }
                            else if (method.Name == "PushLua")
                            {
                                if (method.Parameters.Count == 2)
                                {
                                    pushLuaDef = method;
                                }
                            }
                        }
                    }
                    { // GetFromLua
                        var func = new MethodDefinition("GetFromLua", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, voidType);
                        func.Parameters.Add(new ParameterDefinition("l", ParameterAttributes.None, intPtrType));
                        var emitter = func.Body.GetILProcessor();
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            var gindex = paramCnt - 1 - i;
                            emitter.Emit(OpCodes.Ldarg_1);
                            emitter.Emit(OpCodes.Ldc_I4_S, (sbyte)(-1 - i));
                            emitter.Emit(OpCodes.Ldarg_0);
                            emitter.Emit(OpCodes.Ldflda, new FieldReference("t" + gindex, injecttype.GenericParameters[gindex], selfType));
                            var getLuaFunc = new GenericInstanceMethod(getLuaDef);
                            getLuaFunc.GenericArguments.Add(injecttype.GenericParameters[gindex]);
                            emitter.Emit(OpCodes.Call, getLuaFunc);
                        }
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Methods.Add(func);
                    }
                    { // PushToLua
                        var func = new MethodDefinition("PushToLua", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, voidType);
                        func.Parameters.Add(new ParameterDefinition("l", ParameterAttributes.None, intPtrType));
                        var emitter = func.Body.GetILProcessor();
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            var gindex = paramCnt - 1 - i;
                            emitter.Emit(OpCodes.Ldarg_1);
                            emitter.Emit(OpCodes.Ldarg_0);
                            emitter.Emit(OpCodes.Ldfld, new FieldReference("t" + i, injecttype.GenericParameters[i], selfType));
                            var pushLuaFunc = new GenericInstanceMethod(pushLuaDef);
                            pushLuaFunc.GenericArguments.Add(injecttype.GenericParameters[i]);
                            emitter.Emit(OpCodes.Call, pushLuaFunc);
                        }
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Methods.Add(func);
                    }
                    { // ElementCount
                        var prop = new PropertyDefinition("ElementCount", PropertyAttributes.None, intType);
                        var getter = prop.GetMethod = new MethodDefinition("get_ElementCount", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, intType);
                        var emitter = getter.Body.GetILProcessor();
                        emitter.Emit(OpCodes.Ldc_I4_S, (sbyte)paramCnt);
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Properties.Add(prop);
                        injecttype.Methods.Add(getter);
                    }
                    { // Item
                        var prop = new PropertyDefinition("Item", PropertyAttributes.None, objectType);
                        injecttype.Properties.Add(prop);
                        { // getter
                            var getter = prop.GetMethod = new MethodDefinition("get_Item", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, objectType);
                            getter.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, intType));
                            var emitter = getter.Body.GetILProcessor();
                            emitter.Emit(OpCodes.Ldsfld, new FieldReference("_IndexAccessors", indexerListType, selfType));
                            emitter.Emit(OpCodes.Ldarg_0);
                            emitter.Emit(OpCodes.Ldarg_1);
                            var getMethod = new MethodReference("GetItem", objectType, indexerListType) { HasThis = true };
                            getMethod.Parameters.Add(new ParameterDefinition("thiz", ParameterAttributes.None, new ByReferenceType(indexerListDefType.GenericParameters[0])));
                            getMethod.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, intType));
                            emitter.Emit(OpCodes.Callvirt, getMethod);
                            emitter.Emit(OpCodes.Ret);
                            injecttype.Methods.Add(getter);
                        }
                        { // setter
                            var setter = prop.SetMethod = new MethodDefinition("set_Item", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, voidType);
                            setter.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, intType));
                            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, objectType));
                            var emitter = setter.Body.GetILProcessor();
                            emitter.Emit(OpCodes.Ldsfld, new FieldReference("_IndexAccessors", indexerListType, selfType));
                            emitter.Emit(OpCodes.Ldarg_0);
                            emitter.Emit(OpCodes.Ldarg_1);
                            emitter.Emit(OpCodes.Ldarg_2);
                            var setMethod = new MethodReference("SetItem", voidType, indexerListType) { HasThis = true };
                            setMethod.Parameters.Add(new ParameterDefinition("thiz", ParameterAttributes.None, new ByReferenceType(indexerListDefType.GenericParameters[0])));
                            setMethod.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, intType));
                            setMethod.Parameters.Add(new ParameterDefinition("val", ParameterAttributes.None, objectType));
                            emitter.Emit(OpCodes.Callvirt, setMethod);
                            emitter.Emit(OpCodes.Ret);
                            injecttype.Methods.Add(setter);
                        }
                    }

                    MarkDirty("CapsLua");
                    existing = injecttype;
                }
                return existing;
            }
        }

        public static HashSet<string> GetLuaDeps()
        {
            HashSet<string> rv = new HashSet<string>();
            List<string> list = new List<string>();

            rv.Add("CapsLua");
            list.Add("CapsLua");

            for (int i = 0; i < list.Count; ++i)
            {
                var asmname = list[i];
                var asm = GetAssembly(asmname);
                if (asm != null)
                {
                    foreach (var module in asm.Modules)
                    {
                        foreach (var dep in module.AssemblyReferences)
                        {
                            if (rv.Add(dep.Name))
                            {
                                list.Add(dep.Name);
                            }
                        }
                    }
                }
            }

            rv.Remove("CapsLua");
            return rv;
        }

        internal static TypeDefinition GetType(string typeName)
        {
            foreach (var kvp in _LoadedAsms)
            {
                var asm = kvp.Value;
                if (asm != null && asm.Asm != null)
                {
                    var found = GetType(asm.Asm, typeName);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
        internal static TypeDefinition GetType(AssemblyDefinition asm, string token)
        {
            foreach (var module in asm.Modules)
            {
                foreach (var type in module.Types)
                {
                    var found = GetType(type, token);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
        internal static TypeDefinition GetType(TypeDefinition type, string token)
        {
            if (token == ReflectAnalyzer.GetIDString(type))
            {
                return type;
            }
            else
            {
                foreach (var ntype in type.NestedTypes)
                {
                    var found = GetType(ntype, token);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
        internal static MethodDefinition GetMethodFromSig(TypeDefinition type, string sig)
        {
            foreach (var method in type.Methods)
            {
                if (ReflectAnalyzer.GetIDString(method) == sig)
                {
                    return method;
                }
            }
            return null;
        }
        internal static MethodDefinition GetMethod(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }
            var splitIndex = token.IndexOf(" ");
            if (splitIndex <= 0)
            {
                return null;
            }
            var methodSig = token.Substring(splitIndex + " ".Length);
            if (string.IsNullOrEmpty(methodSig))
            {
                return null;
            }
            var typeName = token.Substring(0, splitIndex);
            var found = GetType(typeName);
            if (found != null)
            {
                return GetMethodFromSig(found, methodSig);
            }
            return null;
        }

        internal static List<MethodDefinition> GetHotFixMethods(AssemblyDefinition asm)
        {
            List<MethodDefinition> list = new List<MethodDefinition>();
            foreach (var module in asm.Modules)
            {
                foreach (var type in module.Types)
                {
                    GetHotFixMethods(list, type);
                }
            }
            return list;
        }
        internal static void GetHotFixMethods(List<MethodDefinition> list, TypeDefinition type)
        {
            bool allmember = false;
            if (HasHotFixAttribute(type))
            {
                //var cctor = type.GetMethod(".cctor", 0);
                //if (cctor != null && (cctor.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private)
                //{
                //    list.Add(cctor);
                //}

                allmember = true;
            }
            foreach (var method in type.Methods)
            {
                if (allmember || HasHotFixAttribute(method))
                {
                    list.Add(method);
                }
            }
            if (!allmember)
            { // if allmember, the methods should already be added.
                foreach (var prop in type.Properties)
                {
                    if (HasHotFixAttribute(prop))
                    {
                        if (prop.GetMethod != null)
                        {
                            if (!HasHotFixAttribute(prop.GetMethod))
                            { // if it has this attr, it will be already added.
                                list.Add(prop.GetMethod);
                            }
                        }
                        if (prop.SetMethod != null)
                        {
                            if (!HasHotFixAttribute(prop.SetMethod))
                            { // if it has this attr, it will be already added.
                                list.Add(prop.SetMethod);
                            }
                        }
                    }
                }
            }
            // TODO: events?
            foreach (var ntype in type.NestedTypes)
            {
                GetHotFixMethods(list, ntype);
            }
        }
        internal static bool HasHotFixAttribute(this IMemberDefinition method)
        {
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.FullName == "Capstones.LuaWrap.LuaHotFixAttribute")
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Inject(MethodDefinition method)
        {
            var luadll = GetOrLoadAssembly("CapsLua");
            string token = ReflectAnalyzer.GetIDString(method.DeclaringType) + " " + ReflectAnalyzer.GetIDString(method);

            List<int> InArgs = new List<int>();
            List<int> PostInArgs = new List<int>(); // in tail hotfix, we can pass existing return value and out values to lua, so there is more args than head hotfix
            List<int> OutArgs = new List<int>();
            List<TypeReference> argTypes = new List<TypeReference>();
            List<TypeReference> argEleTypes = new List<TypeReference>();
            bool shouldReturnVal = false;
            //bool isStructInstanceMethod = false;
            int parametersOffset = 0;
            if (method.ReturnType != method.Module.TypeSystem.Void)
            {
                OutArgs.Add(-1);
                PostInArgs.Add(-1);
                shouldReturnVal = true;
            }
            if (!method.IsStatic)
            {
                ++parametersOffset;
                InArgs.Add(0);
                PostInArgs.Add(0);
                TypeReference thizType = method.DeclaringType;
                if (method.DeclaringType.HasGenericParameters)
                {
                    var gthizType = new GenericInstanceType(thizType);
                    foreach (var gpar in method.DeclaringType.GenericParameters)
                    {
                        gthizType.GenericArguments.Add(gpar);
                    }
                    thizType = gthizType;
                }
                argEleTypes.Add(thizType);
                if (method.DeclaringType.IsValueType)
                {
                    OutArgs.Add(0);
                    argTypes.Add(new ByReferenceType(thizType));
                    //isStructInstanceMethod = true;
                }
                else
                {
                    argTypes.Add(thizType);
                }
            }
            for (int i = 0; i < method.Parameters.Count; ++i)
            {
                var argindex = i + parametersOffset;
                var par = method.Parameters[i];
                argTypes.Add(par.ParameterType);
                PostInArgs.Add(argindex);
                if (par.ParameterType.IsByReference)
                {
                    var partype = par.ParameterType as ByReferenceType;
                    argEleTypes.Add(partype.ElementType);
                    OutArgs.Add(argindex);
                    if (par.Attributes.HasFlag(ParameterAttributes.In) || !par.Attributes.HasFlag(ParameterAttributes.Out)) // try always send byref val to lua
                    {
                        InArgs.Add(argindex);
                    }
                }
                else
                {
                    argEleTypes.Add(par.ParameterType);
                    InArgs.Add(argindex);
                }
            }
            if (shouldReturnVal)
            {
                argTypes.Add(method.ReturnType);
                argEleTypes.Add(method.ReturnType); // TODO: ref return
            }
            var ideftype = method.Module.ImportReference(GetLuaPack(InArgs.Count));
            var pideftype = method.Module.ImportReference(GetLuaPack(PostInArgs.Count));
            var odeftype = method.Module.ImportReference(GetLuaPack(OutArgs.Count));
            TypeReference itype = ideftype;
            if (InArgs.Count > 0)
            {
                var gitype = new GenericInstanceType(ideftype);
                itype = gitype;
                for (int i = 0; i < InArgs.Count; ++i)
                {
                    gitype.GenericArguments.Add(argEleTypes[InArgs[i]]);
                }
            }
            TypeReference pitype = pideftype;
            if (PostInArgs.Count > 0)
            {
                var gitype = new GenericInstanceType(pideftype);
                pitype = gitype;
                for (int i = 0; i < PostInArgs.Count; ++i)
                {
                    gitype.GenericArguments.Add(argEleTypes[(PostInArgs[i] + argEleTypes.Count) % argEleTypes.Count]);
                }
            }
            TypeReference otype = odeftype;
            if (OutArgs.Count > 0)
            {
                var gotype = new GenericInstanceType(odeftype);
                otype = gotype;
                for (int i = 0; i < OutArgs.Count; ++i)
                {
                    gotype.GenericArguments.Add(argEleTypes[(OutArgs[i] + argEleTypes.Count) % argEleTypes.Count]);
                }
            }

            var excallClass = luadll.MainModule.GetType("Capstones.LuaWrap.HotFixCaller");
            var excallDef = excallClass.GetMethod("CallHotFix");
            var precallMethod = new GenericInstanceMethod(excallDef.GetReference(method.Module));
            precallMethod.GenericArguments.Add(itype);
            precallMethod.GenericArguments.Add(otype);
            var postcallMethod = new GenericInstanceMethod(excallDef.GetReference(method.Module));
            postcallMethod.GenericArguments.Add(pitype);
            postcallMethod.GenericArguments.Add(otype);

            var ivar = new VariableDefinition(itype);
            var pivar = new VariableDefinition(pitype);
            var ovar = new VariableDefinition(otype);
            method.Body.Variables.Add(ivar);
            method.Body.Variables.Add(pivar);
            method.Body.Variables.Add(ovar);
            VariableDefinition rvvar = null;
            if (shouldReturnVal)
            {
                rvvar = new VariableDefinition(method.ReturnType);
                method.Body.Variables.Add(rvvar);
            }
            var emitter = method.Body.GetILProcessor();

            List<Instruction> PreParts = new List<Instruction>();
            List<Instruction> PostParts = new List<Instruction>();
            Instruction postStart = null;
            Instruction postEnd = emitter.Create(OpCodes.Ret);
            Instruction bodyStart = method.Body.Instructions[0];

            { // Post parts
                { // ctor for pivar
                    if (shouldReturnVal)
                    {
                        PostParts.Add(emitter.Create(OpCodes.Stloc, rvvar));
                    }
                    var ictor = new MethodReference(".ctor", luadll.MainModule.TypeSystem.Void, pitype) { HasThis = true };
                    for (int i = 0; i < PostInArgs.Count; ++i)
                    {
                        ictor.Parameters.Add(new ParameterDefinition(pideftype.GenericParameters[i]));
                    }
                    PostParts.Add(emitter.Create(OpCodes.Ldloca, pivar));
                    for (int i = 0; i < PostInArgs.Count; ++i)
                    {
                        var argindex = PostInArgs[i];
                        if (argindex < 0)
                        {  // this is return
                            PostParts.Add(emitter.Create(OpCodes.Ldloc, rvvar));
                            continue;
                        }
                        PostParts.Add(emitter.Create(OpCodes.Ldarg, argindex));
                        if (argTypes[argindex].IsByReference)
                        {
                            PostParts.Add(emitter.Create(OpCodes.Ldobj, argEleTypes[argindex]));
                        }
                    }
                    PostParts.Add(emitter.Create(OpCodes.Call, ictor));
                }

                PostParts.Add(emitter.Create(OpCodes.Ldstr, token + " tail"));
                PostParts.Add(emitter.Create(OpCodes.Ldloc, pivar));
                PostParts.Add(emitter.Create(OpCodes.Ldloca, ovar));
                PostParts.Add(emitter.Create(OpCodes.Call, postcallMethod));
                var postJump = emitter.Create(OpCodes.Brfalse, postEnd);
                if (shouldReturnVal || OutArgs.Count > 0)
                {
                    PostParts.Add(postJump);
                }
                else
                { // no return and no out values
                    PostParts.Add(emitter.Create(OpCodes.Pop));
                }
                for (int i = 0; i < OutArgs.Count; ++i)
                {
                    var argindex = OutArgs[i];
                    if (argindex < 0)
                    {  // this is return
                        continue;
                    }
                    PostParts.Add(emitter.Create(OpCodes.Ldarg, argindex));
                    PostParts.Add(emitter.Create(OpCodes.Ldloc, ovar));
                    PostParts.Add(emitter.Create(OpCodes.Ldfld, new FieldReference("t" + i, odeftype.GenericParameters[i], otype)));
                    PostParts.Add(emitter.Create(OpCodes.Stobj, argEleTypes[argindex]));
                }
                if (shouldReturnVal)
                {
                    PostParts.Add(emitter.Create(OpCodes.Ldloc, ovar));
                    PostParts.Add(emitter.Create(OpCodes.Ldfld, new FieldReference("t0", odeftype.GenericParameters[0], otype)));
                    PostParts.Add(emitter.Create(OpCodes.Ret));
                }
                if (shouldReturnVal)
                {
                    var postJumpTo = emitter.Create(OpCodes.Ldloc, rvvar);
                    PostParts.Add(postJumpTo);
                    postJump.Operand = postJumpTo;
                }
                PostParts.Add(postEnd);
                postStart = PostParts[0];
            }

            { // Pre parts
                { // ctor for ivar
                    var ictor = new MethodReference(".ctor", luadll.MainModule.TypeSystem.Void, itype) { HasThis = true };
                    for (int i = 0; i < InArgs.Count; ++i)
                    {
                        ictor.Parameters.Add(new ParameterDefinition(ideftype.GenericParameters[i]));
                    }
                    PreParts.Add(emitter.Create(OpCodes.Ldloca, ivar));
                    for (int i = 0; i < InArgs.Count; ++i)
                    {
                        var argindex = InArgs[i];
                        PreParts.Add(emitter.Create(OpCodes.Ldarg, argindex));
                        if (argTypes[argindex].IsByReference)
                        {
                            PreParts.Add(emitter.Create(OpCodes.Ldobj, argEleTypes[argindex]));
                        }
                    }
                    PreParts.Add(emitter.Create(OpCodes.Call, ictor));
                }

                PreParts.Add(emitter.Create(OpCodes.Ldstr, token + " head"));
                PreParts.Add(emitter.Create(OpCodes.Ldloc, ivar));
                PreParts.Add(emitter.Create(OpCodes.Ldloca, ovar));
                PreParts.Add(emitter.Create(OpCodes.Call, precallMethod));
                PreParts.Add(emitter.Create(OpCodes.Brfalse, bodyStart));
                for (int i = 0; i < OutArgs.Count; ++i)
                {
                    var argindex = OutArgs[i];
                    if (argindex < 0)
                    {  // this is return
                        continue;
                    }
                    PreParts.Add(emitter.Create(OpCodes.Ldarg, argindex));
                    PreParts.Add(emitter.Create(OpCodes.Ldloc, ovar));
                    PreParts.Add(emitter.Create(OpCodes.Ldfld, new FieldReference("t" + i, odeftype.GenericParameters[i], otype)));
                    PreParts.Add(emitter.Create(OpCodes.Stobj, argEleTypes[argindex]));
                }
                if (shouldReturnVal)
                {
                    PreParts.Add(emitter.Create(OpCodes.Ldloc, ovar));
                    PreParts.Add(emitter.Create(OpCodes.Ldfld, new FieldReference("t0", odeftype.GenericParameters[0], otype)));
                }
                PreParts.Add(emitter.Create(OpCodes.Ret));
            }

            { // change existing ret to br
                for (int i = 0; i < method.Body.Instructions.Count; ++i)
                {
                    var ins = method.Body.Instructions[i];
                    if (ins.OpCode == OpCodes.Ret)
                    {
                        emitter.Replace(ins, emitter.Create(OpCodes.Br, postStart));
                    }
                }
            }

            { // Insert PreParts
                foreach (var ins in PreParts)
                {
                    emitter.InsertBefore(bodyStart, ins);
                }
            }

            { // Append PostParts
                foreach (var ins in PostParts)
                {
                    emitter.Append(ins);
                }
            }

            List<Instruction> insToDelete = new List<Instruction>();
            { // delete br to next
                for (int i = 0; i < method.Body.Instructions.Count; ++i)
                {
                    var ins = method.Body.Instructions[i];
                    if (ins.OpCode == OpCodes.Br)
                    {
                        if (ins.Operand == ins.Next)
                        {
                            insToDelete.Add(ins);
                        }
                    }
                }
            }

            { // delete instruction
                foreach (var dins in insToDelete)
                {
                    for (int i = 0; i < method.Body.Instructions.Count; ++i)
                    {
                        var ins = method.Body.Instructions[i];
                        if (ins.Operand == dins)
                        {
                            ins.Operand = dins.Next;
                        }
                    }
                    emitter.Remove(dins);
                }
            }

            MarkDirty(method.Module.Assembly.Name.Name);
        }

        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> AddWork(this Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, string asm, string typestr, MethodDefinition method)
        {
            Dictionary<string, List<MethodDefinition>> typedict;
            if (!works.TryGetValue(asm, out typedict))
            {
                typedict = new Dictionary<string, List<MethodDefinition>>();
                works[asm] = typedict;
            }
            List<MethodDefinition> list;
            if (!typedict.TryGetValue(typestr, out list))
            {
                list = new List<MethodDefinition>();
                typedict[typestr] = list;
            }
            list.Add(method);
            return works;
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> AddWork(this Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, MethodDefinition method)
        {
            return AddWork(works, method.Module.Assembly.Name.Name, ReflectAnalyzer.GetIDString(method.DeclaringType), method);
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> ParseInjectWork(IList<string> methodNames, bool searchAttribute)
        {
            Dictionary<string, Dictionary<string, List<MethodDefinition>>> rv = new Dictionary<string, Dictionary<string, List<MethodDefinition>>>();
            var luadeps = LuaHotFixCodeInjector.GetLuaDeps();
            HashSet<string> memberset = new HashSet<string>();
            Dictionary<string, TypeDefinition> typeCache = new Dictionary<string, TypeDefinition>();
            if (searchAttribute)
            {
                foreach (var loaded in _LoadedAsms)
                {
                    if (!luadeps.Contains(loaded.Key) && loaded.Value != null && loaded.Value.Asm != null)
                    {
                        var methods = GetHotFixMethods(loaded.Value.Asm);
                        foreach (var method in methods)
                        {
                            var typestr = ReflectAnalyzer.GetIDString(method.DeclaringType);
                            var token = typestr + " " + ReflectAnalyzer.GetIDString(method);
                            if (memberset.Add(token))
                            {
                                typeCache[typestr] = method.DeclaringType;
                                AddWork(rv, loaded.Key, typestr, method);
                            }
                        }
                    }
                }
            }
            HashSet<string> allMemberTypes = new HashSet<string>();
            if (methodNames != null)
            {
                foreach (var mname in methodNames)
                {
                    if (!string.IsNullOrEmpty(mname))
                    {
                        if (mname.StartsWith("type "))
                        {
                            var typestr = mname.Substring("type ".Length);
                            allMemberTypes.Add(typestr);
                        }
                        else if (mname.StartsWith("member "))
                        {
                            var parts = mname.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts != null && parts.Length >= 5)
                            {
                                var typestr = parts[3];
                                var sig = parts[4];
                                if (sig == "*")
                                {
                                    allMemberTypes.Add(typestr);
                                }
                                else
                                {
                                    if (parts[1] == "prop")
                                    {
                                        TypeDefinition type;
                                        if (!typeCache.TryGetValue(typestr, out type))
                                        {
                                            type = GetType(typestr);
                                            if (type != null && luadeps.Contains(type.Module.Assembly.Name.Name))
                                            {
                                                type = null;
                                            }
                                            typeCache[typestr] = type;
                                        }
                                        if (type != null)
                                        {
                                            var prop = GetProperty(type, sig);
                                            if (prop != null)
                                            {
                                                {
                                                    var method = prop.GetMethod;
                                                    if (method != null)
                                                    {
                                                        var rsig = ReflectAnalyzer.GetIDString(method);
                                                        var token = typestr + " " + rsig;
                                                        if (memberset.Add(token))
                                                        {
                                                            AddWork(rv, type.Module.Assembly.Name.Name, typestr, method);
                                                        }
                                                    }
                                                }
                                                {
                                                    var method = prop.SetMethod;
                                                    if (method != null)
                                                    {
                                                        var rsig = ReflectAnalyzer.GetIDString(method);
                                                        var token = typestr + " " + rsig;
                                                        if (memberset.Add(token))
                                                        {
                                                            AddWork(rv, type.Module.Assembly.Name.Name, typestr, method);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (parts[1] == "func" || parts[1] == "ctor")
                                    {
                                        var token = typestr + " " + sig;
                                        if (memberset.Add(token))
                                        {
                                            TypeDefinition type;
                                            if (!typeCache.TryGetValue(typestr, out type))
                                            {
                                                type = GetType(typestr);
                                                if (type != null && luadeps.Contains(type.Module.Assembly.Name.Name))
                                                {
                                                    type = null;
                                                }
                                                typeCache[typestr] = type;
                                            }
                                            if (type != null)
                                            {
                                                var method = GetMethodFromSig(type, sig);
                                                if (method != null)
                                                {
                                                    AddWork(rv, type.Module.Assembly.Name.Name, typestr, method);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (var typestr in allMemberTypes)
            {
                TypeDefinition type;
                if (!typeCache.TryGetValue(typestr, out type))
                {
                    type = GetType(typestr);
                    if (type != null && luadeps.Contains(type.Module.Assembly.Name.Name))
                    {
                        type = null;
                    }
                    typeCache[typestr] = type;
                }
                if (type != null)
                {
                    foreach (var method in type.Methods)
                    {
                        var sig = ReflectAnalyzer.GetIDString(method);
                        var token = typestr + " " + sig;
                        if (memberset.Add(token))
                        {
                            AddWork(rv, type.Module.Assembly.Name.Name, typestr, method);
                        }
                    }
                }
            }
            return rv;
        }
        internal static List<MethodDefinition> MergeInjectWork(this Dictionary<string, Dictionary<string, List<MethodDefinition>>> works)
        {
            List<MethodDefinition> rv = new List<MethodDefinition>();
            foreach (var typedict in works.Values)
            {
                foreach (var list in typedict.Values)
                {
                    rv.AddRange(list);
                }
            }
            return rv;
        }

        public static void Inject(IList<string> methodNames, bool searchAttribute)
        {
            var work = MergeInjectWork(ParseInjectWork(methodNames, searchAttribute));
            foreach (var method in work)
            {
                Inject(method);
            }
        }
    }
}