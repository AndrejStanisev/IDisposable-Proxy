using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
	public interface IProxyHub<T> where T: IDisposable
	{
		T Value { get; }
	}

	public class ProxyHub<T> : IProxyHub<T> where T : class, IDisposable
	{
		private readonly Action _onDisposed;
		private readonly Func<T> _producer;

		private T _instance;
		private bool _isDisposed; 
		private int _refCount;
		
		static ProxyHub()
		{
			EnsureInterface(typeof(T));

			void EnsureInterface(Type type)
			{
				if (!type.IsInterface) throw new ArgumentException($"Type {type} is not an interface");
			}
		}

		public ProxyHub(Func<T> producer, Action onDisposed = null)
		{
			_producer = producer;
			_onDisposed = onDisposed;
		}

		public T Value
		{
			get
			{
				EnsureNotDisposed();

				if(_instance == null)
				{
					_instance = _producer();
				}

				_refCount++;

				var reference = ProxyGenerator<T>.CreateInstance(_instance, RefDispose);
				return reference;
			}
		}

		private void EnsureNotDisposed()
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(Proxy));
		}

		private void RefDispose()
		{
			_refCount--;

			if(_refCount == 0)
			{
				_isDisposed = true;
				_instance.Dispose();
				_onDisposed?.Invoke();
			}
		}
	}
}
