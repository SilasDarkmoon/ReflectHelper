using System;

namespace TestAssemblyInitializer
{
    public class Initializer : Capstones.AssemblyInitializer.IAssemblyInitializer
    {
        public void Init()
        {
            Console.WriteLine("IAssemblyInitializer.Init");
        }
    }

    class Program
    {
        static Program() { }
        static Capstones.AssemblyInitializer.AssemblyInitializerDummy _D = new Capstones.AssemblyInitializer.AssemblyInitializerDummy();

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
