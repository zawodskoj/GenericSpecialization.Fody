using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace GenericSpecialization.Fody
{
    internal class Injector
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
            
            Debugger.Launch();
            
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
                            newref = newref.MakeGenericInstanceMethod(
                                genericInstanceMethod.GenericArguments
                                    .Select(x => FindSpecializedType(x, specializations)).ToArray());
                        }

                        instruction.Operand = newref;
                        break;
                }
            }
        }

        private MethodReference InsertRemainingGenerics(MethodReference methodReference, 
            GenericInstanceType genericInstanceType)
        {
            if (genericInstanceType.GenericArguments.Count > 1)
                return methodReference.MakeMethodWithGenericDeclaringType(genericInstanceType.GenericArguments.Skip(1).ToArray());
            return methodReference;
        }
    }
}