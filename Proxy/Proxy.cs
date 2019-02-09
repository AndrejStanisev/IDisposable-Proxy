using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
	interface IProxyHub<T> where T: IDisposable
	{
		T Value { get; }
	}

	class ProxyHub<T, R> : IProxyHub<R> where R: T, IDisposable where T: IDisposable
	{
		static ProxyHub()
		{
			EnsureInterface(typeof(T));
			EnsureInterface(typeof(R));

			void EnsureInterface(Type type)
			{
				if (!type.IsInterface) throw new ArgumentException($"Type {type} is not an interface");
			}
		}

		private readonly R _instance;
		private int _refCount;

		public R Value
		{
			get
			{
				_refCount++;
				return _instance;
			}
		}

		public ProxyHub(Func<T> producer)
		{
			//_instance = producer();
			_refCount = 1;
		}
	}

	class ProxyHub<T>: ProxyHub<T,T> where T: IDisposable
	{
		public ProxyHub(Func<T> producer) : base(producer) { }
	}
}
