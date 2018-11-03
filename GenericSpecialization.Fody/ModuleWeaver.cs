using System;
using System.Collections.Generic;
using Fody;

namespace GenericSpecialization.Fody
{
    /// <inheritdoc />
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override void Execute()
        {
            
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
            yield return "System";
            yield return "System.Runtime";
            yield return "System.Core";
            yield return "netstandard";
            yield return "System.Collections";
            yield return "System.ObjectModel";
            yield return "System.Threading";
            yield return "FSharp.Core";
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class GenerateSpecializationAttribute : Attribute
    {
        public Type SpecializationType { get; }
        
        public GenerateSpecializationAttribute(Type specializationType)
        {
            SpecializationType = specializationType;
        }
    }
}