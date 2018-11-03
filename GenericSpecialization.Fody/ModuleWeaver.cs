using System;
using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

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
            
            if (typeReference is GenericInstanceType genericInstanceType)
            {
                return typeReference.Resolve().MakeGenericInstanceType(
                    genericInstanceType.GenericArguments.Select(x => GetSpecializedType(x, scope)).ToArray());
            }
            
            return typeReference;
        }
        
        private static bool CompareTypeReferences(TypeReference ref1, TypeReference ref2)
        {
            if (!(ref1 is GenericInstanceType git1) ||
                !(ref2 is GenericInstanceType git2))
                return ref1.Resolve() == ref2.Resolve();

            return git1.Resolve() == git2.Resolve() &&
                   git1.GenericArguments.Zip(git2.GenericArguments,
                       CompareTypeReferences).All(x => x);
        }
        
        private TypeReference MakeGenericType(TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }
        
        private MethodReference MakeGenericTypeNonGenericMethod(MethodReference self, SpecializationScope scope,
            params TypeReference[] arguments) 
        {
            var reference = new MethodReference(self.Name, self.ReturnType, MakeGenericType(self.DeclaringType, arguments))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(GetSpecializedType(parameter.ParameterType, scope)));

            foreach (var genericParameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(genericParameter.Name, reference));

            return reference;
        }
        
        private MethodReference CloneMethodReference(MethodReference self, SpecializationScope scope)
        {
            var reference = new MethodReference(self.Name, self.ReturnType, self.DeclaringType)
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(GetSpecializedType(parameter.ParameterType, scope)));

            foreach (var genericParameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(genericParameter.Name, reference));

            return reference;
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
                            body.Instructions.Add(Instruction.Create(instruction.OpCode,
                                GetSpecializedType(typeref, scope)));
                            break;
                        case MethodReference methodref:
                            var specializedDeclaringType = GetSpecializedType(methodref.DeclaringType, scope);
                            var genericDeclaringType = specializedDeclaringType.Resolve();
                            var genericMethod = genericDeclaringType
                                .Methods.Single(x => x.Name == methodref.Name && x.Attributes == methodref.Resolve().Attributes &&
                                                    x.Parameters.Count == methodref.Parameters.Count &&
                                                    x.Parameters.Zip(methodref.Parameters,
                                                         (y, z) => CompareTypeReferences(
                                                             GetSpecializedType(y.ParameterType, scope),
                                                             GetSpecializedType(z.ParameterType, scope))).All(y => y));
                            if (specializedDeclaringType is GenericInstanceType genericInstanceType)
                            {
                                var specializedMethod = MakeGenericTypeNonGenericMethod(genericMethod, scope,
                                    genericInstanceType.GenericArguments.Select(x => GetSpecializedType(x, scope))
                                        .ToArray());
                                body.Instructions.Add(Instruction.Create(instruction.OpCode, ModuleDefinition.ImportReference(specializedMethod)));
                            }
                            else
                            {
                                body.Instructions.Add(Instruction.Create(instruction.OpCode, ModuleDefinition.ImportReference(genericMethod)));
                            }
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