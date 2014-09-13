using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DevSnicket.ProcessSpawning
{
	internal static class Program
	{
		internal static void Main(string[] arguments)
		{
			if (!TryInvokeFromProcess(arguments))
			{
				Console.WriteLine("Enter a number and press the enter key to calculate primes:");

				Console.WriteLine(
					String.Join(
						", ",
						InvokeInProcess(
							CalcuatePrimeNumbers,
							Int32.Parse(Console.ReadLine()))));
			}
		}

		public static IReadOnlyCollection<Int32> CalcuatePrimeNumbers(Int32 max)
		{
			return
				Enumerable.Range(2, max - 1)
				.Where(i => Enumerable.Range(2, i - 2).All(j => i % j != 0))
				.ToArray();
		}

		private static Boolean TryInvokeFromProcess(
			IReadOnlyCollection<String> arguments)
		{
			if (arguments.Count != 1 || arguments.First() != _processArgument)
				return false;
			else
			{
				MethodAndParameter methodAndParameter;

				using (Stream input = Console.OpenStandardInput())
					methodAndParameter =
						Deserialise<MethodAndParameter>(input);

				using (Stream output = Console.OpenStandardOutput())
					Serialise(
						output:
							output,
						value:
							methodAndParameter.Method.Invoke(
								null,
								new[] { methodAndParameter.Parameter, }));

				return true;
			}
		}

		private static TResult InvokeInProcess<TParameter, TResult>(
			Func<TParameter, TResult> method,
			TParameter parameter)
		{
			using (Process process = CreateProcess())
			{
				process.Start();
				
				if (Debugger.IsAttached)
					AttachDebuggerToProcessId(process.Id);

				using (var output = new MemoryStream())
				{
					Serialise(
						process.StandardInput.BaseStream,
						new MethodAndParameter
						{
							Method = method.Method,
							Parameter = parameter,
						});

					while (!process.HasExited)
						process.StandardOutput.BaseStream.CopyTo(output);

					output.Position = 0;

					return Deserialise<TResult>(output);
				}
			}
		}

		private static T Deserialise<T>(Stream stream)
		{
			return (T)new BinaryFormatter().Deserialize(stream);
		}

		private static void Serialise(
			Stream output,
			Object value)
		{
			new BinaryFormatter().Serialize(output, value);
		}

		[Serializable]
		private class MethodAndParameter
		{
			public MethodInfo Method { get; set; }
			public Object Parameter { get; set; }
		}

		private static Process CreateProcess()
		{
			var process = new Process();

			try
			{
				process.StartInfo =
					new ProcessStartInfo(
						arguments: _processArgument,
						fileName: Assembly.GetExecutingAssembly().Location)
					{
						RedirectStandardInput = true,
						RedirectStandardOutput = true,
						UseShellExecute = false,
					};

				return process;
			}
			catch
			{
				process.Dispose();
				throw;
			}
		}

		private const String _processArgument = "RunSubprocess";

		private static void AttachDebuggerToProcessId(
			Int32 id)
		{
			Boolean isAttached = false;
			
			do
			{
				try
				{
					((EnvDTE.DTE)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE.12.0"))
					.Debugger
					.LocalProcesses
					.Cast<EnvDTE.Process>()
					.Single(visualStudioProcess => visualStudioProcess.ProcessID == id)
					.Attach();

					isAttached = true;
				}
				catch (COMException e)
				{
					if (!e.Message.StartsWith("The message filter indicated that the application is busy."))
						throw;
				}
			}
			while (!isAttached);
		}
	}
}
