using System;
using System.Reflection;

namespace Capstones.AssemblyInitializer
{
    public interface IAssemblyInitializer
    {
        void Init();
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AssemblyInitializerAttribute : Attribute
    { }

    public class AssemblyInitializerUtils
    {
        public static void Init()
        {
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; ++i)
            {
                var asm = asms[i];
                InitAssembly(asm);
            }
            AppDomain.CurrentDomain.AssemblyLoad += (sender, e) =>
            {
                InitAssembly(e.LoadedAssembly);
            };
        }
        public static void InitAssembly(Assembly asm)
        {
            var types = asm.GetTypes();
            for (int j = 0; j < types.Length; ++j)
            {
                var type = types[j];
                if (typeof(IAssemblyInitializer).IsAssignableFrom(type))
                {
                    try
                    {
                        var inst = Activator.CreateInstance(type) as IAssemblyInitializer;
                        inst.Init();
                    }
                    catch (Exception e)
                    {
                        // TODO: report this exception
                    }
                }
            }
        }
    }

    public struct AssemblyInitializerDummy
    { }
}
