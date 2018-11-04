using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace GenericSpecialization.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        private class SpecializationInfo
        {
            public SpecializationInfo(TypeDefinition genericClass, TypeReference specialization,
                TypeDefinition specializedClass, Dictionary<MethodReference, MethodReference> specializedMethods)
            {
                GenericClass = genericClass;
                Specialization = specialization;
                SpecializedClass = specializedClass;
                SpecializedMethods = specializedMethods;
            }

            public TypeDefinition GenericClass { get; }
            public TypeReference Specialization { get; }
            public TypeDefinition SpecializedClass { get; }
            public Dictionary<MethodReference, MethodReference> SpecializedMethods { get; }
        }
        
        public override void Execute()
        {
            var generateSpecializationAttributeType =
                ModuleDefinition.ImportReference(typeof(GenerateSpecializationAttribute)).Resolve();
            var injectSpecializationsAttributeType =
                ModuleDefinition.ImportReference(typeof(InjectSpecializationsAttribute)).Resolve();
            
            var specializations = new List<SpecializationInfo>();
            
            foreach (var type in ModuleDefinition.Types.ToArray())
            {
                foreach (var genAttr in type.CustomAttributes.Where(x =>
                    x.AttributeType.Resolve() == generateSpecializationAttributeType))
                {
                    var typeref = (TypeReference) genAttr.ConstructorArguments[0].Value;
                    specializations.Add(GenerateSpecialization(type, typeref));
                }
            }

            Debugger.Launch();
            
            foreach (var type in ModuleDefinition.Types.ToArray())
            {
                if (type.CustomAttributes.Any(x => x.AttributeType.Resolve() == injectSpecializationsAttributeType))
                {
                    InjectSpecializations(type, specializations);
                }
            }
        }

        private void InjectSpecializations(TypeDefinition type, List<SpecializationInfo> specializations)
        {
            foreach (var method in type.Methods)
            {
                InjectSpecializationsInMethod(method, specializations);
            }
        }

        private TypeReference FindSpecializedType(TypeReference type, List<SpecializationInfo> specializations)
        {
            if (!(type is GenericInstanceType genericInstanceType) || genericInstanceType.GenericArguments.Count != 1)
                return type;
            var decl = type.Resolve();

            if (specializations.SingleOrDefault(x =>
                        x.GenericClass == decl &&
                        x.Specialization == genericInstanceType.GenericArguments[0]) is SpecializationInfo specialization)
                return specialization.SpecializedClass;
            else
                return type;
        }
        
        private void InjectSpecializationsInMethod(MethodDefinition method,
            List<SpecializationInfo> specializations)
        {
            foreach (var local in method.Body.Variables)
            {
                local.VariableType = FindSpecializedType(local.VariableType, specializations);
            }

            foreach (var instruction in method.Body.Instructions)
            {
                switch (instruction.Operand)
                {
                    case TypeReference typeref:
                        instruction.Operand = FindSpecializedType(typeref, specializations);
                        break;
                    case MethodReference methodref:
                        if (!(methodref.DeclaringType is GenericInstanceType genericInstanceType)) break;
                        var specializationArg = genericInstanceType.GenericArguments[0];
                        instruction.Operand = ModuleDefinition.ImportReference(
                            specializations.Select(x =>
                                    x.SpecializedMethods.FirstOrDefault(y =>
                                            MetadataComparer.AreSame(y.Key, methodref, true) &&
                                            MetadataComparer.AreSame(specializationArg, x.Specialization))
                                        is var pair && pair.Value != null
                                        ? pair.Value
                                        : null)
                                .FirstOrDefault(x => x != null) ?? methodref);
                        break;
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

        private SpecializationInfo GenerateSpecialization(TypeDefinition type, TypeReference specializedArgument)
        {
            if (!type.HasGenericParameters) throw new NotSupportedException();
            if (type.GenericParameters.Count > 1) throw new NotImplementedException();
            
            var specializedType = new TypeDefinition(type.Namespace, 
                type.Name + "$specialized$" + specializedArgument.FullName, 
                type.Attributes,
                type.BaseType);
            
            var scope = new SpecializationScope(type.GenericParameters[0], specializedArgument);

            var methods = new Dictionary<MethodReference, MethodReference>();
            foreach (var method in type.Methods)
            {
                var newMethod = SpecializeMethod(method, scope);
                methods.Add(method, newMethod);
                specializedType.Methods.Add(newMethod);
            }
            
            ModuleDefinition.Types.Add(specializedType);

            return new SpecializationInfo(type, specializedArgument, specializedType, methods);
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
        
        private TypeReference MakeGenericType(TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }
        
        private MethodReference MakeGenericTypeNonGenericMethod(MethodReference self, List<SpecializationInfo> specializations,
            params TypeReference[] arguments) 
        {
            var reference = new MethodReference(self.Name, self.ReturnType, MakeGenericType(self.DeclaringType, arguments))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(FindSpecializedType(parameter.ParameterType, specializations)));

            foreach (var genericParameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(genericParameter.Name, reference));

            return reference;
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
            var newMethod = new MethodDefinition(method.Name, method.Attributes, 
                GetSpecializedType(method.ReturnType, scope));

            foreach (var genericParameter in method.GenericParameters)
            {
                newMethod.GenericParameters.Add(new GenericParameter(genericParameter.Name, newMethod));
            }

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
                                                         (y, z) => MetadataComparer.AreSame(
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
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class InjectSpecializationsAttribute : Attribute {}
}