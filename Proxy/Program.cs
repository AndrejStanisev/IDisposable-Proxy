using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
	public interface I1
	{
		void Test();
	}

	public interface I2: I1
	{
		new void Test();
		void TestGeneric<R>(R nesto);
	}

	class Test : I2
	{
		public void TestGeneric<R>(R nesto)
		{
			Console.WriteLine("Generic");
		}

		void I2.Test()
		{
			Console.WriteLine("T2");
		}

		void I1.Test()
		{
			Console.WriteLine("T1");
		}

		public override string ToString()
		{
			return "TosStringT";
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			var list = new List<int>() { 1, 2, 3 };

			var test = new Test();
			var proxy = ProxyGenerator<I2>.CreateInstance(test);

			Console.WriteLine(proxy.ToString());

			proxy.TestGeneric(50);

			//proxy.Add(10);

			//foreach (var i in proxy)
			//{
			//	Console.WriteLine(i);
			//}

			Console.ReadLine();
		}
	}
}
