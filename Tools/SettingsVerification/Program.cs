using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Host execution of the SAME pure NUnit tests imported by Unity EditMode.
// Only ordinary [Test]/[SetUp] tests are supported here, not UnityTest or scene tests.
internal static class Program
{
    private static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "StudentGrowth.Tests"))
            foreach (var test in type.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
            {
                try
                {
                    var fixture = Activator.CreateInstance(type);
                    foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<SetUpAttribute>() != null)) setup.Invoke(fixture, null);
                    test.Invoke(fixture, null);
                    Console.WriteLine("PASS " + test.Name); passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FAIL " + test.Name + ": " + (ex.InnerException ?? ex)); failed++;
                }
            }
        Console.WriteLine($"Core NUnit tests on .NET host: passed={passed}, failed={failed}. Unity/Player UI tests are separate.");
        return failed == 0 ? 0 : 1;
    }
}
