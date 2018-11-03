using System;
using Fody;
using ImplicitResolution.AssemblyToProcess;
using ImplicitResolution.Fody;
using Xunit;

namespace ImplicitResolution.Tests
{
    public class GenericSpecializationTests
    {
        private static readonly dynamic SpecializedInstance, NotSpecializedInstance;

        static GenericSpecializationTests()
        {
            var weavingTask = new GenericSpecialization.Fody.ModuleWeaver();
            var testResult = weavingTask.ExecuteTestRun(
                "C:\\Users\\miair\\RiderProjects\\ImplicitResolution\\ImplicitResolution.Tests\\bin\\Debug\\net462\\ImplicitResolution.AssemblyToProcess.dll",
                false);
            
            var specType = testResult.Assembly.GetType("ImplicitResolution.AssemblyToProcess.GenericSpecializationTest_Specialized");
            SpecializedInstance = Activator.CreateInstance(specType);
            
            var notSpecType = testResult.Assembly.GetType("ImplicitResolution.AssemblyToProcess.GenericSpecializationTest_NotSpecialized");
            NotSpecializedInstance = Activator.CreateInstance(notSpecType);
        }
        
        [Fact]
        public void Specialized_Method_AcceptsString() 
            => SpecializedInstance.Method_AcceptsString();
        
        [Fact]
        public void Specialized_Method_AcceptsString_ReturnsString() 
            => Assert.Equal("Test", SpecializedInstance.Method_AcceptsString_ReturnsString("Test"));
        
        [Fact]
        public void Specialized_Method_AcceptsTwoStrings_ReturnsEquality()
        {
            Assert.False(SpecializedInstance.Method_AcceptsTwoStrings_ReturnsEquality("Test1", "Test2"));
            Assert.True(SpecializedInstance.Method_AcceptsTwoStrings_ReturnsEquality("Test", "Test"));
        }

        [Fact]
        public void Specialized_Method_AcceptsInt()
            => SpecializedInstance.Method_AcceptsInt();
        
        [Fact]
        public void Specialized_Method_AcceptsInt_ReturnsInt() 
            => Assert.Equal(1337, SpecializedInstance.Method_AcceptsInt_ReturnsInt(1337));
        
        [Fact]
        public void Specialized_Method_AcceptsTwoInts_ReturnsEquality()
        {
            Assert.False(SpecializedInstance.Method_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(SpecializedInstance.Method_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
        
        [Fact]
        public void Specialized_StructuralMethod_AcceptsInt_ReturnsInt() 
            => Assert.Equal(1337, SpecializedInstance.StructuralMethod_AcceptsInt_ReturnsInt(1337));
        
        [Fact]
        public void Specialized_StructuralMethod_AcceptsTwoInts_ReturnsEquality()
        {
            Assert.False(SpecializedInstance.StructuralMethod_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(SpecializedInstance.StructuralMethod_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
        
        [Fact]
        public void NotSpecialized_Method_AcceptsString() 
            => NotSpecializedInstance.Method_AcceptsString();
        
        [Fact]
        public void NotSpecialized_Method_AcceptsString_ReturnsString() 
            => Assert.Equal("Test", NotSpecializedInstance.Method_AcceptsString_ReturnsString("Test"));
        
        [Fact]
        public void NotSpecialized_Method_AcceptsTwoStrings_ReturnsEquality()
        {
            Assert.False(NotSpecializedInstance.Method_AcceptsTwoStrings_ReturnsEquality("Test1", "Test2"));
            Assert.True(NotSpecializedInstance.Method_AcceptsTwoStrings_ReturnsEquality("Test", "Test"));
        }

        [Fact]
        public void NotSpecialized_Method_AcceptsInt()
            => NotSpecializedInstance.Method_AcceptsInt();
        
        [Fact]
        public void NotSpecialized_Method_AcceptsInt_ReturnsInt() 
            => Assert.Equal(1337, NotSpecializedInstance.Method_AcceptsInt_ReturnsInt(1337));
        
        [Fact]
        public void NotSpecialized_Method_AcceptsTwoInts_ReturnsEquality()
        {
            Assert.False(NotSpecializedInstance.Method_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(NotSpecializedInstance.Method_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
        
        [Fact]
        public void NotSpecialized_StructuralMethod_AcceptsInt_ReturnsInt() 
            => Assert.Equal(1337, NotSpecializedInstance.StructuralMethod_AcceptsInt_ReturnsInt(1337));
        
        [Fact]
        public void NotSpecialized_StructuralMethod_AcceptsTwoInts_ReturnsEquality()
        {
            Assert.False(NotSpecializedInstance.StructuralMethod_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(NotSpecializedInstance.StructuralMethod_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
    }
}