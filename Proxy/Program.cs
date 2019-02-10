using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
	public interface ITest: IDisposable
	{
		void Test();
	}

	class Test : ITest
	{
		public void Dispose()
		{
			Console.WriteLine("Disposed");
		}

		void ITest.Test()
		{
			Console.WriteLine("Test");
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			var hub = new ProxyHub<ITest>(() => new Test());

			var ref1 = hub.Value;
			var ref2 = hub.Value;

			ref1.Test();
			ref2.Test();

			ref1.Dispose();
			ref2.Dispose();

			Console.ReadLine();
		}
	}
}
