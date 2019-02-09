using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
	class ProxyGenerator<T> where T: class
	{
		private static readonly Type _originalType = typeof(T);
		private static readonly Type _proxyType;

		private static FieldBuilder _original;

		static ProxyGenerator()
		{
			EnsureInterface();

			var assemblyName = new AssemblyName("Proxy");
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule("Module", "module.dll");

			//We want to implement all the interfaces in the hierarchy
			var interfaces = GetInterfaces(_originalType).ToArray();

			var typeBuilder = moduleBuilder.DefineType($"{typeof(T)}#Proxy", TypeAttributes.Public, typeof(object), interfaces);
			_proxyType = GenerateType(typeBuilder);
			
			assemblyBuilder.Save(@"assembly.dll");
		}

		public static T CreateInstance(T original) => (T) Activator.CreateInstance(_proxyType, new object[] { original });

		private static void EnsureInterface()
		{
			if (!_originalType.IsInterface) throw new ArgumentException($"Type {_originalType} is not an interface");
		}

		private static Type GenerateType(TypeBuilder typeBuilder)
		{
			_original = typeBuilder.DefineField("_original", _originalType, FieldAttributes.Private);

			GenerateConstructor(typeBuilder);

			var interfaceMethods = GetMethodsFromInterfaces(_originalType);
			GenerateMethods(typeBuilder, interfaceMethods, true);

			var objectMethods = typeof(object).GetMethods();
			GenerateMethods(typeBuilder, objectMethods, false);

			return typeBuilder.CreateType();
		}

		private static void GenerateMethods(TypeBuilder typeBuilder, IEnumerable<MethodInfo> originalMethods, bool defineOverride)
		{
			foreach (var o in originalMethods)
			{
				var parameterTypes = o.GetParameters().Select(x => x.ParameterType).ToArray();

				var methodName = $"{o.DeclaringType.FullName}.{o.Name}";

				var method = typeBuilder.DefineMethod(o.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
					CallingConventions.HasThis, o.ReturnType, parameterTypes);

				if (o.IsGenericMethod)
				{
					var genericParameters = o.GetGenericArguments().Select(x => x.Name).ToArray();
					method.DefineGenericParameters(genericParameters);
				}

				var il = method.GetILGenerator();

				il.EmitWriteLine(methodName);

				//Load the reference to the wrapped object
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldfld, _original);

				//Load all the arguments onto the stack
				for (var i = 1; i <= o.GetParameters().Length; i++)
				{
					il.Emit(OpCodes.Ldarg, i);
				}

				il.Emit(OpCodes.Callvirt, o);
				il.Emit(OpCodes.Ret);

				//Associate the built method with the declaration from the original interface
				//Without this step we would potentially dispatch to wrong method implementations for methods that were hidden with new
				if (defineOverride)
				{
					typeBuilder.DefineMethodOverride(method, o);
				}
			}
		}
	
		private static void GenerateConstructor(TypeBuilder typeBuilder)
		{
			var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { _originalType });
			var il = constructor.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Stfld, _original);
			il.Emit(OpCodes.Ret);
		}

		static IEnumerable<MethodInfo> GetMethodsFromInterfaces(Type type)
		{
			var interfaces = GetInterfaces(type);
			return interfaces.SelectMany(x => x.GetMethods());
		}

		static IEnumerable<Type> GetInterfaces(Type type)
		{
			return GetInterfacesRecursive(type).Append(type).Distinct();

			IEnumerable<Type> GetInterfacesRecursive(Type t)
			{
				IEnumerable<Type> interfaces = t.GetInterfaces();

				foreach (var i in interfaces)
				{
					interfaces = interfaces.Concat(GetInterfacesRecursive(i));
				}

				return interfaces;
			}
		}

	}
}
