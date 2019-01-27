using System;

namespace UniWren
{
	public class WrenIgnoreAttribute : Attribute { }

	public class WrenNameAttribute : Attribute
	{
		public readonly string name;

		public WrenNameAttribute( string name )
		{
			this.name = name;
		}
	}

}
