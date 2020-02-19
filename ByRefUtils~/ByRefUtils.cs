using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Capstones.UnityEngineEx
{
    public static class ByRefUtils
    {
        public interface IRef
        {
            IntPtr Raw { get; }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RawRef : IRef
        {
            private IntPtr _Ref;
            public IntPtr Raw { get { return _Ref; } }
            public static implicit operator IntPtr(RawRef r)
            {
                return r._Ref;
            }
            public override bool Equals(object obj)
            {
                if (obj is IRef r)
                {
                    return _Ref == r.Raw;
                }
                else if (obj is IntPtr p)
                {
                    return _Ref == p;
                }
                return false;
            }
            public override int GetHashCode()
            {
                return _Ref.GetHashCode();
            }
            public static bool operator ==(RawRef r1, RawRef r2)
            {
                return r1._Ref == r2._Ref;
            }
            public static bool operator !=(RawRef r1, RawRef r2)
            {
                return r1._Ref != r2._Ref;
            }
            public static bool operator ==(RawRef r1, IntPtr p2)
            {
                return r1._Ref == p2;
            }
            public static bool operator !=(RawRef r1, IntPtr p2)
            {
                return r1._Ref != p2;
            }
            public static bool operator ==(IntPtr p1, RawRef r2)
            {
                return p1 == r2._Ref;
            }
            public static bool operator !=(IntPtr p1, RawRef r2)
            {
                return p1 != r2._Ref;
            }
            public static bool operator ==(RawRef r1, IRef r2)
            {
                return r1._Ref == r2.Raw;
            }
            public static bool operator !=(RawRef r1, IRef r2)
            {
                return r1._Ref != r2.Raw;
            }
            public static bool operator ==(IRef r1, RawRef r2)
            {
                return r1.Raw == r2._Ref;
            }
            public static bool operator !=(IRef r1, RawRef r2)
            {
                return r1.Raw != r2._Ref;
            }

            public ref T GetRef<T>()
            {
                throw new NotImplementedException();
            }
            public void SetRef<T>(ref T r)
            {
                throw new NotImplementedException();
            }
            public T GetValue<T>()
            {
                return GetRef<T>();
            }
            public void SetValue<T>(T value)
            {
                GetRef<T>() = value;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public sealed class Ref : IRef
        {
            private RawRef _Ref = new RawRef();
            public IntPtr Raw { get { return _Ref.Raw; } }
            public static implicit operator IntPtr(Ref r)
            {
                return r._Ref;
            }
            public override bool Equals(object obj)
            {
                if (obj is IRef r)
                {
                    return _Ref == r.Raw;
                }
                else if (obj is IntPtr p)
                {
                    return _Ref == p;
                }
                return false;
            }
            public override int GetHashCode()
            {
                return _Ref.GetHashCode();
            }
            public static bool operator ==(Ref r1, Ref r2)
            {
                return r1._Ref == r2._Ref;
            }
            public static bool operator !=(Ref r1, Ref r2)
            {
                return r1._Ref != r2._Ref;
            }
            public static bool operator ==(Ref r1, IntPtr p2)
            {
                return r1._Ref == p2;
            }
            public static bool operator !=(Ref r1, IntPtr p2)
            {
                return r1._Ref != p2;
            }
            public static bool operator ==(IntPtr p1, Ref r2)
            {
                return p1 == r2._Ref;
            }
            public static bool operator !=(IntPtr p1, Ref r2)
            {
                return p1 != r2._Ref;
            }
            public static bool operator ==(Ref r1, IRef r2)
            {
                return r1._Ref == r2.Raw;
            }
            public static bool operator !=(Ref r1, IRef r2)
            {
                return r1._Ref != r2.Raw;
            }
            public static bool operator ==(IRef r1, Ref r2)
            {
                return r1.Raw == r2._Ref;
            }
            public static bool operator !=(IRef r1, Ref r2)
            {
                return r1.Raw != r2._Ref;
            }

            public ref T GetRef<T>()
            {
                return ref _Ref.GetRef<T>();
            }
            public void SetRef<T>(ref T r)
            {
                _Ref.SetRef<T>(ref r);
            }
            public T GetValue<T>()
            {
                return _Ref.GetValue<T>();
            }
            public void SetValue<T>(T value)
            {
                _Ref.SetValue<T>(value);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public sealed class Ref<T> : IRef
        {
            private RawRef _Ref = new RawRef();
            public IntPtr Raw { get { return _Ref.Raw; } }
            public static implicit operator IntPtr(Ref<T> r)
            {
                return r._Ref;
            }
            public override bool Equals(object obj)
            {
                if (obj is IRef r)
                {
                    return _Ref == r.Raw;
                }
                else if (obj is IntPtr p)
                {
                    return _Ref == p;
                }
                return false;
            }
            public override int GetHashCode()
            {
                return _Ref.GetHashCode();
            }
            public static bool operator ==(Ref<T> r1, Ref<T> r2)
            {
                return r1._Ref == r2._Ref;
            }
            public static bool operator !=(Ref<T> r1, Ref<T> r2)
            {
                return r1._Ref != r2._Ref;
            }
            public static bool operator ==(Ref<T> r1, IntPtr p2)
            {
                return r1._Ref == p2;
            }
            public static bool operator !=(Ref<T> r1, IntPtr p2)
            {
                return r1._Ref != p2;
            }
            public static bool operator ==(IntPtr p1, Ref<T> r2)
            {
                return p1 == r2._Ref;
            }
            public static bool operator !=(IntPtr p1, Ref<T> r2)
            {
                return p1 != r2._Ref;
            }
            public static bool operator ==(Ref<T> r1, IRef r2)
            {
                return r1._Ref == r2.Raw;
            }
            public static bool operator !=(Ref<T> r1, IRef r2)
            {
                return r1._Ref != r2.Raw;
            }
            public static bool operator ==(IRef r1, Ref<T> r2)
            {
                return r1.Raw == r2._Ref;
            }
            public static bool operator !=(IRef r1, Ref<T> r2)
            {
                return r1.Raw != r2._Ref;
            }

            public ref T GetRef()
            {
                return ref _Ref.GetRef<T>();
            }
            public void SetRef(ref T r)
            {
                _Ref.SetRef<T>(ref r);
            }

            public T Value
            {
                get { return _Ref.GetValue<T>(); }
                set { _Ref.SetValue<T>(value); }
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
