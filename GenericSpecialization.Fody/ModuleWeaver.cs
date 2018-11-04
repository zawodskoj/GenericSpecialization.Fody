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

            Debugger.Launch();

            foreach (var type in ModuleDefinition.Types.ToArray())
            {
                foreach (var genAttr in type.CustomAttributes.Where(x =>
                    x.AttributeType.Resolve() == generateSpecializationAttributeType))
                {
                    var typeref = (TypeReference) genAttr.ConstructorArguments[0].Value;
                    specializations.Add(GenerateSpecialization(type, typeref));
                }
            }
            
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
                        MetadataComparer.AreSame(x.Specialization, genericInstanceType.GenericArguments[0])) is SpecializationInfo specialization)
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
                        var newref = ModuleDefinition.ImportReference(
                            specializations.Select(x =>
                                    x.SpecializedMethods.FirstOrDefault(y =>
                                            y.Key.Resolve() == methodref.Resolve() &&
                                            MetadataComparer.AreSame(specializationArg, x.Specialization))
                                        is var pair && pair.Value != null
                                        ? pair.Value
                                        : null)
                                .FirstOrDefault(x => x != null) ?? methodref);
                        if (methodref is GenericInstanceMethod genericInstanceMethod)
                        {
                            var newGenericMethod = new GenericInstanceMethod(newref);
                            foreach (var argument in genericInstanceMethod.GenericArguments)
                                newGenericMethod.GenericArguments.Add(FindSpecializedType(argument, specializations));
                            newref = newGenericMethod;
                        }

                        instruction.Operand = newref;
                        break;
                }
            }
        }

        private class SpecializationScope
        {
            public SpecializationScope(TypeReference genericArgumentType, TypeReference specializedArgumentType,
                SpecializationScope outerScope, TypeDefinition genericType, TypeDefinition specializedType)
            {
                GenericArgumentType = genericArgumentType;
                SpecializedArgumentType = specializedArgumentType;
                OuterScope = outerScope;
                GenericType = genericType;
                SpecializedType = specializedType;
            }

            public TypeReference GenericArgumentType { get; }
            public TypeReference SpecializedArgumentType { get; }
            public TypeDefinition GenericType { get; }
            public TypeDefinition SpecializedType { get; }
            public SpecializationScope OuterScope { get; }
        }

        private SpecializationInfo GenerateSpecialization(TypeDefinition type, TypeReference specializedArgument)
        {
            if (!type.HasGenericParameters) throw new NotSupportedException();
            if (type.GenericParameters.Count > 1) throw new NotImplementedException();
            
            var specializedType = new TypeDefinition(type.Namespace, 
                type.Name + "$specialized$" + specializedArgument.FullName, 
                type.Attributes,
                type.BaseType);
            
            var scope = new SpecializationScope(type.GenericParameters[0], specializedArgument, null, type, specializedType);

            foreach (var nestedClass in type.NestedTypes)
            {
                var specInfo = GenerateNestedClassSpecialization(nestedClass, scope);
                specializedType.NestedTypes.Add(specInfo.SpecializedClass);
            }
            
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
        
        private SpecializationInfo GenerateNestedClassSpecialization(TypeDefinition type, SpecializationScope parentScope)
        {
            if (!type.HasGenericParameters)
                throw new NotSupportedException();
            
            var specializedType = new TypeDefinition(type.Namespace, 
                type.Name, 
                type.Attributes,
                type.BaseType);
            
            // todo nested classes

            foreach (var genericParameter in type.GenericParameters.Skip(1)) // skipping specialized parameter
            {
                specializedType.GenericParameters.Add(new GenericParameter(genericParameter.Name, specializedType));
            }
            
            var scope = new SpecializationScope(type.GenericParameters[0], parentScope.SpecializedArgumentType, parentScope,
                type, specializedType);
            
            var methods = new Dictionary<MethodReference, MethodReference>();
            foreach (var method in type.Methods)
            {
                var newMethod = SpecializeMethod(method, scope);
                methods.Add(method, newMethod);
                specializedType.Methods.Add(newMethod);
            }
            
            return new SpecializationInfo(type, scope.SpecializedArgumentType, specializedType, methods);
        }

        private TypeReference GetSpecializedType(TypeReference typeReference, SpecializationScope scope, int depth = 0)
        {
            if (typeReference == scope.GenericArgumentType) return scope.SpecializedArgumentType;
            
            if (typeReference is GenericInstanceType genericInstanceType)
            {
                return typeReference.Resolve().MakeGenericInstanceType(
                    genericInstanceType.GenericArguments.Select(x => GetSpecializedType(x, scope, depth)).ToArray());
            }
            
            return scope.OuterScope == null 
                ? depth == 0 ? typeReference : typeReference // todo
                : GetSpecializedType(typeReference, scope.OuterScope, depth + 1);
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

        private TypeReference GetValidTypeForGenericScope(TypeReference typeref, IGenericParameterProvider oldOwner,
            IEnumerable<GenericParameter> parameters)
        {
            if (typeref is GenericParameter genp &&
                genp.Owner == oldOwner &&
                parameters.FirstOrDefault(x => x.Name == genp.Name) is GenericParameter newGenericParam)
            {
                return newGenericParam;
            }
            else
            {
                return typeref;
            }
        }
        
        private TypeReference GetValidTypeForGenericScope(TypeReference typeref, SpecializationScope scope)
        {
            if (typeref is GenericParameter genp &&
                genp.Owner == scope.GenericType &&
                scope.SpecializedType.GenericParameters.FirstOrDefault(x => x.Name == genp.Name) is GenericParameter newGenericParam)
            {
                return newGenericParam;
            }
            else
            {
                return scope.OuterScope == null ? typeref : GetValidTypeForGenericScope(typeref, scope.OuterScope);
            }
        }
        
        private TypeReference GetValidTypeForGenericScope(TypeReference typeref, IGenericParameterProvider oldOwner,
            IEnumerable<GenericParameter> parameters, SpecializationScope scope)
        {
            return
                GetValidTypeForGenericScope(GetValidTypeForGenericScope(GetSpecializedType(typeref, scope), oldOwner, parameters), scope);
        }
        
        private MethodDefinition SpecializeMethod(MethodDefinition method, SpecializationScope scope)
        {
            var newMethod = new MethodDefinition(method.Name, method.Attributes, GetSpecializedType(method.ReturnType, scope));

            foreach (var genericParameter in method.GenericParameters)
            {
                newMethod.GenericParameters.Add(new GenericParameter(genericParameter.Name, newMethod));
            }

            newMethod.ReturnType = GetValidTypeForGenericScope(newMethod.ReturnType, method, newMethod.GenericParameters, scope);
            
            foreach (var parameter in method.Parameters)
            {
                newMethod.Parameters.Add(
                    new ParameterDefinition(
                        parameter.Name,
                        parameter.Attributes,
                        GetValidTypeForGenericScope(parameter.ParameterType, method, newMethod.GenericParameters, scope)));
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
                                GetValidTypeForGenericScope(typeref, method, newMethod.GenericParameters, scope)));
                            break;
                        case MethodReference methodref:
                            var specializedDeclaringType = 
                                GetValidTypeForGenericScope(methodref.DeclaringType, method, newMethod.GenericParameters, scope);
                            var genericDeclaringType = specializedDeclaringType.Resolve();
                            var genericMethod = genericDeclaringType
                                .Methods.Single(x => x.Name == methodref.Name && x.Attributes == methodref.Resolve().Attributes &&
                                                    x.Parameters.Count == methodref.Parameters.Count &&
                                                    x.Parameters.Zip(methodref.Parameters,
                                                         (y, z) => MetadataComparer.AreSame(
                                                             GetValidTypeForGenericScope(y.ParameterType, method, newMethod.GenericParameters, scope),
                                                             GetValidTypeForGenericScope(z.ParameterType, method, newMethod.GenericParameters, scope))).All(y => y));
                            if (specializedDeclaringType is GenericInstanceType genericInstanceType)
                            {
                                var specializedMethod = MakeGenericTypeNonGenericMethod(genericMethod, scope,
                                    genericInstanceType.GenericArguments.Select(x => GetValidTypeForGenericScope(x, method, newMethod.GenericParameters, scope))
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