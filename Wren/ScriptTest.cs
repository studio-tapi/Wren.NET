using System;
using System.Collections;
using UniWren;

namespace Wren
{
	public abstract class ScriptTest : IEnumerator
	{
		protected abstract IEnumerator run();

		IEnumerator context = null;

		public object Current => context.Current;

		public bool MoveNext()
		{
			context = context ?? run();
			return context.MoveNext();
		}

		public void Reset()
		{
			throw new NotImplementedException();
		}
	}

	[WrenName( "wrapper" )]
	public class TestWrapper : ScriptTest
	{
		readonly ScriptTest inner;

		public TestWrapper( ScriptTest inner )
		{
			this.inner = inner;
		}

		protected override IEnumerator run()
		{
			Console.WriteLine( "About to run inner" );
			yield return inner;
			Console.WriteLine( "Finished running inner" );
		}
	}

	[WrenName( "countdown" )]
	public class TestCountdown : ScriptTest
	{
		protected override IEnumerator run()
		{
			for( int i = 5; i > 0; i-- )
			{
				Console.WriteLine( i );
				yield return null;
			}
		}
	}
}
