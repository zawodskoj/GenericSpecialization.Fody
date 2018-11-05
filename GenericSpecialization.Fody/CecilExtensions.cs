using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace GenericSpecialization.Fody
{
    internal static class CecilExtensions
    {
        private const string GenericParameterCountMismatch = "Generic parameter count mismatch";
        
        private static Exception CreateMismatchException<T>(string what, T expecting, T got)
        {
            return new ArgumentException($"{what}\n\tExpecting: {expecting}\n\tGot: {got}");
        }
        
        public static TypeReference ResolveTypeFromSubstitutedGenericArgument(
            this TypeReference typeToResolve,
            IGenericParameterProvider genericParameterProvider,
            IList<TypeReference> substitutedArguments)
        {
            if (genericParameterProvider.GenericParameters.Count != substitutedArguments.Count)
                throw CreateMismatchException(GenericParameterCountMismatch,
                    genericParameterProvider.GenericParameters.Count,
                    substitutedArguments.Count);

            
            if (typeToResolve is GenericParameter genericParameter && genericParameter.Owner == genericParameterProvider)
            {
                var index = genericParameterProvider.GenericParameters.IndexOf(genericParameter);
                if (index < 0) throw new ArgumentException("Generic parameter has valid owner, but does not exist in owner parameter list");
                return substitutedArguments[index];
            }

            return typeToResolve; // unresolved
        }
        
        public static TypeReference ResolveTypeFromClonedGenericParameter(
            this TypeReference typeToResolve,
            IGenericParameterProvider oldParameterProvider,
            IGenericParameterProvider newParameterProvider)
        {
            if (oldParameterProvider.GenericParameters.Count != newParameterProvider.GenericParameters.Count)
                throw CreateMismatchException(GenericParameterCountMismatch,
                    oldParameterProvider.GenericParameters.Count,
                    newParameterProvider.GenericParameters.Count);

            
            if (typeToResolve is GenericParameter genericParameter && genericParameter.Owner == oldParameterProvider)
            {
                var index = oldParameterProvider.GenericParameters.IndexOf(genericParameter);
                if (index < 0) throw new ArgumentException("Generic parameter has valid owner, but does not exist in owner parameter list");
                return newParameterProvider.GenericParameters[index];
            }

            return typeToResolve; // unresolved
        }

        public static TypeReference ResolveTypeFromClonedMethodReference(
            this TypeReference typeToResolve,
            MethodReference oldMethod,
            MethodReference newMethod)
        {
            if (oldMethod.GenericParameters.Count != newMethod.GenericParameters.Count)
                throw CreateMismatchException(GenericParameterCountMismatch,
                    oldMethod.GenericParameters.Count,
                    newMethod.GenericParameters.Count);

            // return type and parameter types in method resolution should not be resolved as substituted generic arguments
            // that type of resolution may be useful in some cases, but i dont care
            
            // if (newMethod.DeclaringType is GenericInstanceType genericInstanceType)
            //     typeToResolve = typeToResolve.ResolveTypeFromSubstitutedGenericArgument(
            //     oldMethod.DeclaringType, genericInstanceType.GenericArguments);

            return typeToResolve.ResolveTypeFromClonedGenericParameter(oldMethod, newMethod);
        }
        
        public static MethodReference MakeMethodWithGenericDeclaringType(this MethodReference methodToClone, params TypeReference[] arguments)
        {
            if (methodToClone.DeclaringType.GenericParameters.Count != arguments.Length)
                throw new ArgumentException(
                    $"Generic parameter count mismatch, expecting {methodToClone.DeclaringType.GenericParameters}, got {arguments.Length}",
                    nameof(arguments));
            
            var clonedMethod = new MethodReference(
                methodToClone.Name, 
                methodToClone.ReturnType, // return type should be resolved from generic arguments later, using old now
                methodToClone.DeclaringType.MakeGenericInstanceType(arguments))
            {
                HasThis = methodToClone.HasThis,
                ExplicitThis = methodToClone.ExplicitThis,
                CallingConvention = methodToClone.CallingConvention
            };

            foreach (var genericParameter in methodToClone.GenericParameters)
                clonedMethod.GenericParameters.Add(new GenericParameter(genericParameter.Name, clonedMethod));

            clonedMethod.ReturnType =
                methodToClone.ReturnType.ResolveTypeFromClonedMethodReference(methodToClone, clonedMethod);
            
            foreach (var parameter in methodToClone.Parameters)
                clonedMethod.Parameters.Add(new ParameterDefinition(
                    parameter.ParameterType.ResolveTypeFromClonedMethodReference(methodToClone, clonedMethod)));

            return clonedMethod;
        }

        public static GenericInstanceMethod MakeGenericInstanceMethod(this MethodReference methodReference,
            params TypeReference[] arguments)
        {
            if (methodReference.GenericParameters.Count != arguments.Length)
                throw CreateMismatchException(GenericParameterCountMismatch,
                    methodReference.GenericParameters.Count,
                    arguments.Length);
            
            var newGenericMethod = new GenericInstanceMethod(methodReference);

            foreach (var argument in arguments)
                newGenericMethod.GenericArguments.Add(argument);

            return newGenericMethod;
        }
    }
}