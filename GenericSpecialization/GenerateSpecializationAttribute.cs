using System;
using System.Runtime.CompilerServices;

namespace GenericSpecialization
{
    /// <summary>
    /// Marks class as requiring specialization with specified type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class GenerateSpecializationAttribute : Attribute
    {
        /// <summary>
        /// Specialization type
        /// </summary>
        public Type SpecializationType { get; }
        
        /// <summary>
        /// Initializes a new instance of the GenerateSpecialization attribute
        /// </summary>
        /// <param name="specializationType">Specialization type</param>
        public GenerateSpecializationAttribute(Type specializationType)
        {
            SpecializationType = specializationType;
        }
    }
}