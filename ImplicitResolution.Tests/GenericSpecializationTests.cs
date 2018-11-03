using System;
using Fody;
using ImplicitResolution.AssemblyToProcess;
using ImplicitResolution.Fody;
using Xunit;

namespace ImplicitResolution.Tests
{
    public class GenericSpecializationTests
    {
        private static readonly dynamic Instance;

        static GenericSpecializationTests()
        {
            var weavingTask = new GenericSpecialization.Fody.ModuleWeaver();
            var testResult = weavingTask.ExecuteTestRun(
                "C:\\Users\\miair\\RiderProjects\\ImplicitResolution\\ImplicitResolution.Tests\\bin\\Debug\\net462\\ImplicitResolution.AssemblyToProcess.dll",
                false);
            
            var type = testResult.Assembly.GetType("ImplicitResolution.AssemblyToProcess.GenericSpecializationTest");
            Instance = Activator.CreateInstance(type);
        }
        
        [Fact]
        public void Method_AcceptsString() 
            => Instance.Method_AcceptsString();
        
        [Fact]
        public void Method_AcceptsString_ReturnsString() 
            => Assert.Equal("Test", Instance.Method_AcceptsString_ReturnsString("Test"));
        
        [Fact]
        public void Method_AcceptsTwoStrings_ReturnsEquality()
        {
            Assert.False(Instance.Method_AcceptsTwoStrings_ReturnsEquality("Test1", "Test2"));
            Assert.True(Instance.Method_AcceptsTwoStrings_ReturnsEquality("Test", "Test"));
        }

        [Fact]
        public void Method_AcceptsInt()
            => Instance.Method_AcceptsInt();
        
        [Fact]
        public void Method_AcceptsInt_ReturnsInt() 
            => Assert.Equal(1337, Instance.Method_AcceptsInt_ReturnsInt(1337));
        
        [Fact]
        public void Method_AcceptsTwoInts_ReturnsEquality()
        {
            Assert.False(Instance.Method_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(Instance.Method_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
    }
}