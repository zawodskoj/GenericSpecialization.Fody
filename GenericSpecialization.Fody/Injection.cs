using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace GenericSpecialization.Fody
{
    public class Injector
    {
        private readonly ModuleDefinition _moduleDefinition;

        public Injector(ModuleDefinition moduleDefinition)
        {
            _moduleDefinition = moduleDefinition;
        }

        public void Inject(IReadOnlyList<SpecializationInfo> specializations)
        {
            var injectSpecializationsAttributeType =
                _moduleDefinition.ImportReference(typeof(InjectSpecializationsAttribute)).Resolve();

            var flattenedSpecializations = FlattenSpecializations(specializations).ToList();
            
            foreach (var type in _moduleDefinition.Types.ToArray())
            {
                if (type.CustomAttributes.Any(x => x.AttributeType.Resolve() == injectSpecializationsAttributeType))
                {
                    InjectSpecializations(type, flattenedSpecializations);
                }
            }
        }

        private IEnumerable<SpecializationInfo> FlattenSpecializations(IReadOnlyList<SpecializationInfo> specializations)
        {
            foreach (var spec in specializations)
            {
                yield return spec;
                foreach (var nested in FlattenSpecializations(spec.NestedClasses))
                    yield return nested;
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
                        var newref = _moduleDefinition.ImportReference(
                            specializations.Select(x =>
                                    x.SpecializedMethods.FirstOrDefault(y =>
                                            y.Key.Resolve() == methodref.Resolve() &&
                                            MetadataComparer.AreSame(specializationArg, x.Specialization))
                                        is var pair && pair.Value != null
                                        ? InsertRemainingGenerics(pair.Value, genericInstanceType)
                                        : null)
                                .FirstOrDefault(x => x != null) ?? methodref);
                        if (methodref is GenericInstanceMethod genericInstanceMethod && newref != methodref)
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
        
        private TypeReference MakeGenericType(TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        private MethodReference MakeGenericTypeNonGenericMethod(MethodReference self,
            params TypeReference[] arguments) 
        {
            var reference = new MethodReference(self.Name, self.ReturnType, MakeGenericType(self.DeclaringType, arguments))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var genericParameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(genericParameter.Name, reference));

            return reference;
        }


        private MethodReference InsertRemainingGenerics(MethodReference methodReference, 
            GenericInstanceType genericInstanceType)
        {
            if (genericInstanceType.GenericArguments.Count > 1)
                return MakeGenericTypeNonGenericMethod(methodReference,
                    genericInstanceType.GenericArguments.Skip(1).ToArray());
            return methodReference;
        }
    }
}