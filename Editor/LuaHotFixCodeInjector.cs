using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Cecil.Mdb;

using Object = UnityEngine.Object;

namespace Capstones.UnityEditorEx
{
    public static class LuaHotFixCodeInjector
    {
        #region Load or Unload Assemblies
        private class LoadedAssembly : IDisposable
        {
            private class SymbolWriterProvider : ISymbolWriterProvider
            {
                private ISymbolWriterProvider Inner = new PortablePdbWriterProvider();

                public ISymbolWriter GetSymbolWriter(ModuleDefinition module, string fileName)
                {
                    //var ext = System.IO.Path.GetExtension(fileName);
                    //var noext = fileName.Substring(0, fileName.Length - ext.Length);
                    //var tmppdb = noext + ".pdb";
                    //return Inner.GetSymbolWriter(module, tmppdb);
                    return Inner.GetSymbolWriter(module, fileName);
                }
                public ISymbolWriter GetSymbolWriter(ModuleDefinition module, System.IO.Stream symbolStream)
                {
                    return Inner.GetSymbolWriter(module, symbolStream);
                }
            }
            private static SymbolWriterProvider SymbolWriterProviderInstance = new SymbolWriterProvider();

            public AssemblyDefinition Asm;
            public string Path;
            public bool Dirty;
            public bool NoPdb;

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
                        try
                        {
                            Asm = AssemblyDefinition.ReadAssembly(Path, _AssemblyReaderParameters);
                        }
                        catch (SymbolsNotFoundException)
                        {
                            NoPdb = true;
                            Asm = AssemblyDefinition.ReadAssembly(Path, _AssemblyReaderParameters_NoPdb);
                        }
                    }
                }
            }
            public void Dispose()
            {
                if (Asm != null)
                {
                    if (Dirty)
                    {
                        var ext = System.IO.Path.GetExtension(Path);
                        var noext = Path.Substring(0, Path.Length - ext.Length);
                        var pdb = noext + ".pdb";
                        var tmp = noext + ".tmp" + ext;
                        var tmppdb = noext + ".tmp" + ".pdb";
                        var wp = new WriterParameters() { WriteSymbols = !NoPdb };
                        if (!NoPdb)
                        {
                            wp.SymbolWriterProvider = SymbolWriterProviderInstance;
                        }
                        Asm.Write(tmp, wp);
                        Asm.Dispose();
                        Asm = null;
                        System.IO.File.Delete(Path);
                        System.IO.File.Move(tmp, Path);
                        if (!NoPdb)
                        {
                            System.IO.File.Delete(pdb);
                            System.IO.File.Move(tmppdb, pdb);
                        }
                        Dirty = false;
                    }
                    else
                    {
                        Asm.Dispose();
                        Asm = null;
                    }
                }
            }
            protected internal void WriteTempFile()
            {
                if (Asm != null)
                {
                    if (Dirty)
                    {
                        var ext = System.IO.Path.GetExtension(Path);
                        var noext = Path.Substring(0, Path.Length - ext.Length);
                        var pdb = noext + ".pdb";
                        var tmp = noext + ".tmp" + ext;
                        var tmppdb = noext + ".tmp" + ".pdb";
                        var wp = new WriterParameters() { WriteSymbols = !NoPdb };
                        if (!NoPdb)
                        {
                            wp.SymbolWriterProvider = SymbolWriterProviderInstance;
                        }
                        Asm.Write(tmp, wp);
                        Dirty = false;
                    }
                }
            }
            protected internal void MoveTempFile()
            {
                var ext = System.IO.Path.GetExtension(Path);
                var noext = Path.Substring(0, Path.Length - ext.Length);
                var tmp = noext + ".tmp" + ext;
                if (System.IO.File.Exists(tmp))
                {
                    System.IO.File.Delete(Path);
                    System.IO.File.Move(tmp, Path);
                    if (!string.IsNullOrEmpty(CopyToDestFolder))
                    {
                        var destdll = System.IO.Path.Combine(CopyToDestFolder, System.IO.Path.GetFileName(Path));
                        System.IO.File.Delete(destdll);
                        System.IO.File.Copy(Path, destdll);
                    }
                    if (!NoPdb)
                    {
                        var pdb = noext + ".pdb";
                        var tmppdb = noext + ".tmp" + ".pdb";
                        if (System.IO.File.Exists(tmppdb))
                        {
                            System.IO.File.Delete(pdb);
                            System.IO.File.Move(tmppdb, pdb);
                            if (!string.IsNullOrEmpty(CopyToDestFolder))
                            {
                                var destpdb = System.IO.Path.Combine(CopyToDestFolder, System.IO.Path.GetFileName(pdb));
                                System.IO.File.Delete(destpdb);
                                System.IO.File.Copy(pdb, destpdb);
                            }
                        }
                    }
                }
            }
        }
        private static Dictionary<string, LoadedAssembly> _LoadedAsms = new Dictionary<string, LoadedAssembly>();
        private class AssemblyResolver : BaseAssemblyResolver
        {
            private DefaultAssemblyResolver _defaultResolver;

            public AssemblyResolver()
            {
                _defaultResolver = new DefaultAssemblyResolver();
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                AssemblyDefinition assembly = null;
                try
                {
                    assembly = _defaultResolver.Resolve(name);
                }
                catch (AssemblyResolutionException)
                {
                    LoadedAssembly loaded;
                    if (_LoadedAsms.TryGetValue(name.Name, out loaded))
                    {
                        assembly = loaded.Asm;
                    }
                    else
                    {
                        // TryLoad From AssembliesDirectory
                        assembly = LoadAssembly(name.Name);
                        if (assembly == null)
                        {
                            // the UnityEngine.dll ?
                            string path;
                            if (InternalAssemblies.TryGetValue(name.Name, out path))
                            {
                                try
                                {
                                    assembly = LoadAssembly(path);
                                }
                                catch (Exception e)
                                {
                                    UnityEngine.Debug.LogException(e);
                                }
                            }
                        }
                    }
                }
                return assembly;
            }
        }
        private static AssemblyResolver _AssemblyResolver = new AssemblyResolver();
        private static ReaderParameters _AssemblyReaderParameters = new ReaderParameters()
        {
            AssemblyResolver = _AssemblyResolver,
            ReadSymbols = true,
        };
        private static ReaderParameters _AssemblyReaderParameters_NoPdb = new ReaderParameters()
        {
            AssemblyResolver = _AssemblyResolver,
            ReadSymbols = false,
        };

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
        public static readonly Dictionary<string, string> InternalAssemblies = new Dictionary<string, string>();
        public static void LoadInternalAssemblies()
        {
            InternalAssemblies.Clear();
            var names = UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyNames();
            for (int i = 0; i < names.Length; ++i)
            {
                var asmname = names[i];
                var path = UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyPathFromAssemblyName(asmname);
                InternalAssemblies[asmname] = path;
            }
            var allunityasms = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies);
            for (int i = 0; i < allunityasms.Length; ++i)
            {
                var uasm = allunityasms[i];
                var refs = uasm.allReferences;
                for (int j = 0; j < refs.Length; ++j)
                {
                    var r = refs[j];
                    var name = System.IO.Path.GetFileNameWithoutExtension(r);
                    InternalAssemblies[name] = r;
                }
            }
        }
        static LuaHotFixCodeInjector()
        {
            LoadInternalAssemblies();
        }

        public static string CopyToDestFolder;

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
            var assemblies = new List<LoadedAssembly>(_LoadedAsms.Values);
            for (int i = 0; i < assemblies.Count; ++i)
            {
                assemblies[i].WriteTempFile();
            }
            assemblies.Clear();
            assemblies.AddRange(_LoadedAsms.Values);
            for (int i = 0; i < assemblies.Count; ++i)
            {
                assemblies[i].Dispose();
            }
            for (int i = 0; i < assemblies.Count; ++i)
            {
                assemblies[i].MoveTempFile();
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
        internal static bool IsSubclassOf(this TypeReference thisType, string baseTypeFullName)
        {
            var curType = thisType;
            while (curType != null)
            {
                if (curType.FullName == baseTypeFullName)
                {
                    return true;
                }
                try
                {
                    curType = curType.Resolve().BaseType;
                }
                catch
                {
                    curType = null;
                }
            }
            return false;
        }
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
        internal static TypeDefinition GetNestedType(this TypeDefinition type, string name)
        {
            foreach (var ntype in type.NestedTypes)
            {
                if (ntype.Name == name)
                {
                    return ntype;
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
        //internal static void Clear<T>(this Mono.Collections.Generic.Collection<T> collection)
        //{
        //    for (int i = collection.Count - 1; i >= 0; --i)
        //    {
        //        collection.RemoveAt(i);
        //    }
        //}
        #endregion

        #region Extra Generators
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

            var type = asm.MainModule.GetType("Capstones.ByRefUtils.Ref");
            TypeDefinition reftype = asm.MainModule.GetType("Capstones.ByRefUtils.RawRef");
            var field = reftype.GetField("_Ref");

            {
                var method = reftype.GetMethod("GetRef");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldfld, field);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = reftype.GetMethod("SetRef");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldarg_1);
                emitter.Emit(OpCodes.Stfld, field);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = reftype.GetMethod("GetRefObj");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldfld, field);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = reftype.GetMethod("SetRefObj");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldarg_1);
                emitter.Emit(OpCodes.Stfld, field);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = type.GetMethod("RefEquals");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldarg_1);
                emitter.Emit(OpCodes.Ceq);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = type.GetMethod("GetEmptyRef");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldnull);
                emitter.Emit(OpCodes.Ret);
            }
            {
                var method = type.GetMethod("IsEmpty");
                method.Body.Instructions.Clear();

                var emitter = method.Body.GetILProcessor();
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldnull);
                emitter.Emit(OpCodes.Ceq);
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
                    var valueType = new TypeReference("System", "ValueType", objectType.Module, objectType.Scope);
                    var voidType = luadll.MainModule.TypeSystem.Void;
                    var intType = luadll.MainModule.TypeSystem.Int32;
                    var intPtrType = luadll.MainModule.TypeSystem.IntPtr;
                    var typeType = new TypeReference("System", "Type", objectType.Module, objectType.Scope);
                    var typeHandleType = new TypeReference("System", "RuntimeTypeHandle", objectType.Module, objectType.Scope) { IsValueType = true };
                    var injecttype = new TypeDefinition("Capstones.LuaWrap", "LuaPack`" + paramCnt, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, valueType);
                    injecttype.Interfaces.Add(new InterfaceImplementation(luadll.MainModule.GetType("Capstones.LuaWrap.ILuaPack")));
                    { // attributes
                        var defaultMemberCtor = new MethodReference(".ctor", voidType, new TypeReference("System.Reflection", "DefaultMemberAttribute", objectType.Module, objectType.Scope)) { HasThis = true };
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
                    var etfieldtype = new ArrayType(typeType);
                    { // ElementTypes
                        var etfield = new FieldDefinition("ElementTypes", FieldAttributes.Private | FieldAttributes.Static, etfieldtype);
                        injecttype.Fields.Add(etfield);
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

                        emitter.Emit(OpCodes.Ldc_I4, paramCnt);
                        emitter.Emit(OpCodes.Newarr, typeType);
                        for (int i = 0; i < paramCnt; ++i)
                        {
                            emitter.Emit(OpCodes.Dup);
                            emitter.Emit(OpCodes.Ldc_I4, i);
                            emitter.Emit(OpCodes.Ldtoken, injecttype.GenericParameters[i]);
                            var typeofMethod = new MethodReference("GetTypeFromHandle", typeType, typeType) { HasThis = false };
                            typeofMethod.Parameters.Add(new ParameterDefinition(typeHandleType));
                            emitter.Emit(OpCodes.Call, typeofMethod);
                            emitter.Emit(OpCodes.Stelem_Ref);
                        }
                        emitter.Emit(OpCodes.Stsfld, new FieldReference("ElementTypes", etfieldtype, selfType));

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
                    { // Length
                        var prop = new PropertyDefinition("Length", PropertyAttributes.None, intType);
                        var getter = prop.GetMethod = new MethodDefinition("get_Length", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, intType);
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
                    { // GetType(index)
                        var func = new MethodDefinition("GetType", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, typeType);
                        func.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, intType));
                        var emitter = func.Body.GetILProcessor();
                        emitter.Emit(OpCodes.Ldsfld, new FieldReference("ElementTypes", etfieldtype, selfType));
                        emitter.Emit(OpCodes.Ldarg_1);
                        emitter.Emit(OpCodes.Ldelem_Ref);
                        emitter.Emit(OpCodes.Ret);
                        injecttype.Methods.Add(func);
                    }

                    MarkDirty("CapsLua");
                    existing = injecttype;
                }
                return existing;
            }
        }
        #endregion

        #region HotFix Token Hash
        private static Dictionary<string, long> _CachedTokenHash;
        private static Dictionary<long, string> _HashToHotFixToken;
        public static Dictionary<string, long> DesignatedHash;
        public static void LoadDesignatedHash(Dictionary<string, long> hashmap)
        {
            if (hashmap == null)
            {
                DesignatedHash = null;
                _CachedTokenHash = null;
                _HashToHotFixToken = null;
            }
            else
            {
                DesignatedHash = hashmap;
                _CachedTokenHash = new Dictionary<string, long>();
                _HashToHotFixToken = new Dictionary<long, string>();
                foreach (var kvp in hashmap)
                {
                    _CachedTokenHash[kvp.Key] = kvp.Value;
                    _HashToHotFixToken[kvp.Value] = kvp.Key;
                }
            }
        }
        private static long GetTokenHash(string token)
        {
            if (token == null) return 0;
            string mainpart = token;
            bool istail = false;
            if (token.EndsWith(" tail"))
            {
                istail = true;
                mainpart = token.Substring(0, token.Length - " tail".Length);
            }
            else if (token.EndsWith(" head"))
            {
                mainpart = token.Substring(0, token.Length - " head".Length);
            }

            long hash = 0;
            if (_CachedTokenHash == null || !_CachedTokenHash.TryGetValue(mainpart, out hash))
            {
                int criticalindex = mainpart.IndexOf(' ');
                if (criticalindex >= 0 && criticalindex + 1 < mainpart.Length)
                {
                    ++criticalindex;
                }
                else
                {
                    criticalindex = -1;
                }
                hash = ExtendedStringHash.GetHashCodeEx(mainpart, 0, 1, criticalindex, 0);

                string existingtoken;
                if (_HashToHotFixToken != null && _HashToHotFixToken.TryGetValue(hash, out existingtoken) && existingtoken != mainpart)
                { // hash conflict
                    int preservedflag = 1;
                    for (; preservedflag < 4; ++preservedflag)
                    {
                        var hash2 = hash | (((long)preservedflag) << 32);
                        if (_HashToHotFixToken == null || !_HashToHotFixToken.TryGetValue(hash2, out existingtoken) || existingtoken == mainpart)
                        {
                            hash = hash2;
                            break;
                        }
                    }
                    if (preservedflag >= 4)
                    {
                        do
                        {
                            ++hash;
                        } while (_HashToHotFixToken != null && _HashToHotFixToken.TryGetValue(hash, out existingtoken) && existingtoken != mainpart);
                    }
                    if (DesignatedHash == null)
                    {
                        DesignatedHash = new Dictionary<string, long>();
                    }
                    DesignatedHash[mainpart] = hash;
                }

                if (_CachedTokenHash == null)
                {
                    _CachedTokenHash = new Dictionary<string, long>();
                }
                _CachedTokenHash[mainpart] = hash;
                if (_HashToHotFixToken == null)
                {
                    _HashToHotFixToken = new Dictionary<long, string>();
                }
                _HashToHotFixToken[hash] = mainpart;
            }
            if (istail)
            {
                hash = -hash;
            }
            return hash;
        }
        #endregion

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

        internal static List<InjectWorkItem> GetHotFixMethods(AssemblyDefinition asm)
        {
            List<InjectWorkItem> list = new List<InjectWorkItem>();
            foreach (var module in asm.Modules)
            {
                foreach (var type in module.Types)
                {
                    GetHotFixMethods(list, type);
                }
            }
            return list;
        }
        internal static void GetHotFixMethods(List<InjectWorkItem> list, TypeDefinition type)
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

            HashSet<MethodDefinition> methodset = new HashSet<MethodDefinition>();
            foreach (var prop in type.Properties)
            {
                if (HasHotFixAttribute(prop))
                {
                    if (prop.GetMethod != null)
                    {
                        if (methodset.Add(prop.GetMethod))
                        {
                            list.Add(new InjectWorkItem(prop.GetMethod, true));
                        }
                    }
                    if (prop.SetMethod != null)
                    {
                        if (methodset.Add(prop.SetMethod))
                        {
                            list.Add(new InjectWorkItem(prop.SetMethod, true));
                        }
                    }
                }
            }
            foreach (var method in type.Methods)
            {
                if (HasHotFixAttribute(method))
                {
                    if (methodset.Add(method))
                    {
                        list.Add(new InjectWorkItem(method, true));
                    }
                }
                else if (allmember)
                {
                    if (methodset.Add(method))
                    {
                        list.Add(new InjectWorkItem(method, false));
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
                    if (!attr.HasFields || attr.Fields == null || attr.Fields.Count == 0)
                    {
                        return true;
                    }
                    for (int i = 0; i < attr.Fields.Count; ++i)
                    {
                        var field = attr.Fields[i];
                        if (field.Name == "Forbidden" && field.Argument.Value is bool)
                        {
                            if ((bool)field.Argument.Value)
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }
        internal static bool HasHotFixForbiddenAttribute(this IMemberDefinition method)
        {
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.FullName == "Capstones.LuaWrap.LuaHotFixAttribute")
                {
                    if (!attr.HasFields || attr.Fields == null || attr.Fields.Count == 0)
                    {
                        return false;
                    }
                    for (int i = 0; i < attr.Fields.Count; ++i)
                    {
                        var field = attr.Fields[i];
                        if (field.Name == "Forbidden" && field.Argument.Value is bool)
                        {
                            if ((bool)field.Argument.Value)
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
            }
            return false;
        }

        private abstract class InstructionOffsetOwner
        {
            public object Owner { get; set; }
            public abstract InstructionOffset Offset { get; set; }
        }
        private abstract class InstructionOffsetOwner<T> : InstructionOffsetOwner
        {
            public new T Owner
            {
                get
                {
                    return (T)base.Owner;
                }
                set
                {
                    base.Owner = value;
                }
            }
        }
        private class ScopeStartInstructionOffsetOwner : InstructionOffsetOwner<ScopeDebugInformation>
        {
            public ScopeStartInstructionOffsetOwner(ScopeDebugInformation scope)
            {
                Owner = scope;
            }
            public override InstructionOffset Offset
            {
                get
                {
                    return Owner.Start;
                }
                set
                {
                    Owner.Start = value;
                }
            }
        }
        private class ScopeEndInstructionOffsetOwner : InstructionOffsetOwner<ScopeDebugInformation>
        {
            public ScopeEndInstructionOffsetOwner(ScopeDebugInformation scope)
            {
                Owner = scope;
            }
            public override InstructionOffset Offset
            {
                get
                {
                    return Owner.End;
                }
                set
                {
                    Owner.End = value;
                }
            }
        }
        private class SequencePointInstructionOffsetOwner : InstructionOffsetOwner<SequencePoint>
        {
            private static Func<SequencePoint, InstructionOffset> GetFunc;
            private static Action<SequencePoint, InstructionOffset> SetFunc;

            static SequencePointInstructionOffsetOwner()
            {
                var fi = typeof(SequencePoint).GetField("offset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var pSequencePoint = Expression.Parameter(typeof(SequencePoint));
                GetFunc = Expression.Lambda<Func<SequencePoint, InstructionOffset>>(Expression.Field(pSequencePoint, fi), pSequencePoint).Compile();
                var pInstructionOffset = Expression.Parameter(typeof(InstructionOffset));
                SetFunc = Expression.Lambda<Action<SequencePoint, InstructionOffset>>(Expression.Assign(Expression.Field(pSequencePoint, fi), pInstructionOffset), pSequencePoint, pInstructionOffset).Compile();
            }

            public override InstructionOffset Offset
            {
                get
                {
                    return GetFunc(Owner);
                }
                set
                {
                    SetFunc(Owner, value);
                }
            }

            public SequencePointInstructionOffsetOwner(SequencePoint sp)
            {
                Owner = sp;
            }
        }
        internal static void Inject(MethodDefinition method)
        {
            // Contruct DebugInfo Mapping
            List<InstructionOffsetOwner> offsetOwners = new List<InstructionOffsetOwner>();
            var scopes = method.DebugInformation.GetScopes();
            foreach (var scope in scopes)
            {
                offsetOwners.Add(new ScopeStartInstructionOffsetOwner(scope));
                offsetOwners.Add(new ScopeEndInstructionOffsetOwner(scope));
            }
            var debugseqs = method.DebugInformation.SequencePoints;
            foreach (var dseq in debugseqs)
            {
                offsetOwners.Add(new SequencePointInstructionOffsetOwner(dseq));
            }
            Dictionary<InstructionOffsetOwner, Instruction> owner2ins = new Dictionary<InstructionOffsetOwner, Instruction>();
            Dictionary<Instruction, List<InstructionOffsetOwner>> ins2owner = new Dictionary<Instruction, List<InstructionOffsetOwner>>();
            Dictionary<int, Instruction> oldoff2ins = new Dictionary<int, Instruction>();
            foreach (var ins in method.Body.Instructions)
            {
                oldoff2ins[ins.Offset] = ins;
            }
            foreach (var owner in offsetOwners)
            {
                if (owner.Offset.IsEndOfMethod) continue;
                Instruction ins;
                if (oldoff2ins.TryGetValue(owner.Offset.Offset, out ins))
                {
                    owner2ins[owner] = ins;
                    List<InstructionOffsetOwner> list;
                    if (!ins2owner.TryGetValue(ins, out list))
                    {
                        list = new List<InstructionOffsetOwner>();
                        ins2owner[ins] = list;
                    }
                    list.Add(owner);
                }
            }
            // Contruct DebugInfo Mapping - END

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
            var excallDef = excallClass.GetMethod("CallHotFixN");
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
            if (!shouldReturnVal)
            {
                if (method.Body.Instructions.Count == 0)
                { // add a ret
                    method.Body.Instructions.Add(emitter.Create(OpCodes.Ret));
                }
            }
            bool hasRet = false; // if there is only one throw op and the method should return something, the stack will not have the return value before the method ends.
            {
                for (int i = 0; i < method.Body.Instructions.Count; ++i)
                {
                    var ins = method.Body.Instructions[i];
                    if (ins.OpCode == OpCodes.Ret)
                    {
                        hasRet = true;
                        break;
                    }
                }
            }

            List<Instruction> PreParts = new List<Instruction>();
            List<Instruction> PostParts = new List<Instruction>();
            Instruction postStart = null;
            Instruction postEnd = emitter.Create(OpCodes.Ret);
            Instruction bodyStart = method.Body.Instructions[0];

            { // Post parts
                if (shouldReturnVal)
                {
                    if (hasRet)
                    {
                        PostParts.Add(emitter.Create(OpCodes.Stloc, rvvar));
                    }
                }
                if (PostInArgs.Count > 0)
                { // ctor for pivar
                    PostParts.Add(emitter.Create(OpCodes.Ldloca, pivar));
                    var ictor = new MethodReference(".ctor", luadll.MainModule.TypeSystem.Void, pitype) { HasThis = true };
                    for (int i = 0; i < PostInArgs.Count; ++i)
                    {
                        ictor.Parameters.Add(new ParameterDefinition(pideftype.GenericParameters[i]));
                    }
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

                PostParts.Add(emitter.Create(OpCodes.Ldc_I8, GetTokenHash(token + " tail")));
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
                if (InArgs.Count > 0)
                { // ctor for ivar
                    PreParts.Add(emitter.Create(OpCodes.Ldloca, ivar));
                    var ictor = new MethodReference(".ctor", luadll.MainModule.TypeSystem.Void, itype) { HasThis = true };
                    for (int i = 0; i < InArgs.Count; ++i)
                    {
                        ictor.Parameters.Add(new ParameterDefinition(ideftype.GenericParameters[i]));
                    }
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

                PreParts.Add(emitter.Create(OpCodes.Ldc_I8, GetTokenHash(token + " head")));
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
                        ins.OpCode = OpCodes.Br;
                        ins.Operand = postStart;
                        //emitter.Replace(ins, emitter.Create(OpCodes.Br, postStart));
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

            //{ // fix ret to br
            //    var lastins = method.Body.Instructions[method.Body.Instructions.Count - 1];
            //    for (int i = 0; i < method.Body.Instructions.Count - 1; ++i)
            //    {
            //        var ins = method.Body.Instructions[i];
            //        if (ins.OpCode == OpCodes.Ret)
            //        {
            //            ins.OpCode = OpCodes.Br;
            //            ins.Operand = lastins;
            //        }
            //    }
            //}

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
                        else if (ins.Operand is IList)
                        {
                            var operands = ins.Operand as IList;
                            for (int j = 0; j < operands.Count; ++j)
                            {
                                if (operands[j] == dins)
                                {
                                    operands[j] = dins.Next;
                                }
                            }
                        }
                    }
                    if (method.Body.HasExceptionHandlers)
                    {
                        for (int i = 0; i < method.Body.ExceptionHandlers.Count; ++i)
                        {
                            var exhandler = method.Body.ExceptionHandlers[i];
                            if (exhandler.TryStart != null)
                            {
                                if (exhandler.TryStart == dins)
                                {
                                    exhandler.TryStart = dins.Next;
                                }
                            }
                            if (exhandler.TryEnd != null)
                            {
                                if (exhandler.TryEnd == dins)
                                {
                                    exhandler.TryEnd = dins.Next;
                                }
                            }
                            if (exhandler.FilterStart != null)
                            {
                                if (exhandler.FilterStart == dins)
                                {
                                    exhandler.FilterStart = dins.Next;
                                }
                            }
                            if (exhandler.HandlerStart != null)
                            {
                                if (exhandler.HandlerStart == dins)
                                {
                                    exhandler.HandlerStart = dins.Next;
                                }
                            }
                            if (exhandler.HandlerEnd != null)
                            {
                                if (exhandler.HandlerEnd == dins)
                                {
                                    exhandler.HandlerEnd = dins.Next;
                                }
                            }
                        }
                    }
                    if (ins2owner.ContainsKey(dins))
                    {
                        var list = ins2owner[dins];
                        ins2owner.Remove(dins);
                        var nxtins = dins.Next;
                        ins2owner[nxtins] = list;
                        foreach (var owner in list)
                        {
                            owner2ins[owner] = nxtins;
                        }
                    }
                    emitter.Remove(dins);
                }
            }

            // replace invalid ShortInlineBrTarget to InlineBrTarget
            FixInvalidShortBr(method);

            // Fix DebugInfo
            foreach (var kvp in owner2ins)
            {
                var owner = kvp.Key;
                var ins = kvp.Value;
                owner.Offset = new InstructionOffset(ins);
            }
            // Fix DebugInfo - END

            // Adding DebugInfo to NEW Instructions
            // Adding DebugInfo to NEW Instructions - END

            MarkDirty(method.Module.Assembly.Name.Name);
        }

        internal static void CalculateInstructionOffsets(this MethodDefinition method)
        {
            int offset = 0;
            for (int i = 0; i < method.Body.Instructions.Count; ++i)
            {
                var ins = method.Body.Instructions[i];
                ins.Offset = offset;
                offset += ins.GetSize();
            }
        }
        internal static Dictionary<OpCode, OpCode> _ShortBrToNormalBr = new Dictionary<OpCode, OpCode>()
        {
            { OpCodes.Beq_S, OpCodes.Beq },
            { OpCodes.Bge_S, OpCodes.Bge },
            { OpCodes.Bge_Un_S, OpCodes.Bge_Un },
            { OpCodes.Bgt_S, OpCodes.Bgt },
            { OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
            { OpCodes.Ble_S, OpCodes.Ble },
            { OpCodes.Ble_Un_S, OpCodes.Ble_Un },
            { OpCodes.Blt_S, OpCodes.Blt },
            { OpCodes.Blt_Un_S, OpCodes.Blt_Un },
            { OpCodes.Bne_Un_S, OpCodes.Bne_Un },
            { OpCodes.Brfalse_S, OpCodes.Brfalse },
            { OpCodes.Brtrue_S, OpCodes.Brtrue },
            { OpCodes.Br_S, OpCodes.Br },
            { OpCodes.Leave_S, OpCodes.Leave },
        };
        internal static bool FixInvalidShortBrStep(this MethodDefinition method)
        {
            CalculateInstructionOffsets(method);
            for (int i = 0; i < method.Body.Instructions.Count; ++i)
            {
                var ins = method.Body.Instructions[i];
                if (ins.OpCode.OperandType == OperandType.ShortInlineBrTarget && _ShortBrToNormalBr.ContainsKey(ins.OpCode))
                {
                    var operand = ins.Operand as Instruction;
                    if (operand != null)
                    {
                        var offset = operand.Offset - (ins.Offset + ins.GetSize());
                        if (offset > sbyte.MaxValue || offset < sbyte.MinValue)
                        {
                            ins.OpCode = _ShortBrToNormalBr[ins.OpCode];
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        internal static void FixInvalidShortBr(this MethodDefinition method)
        {
            while (FixInvalidShortBrStep(method)) ;
        }

        internal struct InjectWorkItem
        {
            public MethodDefinition Method;
            public bool Force;

            public InjectWorkItem(MethodDefinition method)
            {
                Method = method;
                Force = false;
            }
            public InjectWorkItem(MethodDefinition method, bool force)
            {
                Method = method;
                Force = force;
            }

            public static implicit operator InjectWorkItem(MethodDefinition method)
            {
                return new InjectWorkItem(method);
            }
            public static implicit operator MethodDefinition(InjectWorkItem item)
            {
                return item.Method;
            }
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> AddWork(this Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, string asm, string typestr, InjectWorkItem item)
        {
            var method = item.Method;
            // we do the filter here.
            // 1. we only inject public methods
            if (!method.IsPublic && !item.Force)
            { // TODO: if it is private Component.Update, we should inject
                return works;
            }
            // 2. the method must have body
            if (!method.HasBody)
            {
                return works;
            }
            // 3. Ignore ctor of UnityEngine.Object
            if (method.IsConstructor && !method.IsStatic && method.DeclaringType.IsSubclassOf("UnityEngine.Object"))
            {
                return works;
            }

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
            //if (!list.Contains(method))
            {
                list.Add(method);
            }
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
                var allasms = new List<LoadedAssembly>(_LoadedAsms.Values);
                for (int i = 0; i < allasms.Count; ++i)
                {
                    var asm = allasms[i];
                    if (asm != null && asm.Asm != null && !luadeps.Contains(asm.Asm.Name.Name))
                    {
                        var methods = GetHotFixMethods(asm.Asm);
                        foreach (var item in methods)
                        {
                            var method = item.Method;
                            var typestr = ReflectAnalyzer.GetIDString(method.DeclaringType);
                            var token = typestr + " " + ReflectAnalyzer.GetIDString(method);
                            if (memberset.Add(token))
                            {
                                typeCache[typestr] = method.DeclaringType;
                                AddWork(rv, asm.Asm.Name.Name, typestr, item);
                            }
                        }
                    }
                }
            }
            Dictionary<string, bool> allMemberTypes = new Dictionary<string, bool>();
            Dictionary<string, System.Text.RegularExpressions.Regex> regexMemberTypes = new Dictionary<string, System.Text.RegularExpressions.Regex>();
            if (methodNames != null)
            {
                foreach (var mname in methodNames)
                {
                    if (!string.IsNullOrEmpty(mname))
                    {
                        if (mname.StartsWith("type "))
                        {
                            var typestr = mname.Substring("type ".Length);
                            allMemberTypes[typestr] = false;
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
                                    allMemberTypes[typestr] = false;
                                }
                                else if (sig == "**")
                                {
                                    allMemberTypes[typestr] = true;
                                }
                                else if (sig.StartsWith("^"))
                                {
                                    try
                                    {
                                        var regex = new System.Text.RegularExpressions.Regex(sig);
                                        regexMemberTypes[typestr] = regex;
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogException(e);
                                    }
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
                                                            AddWork(rv, type.Module.Assembly.Name.Name, typestr, new InjectWorkItem(method, true));
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
                                                            AddWork(rv, type.Module.Assembly.Name.Name, typestr, new InjectWorkItem(method, true));
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
                                                    AddWork(rv, type.Module.Assembly.Name.Name, typestr, new InjectWorkItem(method, true));
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
            foreach (var kvp in allMemberTypes)
            {
                var typestr = kvp.Key;
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
                            AddWork(rv, type.Module.Assembly.Name.Name, typestr, new InjectWorkItem(method, kvp.Value));
                        }
                    }
                }
            }
            foreach (var kvp in regexMemberTypes)
            {
                var typestr = kvp.Key;
                var regex = kvp.Value;
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
                        if (regex.IsMatch(sig))
                        {
                            var token = typestr + " " + sig;
                            if (memberset.Add(token))
                            {
                                AddWork(rv, type.Module.Assembly.Name.Name, typestr, new InjectWorkItem(method, true));
                            }

                        }
                    }
                }
            }

            return RemoveWorksInBlackList(rv, methodNames, searchAttribute);
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> RemoveWork(this Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, TypeDefinition type)
        {
            var asmname = type.Module.Assembly.Name.Name;
            var typestr = ReflectAnalyzer.GetIDString(type);
            Dictionary<string, List<MethodDefinition>> asmdict;
            if (works.TryGetValue(asmname, out asmdict))
            {
                asmdict.Remove(typestr);
            }
            return works;
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> RemoveWork(this Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, MethodDefinition method)
        {
            var asmname = method.Module.Assembly.Name.Name;
            var typestr = ReflectAnalyzer.GetIDString(method.DeclaringType);
            Dictionary<string, List<MethodDefinition>> asmdict;
            if (works.TryGetValue(asmname, out asmdict))
            {
                List<MethodDefinition> methodlist;
                if (asmdict.TryGetValue(typestr, out methodlist))
                {
                    methodlist.Remove(method);
                }
            }
            return works;
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> RemoveWorksInBlackList(Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, IList<string> methodNames, bool searchAttribute)
        {
            if (works == null)
            {
                return works;
            }
            var luadeps = LuaHotFixCodeInjector.GetLuaDeps();
            if (searchAttribute)
            {
                var allasms = new List<LoadedAssembly>(_LoadedAsms.Values);
                for (int i = 0; i < allasms.Count; ++i)
                {
                    var asm = allasms[i];
                    if (asm != null && asm.Asm != null && !luadeps.Contains(asm.Asm.Name.Name))
                    {
                        foreach (var module in asm.Asm.Modules)
                        {
                            foreach (var type in module.Types)
                            {
                                RemoveWorksInAttibuteBlackListInType(works, type);
                            }
                        }
                    }
                }
            }
            Dictionary<string, TypeDefinition> typeCache = new Dictionary<string, TypeDefinition>();
            Dictionary<string, System.Text.RegularExpressions.Regex> regexMemberTypes = new Dictionary<string, System.Text.RegularExpressions.Regex>();
            if (methodNames != null)
            {
                foreach (var mname in methodNames)
                {
                    if (!string.IsNullOrEmpty(mname))
                    {
                        if (mname.StartsWith("--type "))
                        {
                            var typestr = mname.Substring("--type ".Length);
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
                                RemoveWork(works, type);
                            }
                        }
                        else if (mname.StartsWith("--member "))
                        {
                            var parts = mname.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts != null && parts.Length >= 5)
                            {
                                var typestr = parts[3];
                                var sig = parts[4];
                                if (sig == "*" || sig == "**")
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
                                        RemoveWork(works, type);
                                    }
                                }
                                else if (sig.StartsWith("^"))
                                {
                                    try
                                    {
                                        var regex = new System.Text.RegularExpressions.Regex(sig);
                                        regexMemberTypes[typestr] = regex;
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogException(e);
                                    }
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
                                                        RemoveWork(works, method);
                                                    }
                                                }
                                                {
                                                    var method = prop.SetMethod;
                                                    if (method != null)
                                                    {
                                                        RemoveWork(works, method);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (parts[1] == "func" || parts[1] == "ctor")
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
                                                RemoveWork(works, method);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (var kvp in regexMemberTypes)
            {
                var typestr = kvp.Key;
                var regex = kvp.Value;
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
                        if (regex.IsMatch(sig))
                        {
                            RemoveWork(works, method);
                        }
                    }
                }
            }
            return works;
        }
        internal static Dictionary<string, Dictionary<string, List<MethodDefinition>>> RemoveWorksInAttibuteBlackListInType(Dictionary<string, Dictionary<string, List<MethodDefinition>>> works, TypeDefinition type)
        {
            if (HasHotFixForbiddenAttribute(type))
            {
                RemoveWork(works, type);
            }
            else
            {
                foreach (var prop in type.Properties)
                {
                    if (HasHotFixForbiddenAttribute(prop))
                    {
                        if (prop.GetMethod != null)
                        {
                            RemoveWork(works, prop.GetMethod);
                        }
                        if (prop.SetMethod != null)
                        {
                            RemoveWork(works, prop.SetMethod);
                        }
                    }
                }
                foreach (var method in type.Methods)
                {
                    if (HasHotFixForbiddenAttribute(method))
                    {
                        RemoveWork(works, method);
                    }
                }
                // TODO: events?
            }
            foreach (var ntype in type.NestedTypes)
            {
                RemoveWorksInAttibuteBlackListInType(works, ntype);
            }
            return works;
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

        public static void MarkInjected()
        {
            string asm1, asm2;
            UnityAssemblyUtils.GetDefaultScriptAssemblyName(out asm1, out asm2);
            var asmlast = asm2 ?? asm1;
            var dll = GetOrLoadAssembly(asmlast);
            var marktype = dll.MainModule.GetType("<Indicator_Injected>");
            if (marktype == null)
            {
                var objectType = dll.MainModule.TypeSystem.Object;
                var injecttype = new TypeDefinition("", "<Indicator_Injected>", TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, objectType);
                dll.MainModule.Types.Add(injecttype);
                MarkDirty(asmlast);
            }
        }

        private static bool? _IsInjectedMarked;
        public static bool IsInjectedMarked()
        {
            if (_IsInjectedMarked == null)
            {
                string asm1, asm2;
                UnityAssemblyUtils.GetDefaultScriptAssemblyName(out asm1, out asm2);
                var asmlast = asm2 ?? asm1;
                var asms = System.AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; ++i)
                {
                    var asm = asms[i];
                    if (asm.GetName().Name == asmlast)
                    {
                        _IsInjectedMarked = asm.GetType("<Indicator_Injected>") != null;
                        break;
                    }
                }
            }
            return (bool)_IsInjectedMarked;
        }
    }
}