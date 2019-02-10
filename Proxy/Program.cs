using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

			void Callback()
			{

			}

			var file = File.Open(@"C:\Users\Andrej\Desktop\nesto.txt", FileMode.Open);
			var proxy = ProxyGenerator<IDisposable>.CreateInstance(file, Callback);

			proxy.Dispose();
			//proxy.ToString();

			Console.ReadLine();
		}
	}
}
