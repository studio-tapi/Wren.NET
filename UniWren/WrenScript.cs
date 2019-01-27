using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Wren.Core.Objects;
using Wren.Core.VM;

namespace UniWren
{
	public class WrenScript
	{
		const string WrenScriptLibSource =
			"class Script {}\n" +
			"class ScriptRef {}\n";

		static IEnumerator Noop()
		{
			yield break;
		}

		public static void LoadLibrary<T>( WrenVM vm )
			where T : IEnumerator
		{
			vm.Interpret( "", "", WrenScriptLibSource );

			ObjClass scriptClass = (ObjClass)vm.FindVariable( "Script" );
			ObjClass scriptRefClass = (ObjClass)vm.FindVariable( "ScriptRef" );

			List<string> underscores = new List<string>();
			List<int> registeredArities = new List<int>();
			foreach( var type in Assembly.GetCallingAssembly().GetTypes() )
			{
				if( typeof( T ).IsAssignableFrom( type ) )
				{
					var attrs = type.GetCustomAttributes( true );
					foreach( var attr in attrs )
					{
						var scriptName = attr as WrenNameAttribute;
						if( scriptName != null )
						{
							registeredArities.Clear();
							foreach( var ctor in type.GetConstructors() )
							{
								bool ignore = false;
								foreach( var ctorAttr in ctor.GetCustomAttributes( true ) )
								{
									if( ctorAttr is WrenIgnoreAttribute )
									{
										ignore = true;
									}
								}

								if( !ignore )
								{
									var constructor = ctor; // capture a local cuz we're in a closure
									var parameters = constructor.GetParameters();

									if( registeredArities.Contains( parameters.Length ) )
									{
										throw new Exception( "Wren doesn't understand typed signatures so wren constructable classes shouldn't contain constructors with the same number of signatures" );
									}

									registeredArities.Add( parameters.Length );

									underscores.Clear();
									foreach( var parameter in parameters )
									{
										underscores.Add( "_" );
									}

									var signature = scriptName.name + "(" + string.Join( ",", underscores ) + ")";
									vm.Primitive( scriptRefClass.ClassObj, signature, ( primVM, args, stackStart ) =>
									{
										try
										{
											object[] constructorArgs = new object[parameters.Length];
											for( int i = 0; i < parameters.Length; i++ )
											{
												// Convert.ChangeType is mostly because numbers in wren are always doubles, but it might catch other cool stuff
												var arg = args[stackStart + i + 1].Unbox();
												constructorArgs[i] = arg is IConvertible ? Convert.ChangeType( arg, parameters[i].ParameterType ) : arg;
											}

											args[stackStart] = new ObjForeign() { foreign = constructor.Invoke( constructorArgs ) };
											return true;
										}
										catch( Exception e )
										{
											primVM.Fiber.Error = Obj.MakeString( "Couldn't construct " + constructor.Name + ": " + e.Message );
											return false;
										}
									} );

									vm.Primitive( scriptClass.ClassObj, signature, ( primVM, args, stackStart, success ) =>
									{
										try
										{
											object[] constructorArgs = new object[parameters.Length];
											for( int i = 0; i < parameters.Length; i++ )
											{
												var arg = args[stackStart + i + 1].Unbox();
												constructorArgs[i] = arg is IConvertible ? Convert.ChangeType( arg, parameters[i].ParameterType ) : arg;
											}

											var script = constructor.Invoke( constructorArgs );

											success.value = true;
											return script as IEnumerator;
										}
										catch( Exception e )
										{
											primVM.Fiber.Error = Obj.MakeString( "Couldn't construct " + constructor.Name + ": " + e.Message );
											success.value = false;
											return Noop();
										}
									} );
								}
							}
						}
					}
				}
			}
		}
	}
}
