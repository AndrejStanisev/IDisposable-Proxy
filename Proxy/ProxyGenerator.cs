using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
	static class ProxyGenerator<T> where T: class
	{
		private static readonly Type _originalType = typeof(T);
		private static readonly Type _proxyType;

		private static readonly FieldBuilder _original;
		private static readonly FieldBuilder _isDisposed;
		private static readonly FieldBuilder _disposeCallback;

		static ProxyGenerator()
		{
			EnsureInterface();

			var assemblyName = new AssemblyName("Proxy");
			var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule("Module");

			//We want to implement all the interfaces in the hierarchy
			var interfaces = GetInterfaces(_originalType).ToArray();

			var typeBuilder = moduleBuilder.DefineType($"{typeof(T)}#Proxy", TypeAttributes.Public, typeof(object), interfaces);

			_original = typeBuilder.DefineField(nameof(_original), _originalType, FieldAttributes.Private);
			_isDisposed = typeBuilder.DefineField(nameof(_isDisposed), typeof(bool), FieldAttributes.Private);
			_disposeCallback = typeBuilder.DefineField(nameof(_disposeCallback), typeof(Action), FieldAttributes.Private);

			_proxyType = GenerateType(typeBuilder);
		}

		public static T CreateInstance(T original, Action callback) => (T) Activator.CreateInstance(_proxyType, new object[] { original, callback });

		private static void EnsureInterface()
		{
			if (!_originalType.IsInterface) throw new ArgumentException($"Type {_originalType} is not an interface");
		}

		private static Type GenerateType(TypeBuilder typeBuilder)
		{
			GenerateConstructor(typeBuilder);

			var interfaceMethods = GetMethodsFromInterfaces(_originalType);
			GenerateMethods(typeBuilder, interfaceMethods.Where(x => x.DeclaringType != typeof(IDisposable)), true);

			var objectMethods = typeof(object).GetMethods();
			GenerateMethods(typeBuilder, objectMethods, false);

			GenerateDispose(typeBuilder);

			return typeBuilder.CreateType();
		}

		private static void GenerateDispose(TypeBuilder typeBuilder)
		{
			var dispose = typeBuilder.DefineMethod("Dispose", MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis);
			var il = dispose.GetILGenerator();

			var notDisposedLabel = il.DefineLabel();

			EmitDisposedCheck(il, notDisposedLabel);

			il.MarkLabel(notDisposedLabel);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Stfld, _isDisposed);

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, _disposeCallback);
			il.Emit(OpCodes.Callvirt, typeof(Action).GetMethod(nameof(Action.Invoke)));
			il.Emit(OpCodes.Ret);

			typeBuilder.DefineMethodOverride(dispose, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose)));
		}

		private static void EmitDisposedCheck(ILGenerator il, Label notDisposedLabel)
		{
			var exceptionConstructor = typeof(ObjectDisposedException).GetConstructor(new Type[] { typeof(string) });

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, _isDisposed);
			il.Emit(OpCodes.Brfalse, notDisposedLabel);

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, _original);
			il.Emit(OpCodes.Call, typeof(object).GetMethod(nameof(object.GetType)));
			il.Emit(OpCodes.Callvirt, typeof(Type).GetProperty(nameof(Type.Name)).GetGetMethod());

			il.Emit(OpCodes.Newobj, exceptionConstructor);
			il.Emit(OpCodes.Throw);
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

				var notDisposedLabel = il.DefineLabel();

				EmitDisposedCheck(il, notDisposedLabel);

				il.MarkLabel(notDisposedLabel);
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
			var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { _originalType, typeof(Action) });
			var il = constructor.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Stfld, _original);

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Stfld, _disposeCallback);

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
