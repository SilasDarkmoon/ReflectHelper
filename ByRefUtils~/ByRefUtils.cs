using System;

namespace Capstones.UnityEngineEx
{
    public static class ByRefUtils
    {
        public class Ref<T>
        {
            public ref T GetRef()
            {
                throw new NotImplementedException();
            }
            public void SetRef(ref T r) { }

            public T Value
            {
                get { return GetRef(); }
                set { GetRef() = value; }
            }
        }

        public static bool RefEquals<T>(ref T a, ref T b)
        {
            return false;
        }

        public static ref T GetEmptyRef<T>()
        {
            throw new NotImplementedException();
        }

        public static bool IsEmpty<T>(ref T r)
        {
            return RefEquals(ref r, ref GetEmptyRef<T>());
        }
    }
}
