using System;
using System.Collections.Generic;
using Fody;
using ImplicitResolution.AssemblyToProcess;
using ImplicitResolution.Fody;
using Xunit;

namespace ImplicitResolution.Tests
{
    public class GenericSpecializationTests
    {
        public static readonly dynamic SpecializedInstance, NotSpecializedInstance;
        public static IEnumerable<object[]> Instances;

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

            Instances = new[] {new[]{SpecializedInstance}, new[] {NotSpecializedInstance}};
        }
        
        [Theory, MemberData(nameof(Instances))]
        public void Method_AcceptsString(dynamic instance) 
            => instance.Method_AcceptsString();
        
        [Theory, MemberData(nameof(Instances))]
        public void Method_AcceptsString_ReturnsString(dynamic instance) 
            => Assert.Equal("Test", instance.Method_AcceptsString_ReturnsString("Test"));
        
        [Theory, MemberData(nameof(Instances))]
        public void Method_AcceptsTwoStrings_ReturnsEquality(dynamic instance)
        {
            Assert.False(instance.Method_AcceptsTwoStrings_ReturnsEquality("Test1", "Test2"));
            Assert.True(instance.Method_AcceptsTwoStrings_ReturnsEquality("Test", "Test"));
        }

        [Theory, MemberData(nameof(Instances))]
        public void Method_AcceptsInt(dynamic instance)
            => instance.Method_AcceptsInt();
        
        [Theory, MemberData(nameof(Instances))]
        public void Method_AcceptsInt_ReturnsInt(dynamic instance)
            => Assert.Equal(1337, instance.Method_AcceptsInt_ReturnsInt(1337));
        
        [Theory, MemberData(nameof(Instances))]
        public void Method_AcceptsTwoInts_ReturnsEquality(dynamic instance)
        {
            Assert.False(instance.Method_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(instance.Method_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
        
        [Theory, MemberData(nameof(Instances))]
        public void StructuralMethod_AcceptsInt_ReturnsInt(dynamic instance) 
            => Assert.Equal(1337, instance.StructuralMethod_AcceptsInt_ReturnsInt(1337));
        
        [Theory, MemberData(nameof(Instances))]
        public void StructuralMethod_AcceptsTwoInts_ReturnsEquality(dynamic instance)
        {
            Assert.False(instance.StructuralMethod_AcceptsTwoInts_ReturnsEquality(1, 2));
            Assert.True(instance.StructuralMethod_AcceptsTwoInts_ReturnsEquality(1, 1));
        }
    }
}