using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AssemblyInitializerGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var asm = AssemblyDefinition.ReadAssembly("AssemblyInitializer.dll");
            var type = asm.MainModule.GetType("<Module>");
            var cctor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Private | MethodAttributes.HideBySig, asm.MainModule.TypeSystem.Void);

            var tref = asm.MainModule.GetType("Capstones.AssemblyInitializer.AssemblyInitializerUtils");
            MethodDefinition mdef = null;
            foreach (var m in tref.Methods)
            {
                if (m.Name == "Init")
                {
                    mdef = m;
                    break;
                }
            }
            cctor.Body = new MethodBody(cctor);
            var emitter = cctor.Body.GetILProcessor();
            emitter.Emit(OpCodes.Call, mdef);
            emitter.Emit(OpCodes.Ret);
            type.Methods.Add(cctor);

            asm.Write("AssemblyInitializer2.dll");
            asm.Dispose();

        }
    }
}
