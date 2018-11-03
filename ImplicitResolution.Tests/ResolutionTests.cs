using System;
using Fody;
using ImplicitResolution.Fody;
using Xunit;

namespace ImplicitResolution.Tests
{
    public class ResolutionTests
    {
        private static readonly TestResult TestResult;
        
        static ResolutionTests()
        {
            var weavingTask = new ModuleWeaver();
            TestResult = weavingTask.ExecuteTestRun(
                "C:\\Users\\miair\\RiderProjects\\ImplicitResolution\\ImplicitResolution.Tests\\bin\\Debug\\netcoreapp2.1\\ImplicitResolution.AssemblyToProcess.dll",
                false);
        }
        
        [Fact]
        public void ResolutionResolvesTypeclasses()
        {
            var type = TestResult.Assembly.GetType("ImplicitResolution.AssemblyToProcess.Test");
            var instance = (dynamic)Activator.CreateInstance(type);

            Assert.Equal(true, instance.Run());
            Assert.Equal(true, instance.RunNested());
        }
    }
}