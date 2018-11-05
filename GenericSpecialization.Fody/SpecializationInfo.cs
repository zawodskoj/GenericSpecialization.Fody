using System.Collections.Generic;
using Mono.Cecil;

namespace GenericSpecialization.Fody
{
    public class SpecializationInfo
    {
        public SpecializationInfo(TypeDefinition genericClass, TypeReference specialization,
            TypeDefinition specializedClass, Dictionary<MethodReference, MethodReference> specializedMethods,
            List<SpecializationInfo> nestedClasses)
        {
            GenericClass = genericClass;
            Specialization = specialization;
            SpecializedClass = specializedClass;
            SpecializedMethods = specializedMethods;
            NestedClasses = nestedClasses;
        }

        public TypeDefinition GenericClass { get; }
        public TypeReference Specialization { get; }
        public TypeDefinition SpecializedClass { get; }
        public Dictionary<MethodReference, MethodReference> SpecializedMethods { get; }
        public List<SpecializationInfo> NestedClasses { get; }
    }
}