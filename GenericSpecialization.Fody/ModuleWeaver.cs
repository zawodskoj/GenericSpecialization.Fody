using System;
using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GenericSpecialization.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override void Execute()
        {
            var generateSpecializationAttributeType =
                ModuleDefinition.ImportReference(typeof(GenerateSpecializationAttribute)).Resolve();
            
            foreach (var type in ModuleDefinition.Types.ToArray())
            {
                foreach (var genAttr in type.CustomAttributes.Where(x =>
                    x.AttributeType.Resolve() == generateSpecializationAttributeType))
                {
                    var typeref = (TypeReference) genAttr.ConstructorArguments[0].Value;
                    GenerateSpecialization(type, typeref);
                }
            }
        }

        private class SpecializationScope
        {
            public SpecializationScope(TypeReference genericArgumentType, TypeReference specializedArgumentType)
            {
                GenericArgumentType = genericArgumentType;
                SpecializedArgumentType = specializedArgumentType;
            }

            public TypeReference GenericArgumentType { get; }
            public TypeReference SpecializedArgumentType { get; }
        }

        private void GenerateSpecialization(TypeDefinition type, TypeReference specializedArgument)
        {
            if (!type.HasGenericParameters) throw new NotSupportedException();
            if (type.GenericParameters.Count > 1) throw new NotImplementedException();
            
            var specializedType = new TypeDefinition(type.Namespace, 
                type.Name + "$specialized$" + specializedArgument.FullName, 
                type.Attributes,
                type.BaseType);
            
            var scope = new SpecializationScope(type.GenericParameters[0], specializedArgument);

            foreach (var method in type.Methods)
            {
                specializedType.Methods.Add(SpecializeMethod(method, scope));
            }
            
            ModuleDefinition.Types.Add(specializedType);
        }

        private TypeReference GetSpecializedType(TypeReference typeReference, SpecializationScope scope)
        {
            if (typeReference == scope.GenericArgumentType) return scope.SpecializedArgumentType;
            return typeReference;
        }
        
        private MethodDefinition SpecializeMethod(MethodDefinition method, SpecializationScope scope)
        {
            if (method.HasGenericParameters) throw new NotImplementedException();
            
            var newMethod = new MethodDefinition(method.Name, method.Attributes, 
                GetSpecializedType(method.ReturnType, scope));

            foreach (var parameter in method.Parameters)
            {
                newMethod.Parameters.Add(
                    new ParameterDefinition(
                        parameter.Name, 
                        parameter.Attributes, 
                        GetSpecializedType(parameter.ParameterType, scope)));
            }

            if (!method.IsAbstract)
            {
                var body = new MethodBody(newMethod);
                newMethod.Body = body;

                foreach (var instruction in method.Body.Instructions)
                {
                    switch (instruction.Operand)
                    {
                        case TypeReference typeref:
                            body.Instructions.Add(Instruction.Create(instruction.OpCode, GetSpecializedType(typeref, scope)));
                            break;
                        default:
                            body.Instructions.Add(instruction);
                            break;
                    }
                }
            }

            return newMethod;
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