using Wren.Core.VM;

namespace Wren.Core.Objects
{
	public class ObjForeign : Obj
	{
		// c# object this is wrapping
		public object foreign;

		public bool Is<T>()
		{
			return foreign is T;
		}

		public T As<T>()
		{
			if( foreign is T )
			{
				return (T)foreign;
			}
			else
			{
				return default( T );
			}
		}

        public ObjForeign()
        {
            ClassObj = WrenVM.ForeignClass;
        }
	}
}
