using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace ImplicitResolution.Fody
{
    internal class TypeclassDeclaration
    {
        public TypeclassDeclaration(TypeDefinition baseType, TypeDefinition implementationType, TypeReference instanceType)
        {
            BaseType = baseType;
            ImplementationType = implementationType;
            InstanceType = instanceType;
        }

        public TypeDefinition BaseType { get; }
        public TypeDefinition ImplementationType { get; }
        public TypeReference InstanceType { get; }
    }

    internal class TypeclassSpecialization
    {
        public TypeclassSpecialization(TypeReference specializedType)
        {
            SpecializedType = specializedType;
        }

        public TypeReference SpecializedType { get; }
    }
    
    /// <inheritdoc />
    public class ModuleWeaver : BaseModuleWeaver
    {
        private MethodDefinition _resolveMethod;
        private TypeDefinition _typeclassAttributeType;

        private readonly List<TypeclassDeclaration> _typeclasses =
            new List<TypeclassDeclaration>();
        private readonly Dictionary<TypeclassDeclaration, List<TypeclassSpecialization>> _genericTypeclasses =
            new Dictionary<TypeclassDeclaration, List<TypeclassSpecialization>>();

        private void Initialize()
        {
            _resolveMethod = 
                ModuleDefinition.ImportReference(typeof(Implicitly)).Resolve().Methods.First(x => x.Name == "Resolve");

            _typeclassAttributeType = ModuleDefinition.ImportReference(typeof(TypeclassAttribute)).Resolve();
        }

        private IEnumerable<(TypeDefinition Root, TypeDefinition Type)> CollectAllTypes(
            IEnumerable<TypeDefinition> definitions = null, TypeDefinition root = null)
        {
            foreach (var type in definitions ?? ModuleDefinition.Types)
            {
                yield return (root ?? type, type);
                foreach (var inside in CollectAllTypes(type.NestedTypes, root ?? type))
                    yield return inside;
            }
        }

        private bool CollectTypeclasses()
        {
            foreach (var (_, type) in CollectAllTypes())
            {
                if (type.IsAbstract) continue;
                foreach (var typeclassInterface in type.Interfaces.Where(x =>
                    x.InterfaceType.Resolve().CustomAttributes
                        .Any(y => y.AttributeType.Resolve() == _typeclassAttributeType)))
                {
                    var decl = new TypeclassDeclaration(typeclassInterface.InterfaceType.Resolve(), type,
                        ((GenericInstanceType) typeclassInterface.InterfaceType).GenericArguments[0]);

                    if (decl.ImplementationType.HasGenericParameters)
                    {
                        _genericTypeclasses.Add(decl, new List<TypeclassSpecialization>());
                    }
                    else
                    {
                        _typeclasses.Add(decl);
                    }
                }
            }

            return true;
        }

        private static bool CompareRefs(TypeReference ref1, TypeReference ref2, bool allowGenericOnRef2 = false)
        {
            if (allowGenericOnRef2)
                return ref1.Resolve() == ref2.Resolve(); // todo more checks

            if (!(ref1 is GenericInstanceType git1) ||
                !(ref2 is GenericInstanceType git2))
                return ref1.Resolve() == ref2.Resolve();

            return git1.Resolve() == git2.Resolve() &&
                   git1.GenericArguments.Zip(git2.GenericArguments,
                       (x, y) => CompareRefs(x, y)).All(x => x);
        }
        
        private bool WeaveMethod(MethodDefinition method)
        {
            if (method.Body == null) return true;

            var ilproc = method.Body.GetILProcessor();

            while (true)
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode.Code == Code.Call &&
                        instruction.Operand is GenericInstanceMethod calledMethod)
                    {
                        var resolved = calledMethod.Resolve();
                        if (resolved.GetBaseMethod() == _resolveMethod)
                        {
                            var typeclassType = (GenericInstanceType) calledMethod.GenericArguments[0];
                            TypeReference typeclass = _typeclasses.SingleOrDefault(x => typeclassType.Resolve() == x.BaseType &&
                                CompareRefs(typeclassType.GenericArguments[0], x.InstanceType, true))?.ImplementationType;

                            if (typeclass == null && 
                                _genericTypeclasses.SingleOrDefault(x => typeclassType.Resolve() == x.Key.BaseType) is var pair &&
                                pair.Key != null)
                            {
                                typeclass = SpecializeTypeclass(((GenericInstanceType)typeclassType.GenericArguments[0]).GenericArguments[0],
                                    pair.Key, pair.Value);
                            }

                            if (typeclass == null)
                            {
                                LogError("Unresolved typeclass " + typeclassType);
                                return false;
                            }

                            if (typeclass.HasGenericParameters)
                            {
                                var gen = ((GenericInstanceType) typeclassType.GenericArguments[0]).GenericArguments[0];
                                typeclass = typeclass.MakeGenericInstanceType(gen);
                            }

                            var vardef = new VariableDefinition(typeclass);
                            method.Body.Variables.Add(vardef);

                            if (instruction.Previous.OpCode.Code == Code.Box) ilproc.Remove(instruction.Previous);
                            ilproc.InsertBefore(instruction, Instruction.Create(OpCodes.Ldloca, vardef));
                            ilproc.InsertAfter(instruction, Instruction.Create(OpCodes.Box, typeclass));
                            ilproc.InsertAfter(instruction, Instruction.Create(OpCodes.Ldloc, vardef));
                            ilproc.Replace(instruction, Instruction.Create(OpCodes.Initobj, typeclass));

                            goto cont;
                        }
                    }
                }

                break;

                cont: ;
            }

            return true;
        }

        private TypeReference SpecializeTypeclass(TypeReference targetSpecialization,
            TypeclassDeclaration decl, List<TypeclassSpecialization> existingSpecializations)
        {
            if (existingSpecializations.SingleOrDefault(x => CompareRefs(x.SpecializedType, targetSpecialization))
                    is var exiSpec && exiSpec != null) return exiSpec.SpecializedType;

            var newdecl = GenerateTypeSpecialization(targetSpecialization, decl);
            existingSpecializations.Add(new TypeclassSpecialization(newdecl));
            return newdecl;
        }

        private TypeReference SpecializeTypeReference(TypeReference reference, TypeReference specialization,
            TypeReference originalGeneric, IReadOnlyList<(TypeDefinition, TypeDefinition)> beforeAfterSpec)
        {
            if (reference.Name.StartsWith("!")) // КОСТЫЛЬ
                return specialization;
            if (reference == originalGeneric) return specialization;
            if (beforeAfterSpec.FirstOrDefault(x => x.Item1 == reference) is var found && found.Item2 != null)
                return found.Item2;
            if (reference is GenericInstanceType git)
                return ModuleDefinition.ImportReference(git.Resolve()).MakeGenericInstanceType(
                    git.GenericArguments.Select(x => SpecializeTypeReference(x, specialization, originalGeneric, beforeAfterSpec))
                        .ToArray());
            return reference;
        }

        public MethodDefinition SpecializeMethod(MethodDefinition sourceMethod, TypeReference specialization,
            TypeReference originalGeneric, IReadOnlyList<(TypeDefinition, TypeDefinition)> beforeAfterSpec)
        {
            var targetMethod = new MethodDefinition(sourceMethod.Name, sourceMethod.Attributes, sourceMethod.ReturnType);

            // Copy the parameters; 
            foreach (var param in sourceMethod.Parameters)
            {
                var newParam =
                    new ParameterDefinition(param.Name, param.Attributes, 
                        SpecializeTypeReference(param.ParameterType, specialization, originalGeneric, beforeAfterSpec));
                targetMethod.Parameters.Add(newParam);
            }

            // copy the body
            var newBody = targetMethod.Body;
            var body = sourceMethod.Body;

            newBody.InitLocals = body.InitLocals;

            // copy the local variable definition
            foreach (var var in body.Variables)
            {
                var newVar = new VariableDefinition(var.VariableType);
                newBody.Variables.Add(newVar);
            }

            foreach (var i in body.Instructions)
            {
                switch (i.Operand)
                {
                    case MethodReference methodRef:
                    {
                        var newref = methodRef.IsGenericInstance
                            ? new GenericInstanceMethod(new MethodReference(methodRef.Name,
                                SpecializeTypeReference(methodRef.ReturnType, specialization, originalGeneric, beforeAfterSpec),
                                SpecializeTypeReference(methodRef.DeclaringType, specialization, originalGeneric, beforeAfterSpec)))
                            : new MethodReference(methodRef.Name,
                                SpecializeTypeReference(methodRef.ReturnType, specialization, originalGeneric, beforeAfterSpec),
                                SpecializeTypeReference(methodRef.DeclaringType, specialization, originalGeneric, beforeAfterSpec));
                        
                        // if (methodRef.DeclaringType is non-specialized type)
                        foreach (var param in methodRef.GenericParameters)
                        {
                            newref.GenericParameters.Add(new GenericParameter(param.Name, newref));
                        }
                        foreach (var param in methodRef.Parameters)
                        {
                            newref.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, 
                                SpecializeTypeReference(param.ParameterType, specialization, originalGeneric, beforeAfterSpec)));
                        }
                        if (newref is GenericInstanceMethod gim)
                        {
                            foreach (var genericArg in ((GenericInstanceMethod) methodRef).GenericArguments)
                                gim.GenericArguments.Add(
                                    SpecializeTypeReference(genericArg, specialization, originalGeneric, beforeAfterSpec));
                        }
                        
//                        var newref = methodRef.IsGenericInstance
//                            ? new GenericInstanceMethod(new MethodReference(methodRef.Name, methodRef.ReturnType, methodRef.DeclaringType))
//                            : new MethodReference(methodRef.Name, methodRef.ReturnType, methodRef.DeclaringType);
//                        
//                        // if (methodRef.DeclaringType is non-specialized type)y
//                        foreach (var param in methodRef.GenericParameters)
//                        {
//                            newref.GenericParameters.Add(new GenericParameter(param.Name, newref));
//                        }
//                        foreach (var param in methodRef.Parameters)
//                        {
//                            newref.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
//                        }
//                        if (newref is GenericInstanceMethod gim)
//                        {
//                            foreach (var genericArg in ((GenericInstanceMethod) methodRef).GenericArguments)
//                                gim.GenericArguments.Add(genericArg);
//                        }

                        newBody.Instructions.Add(Instruction.Create(i.OpCode, newref));
                        break;
                    }
                    case FieldReference fieldRef:
                    {
                        var newref = new FieldReference(fieldRef.Name, 
                            SpecializeTypeReference(fieldRef.FieldType, specialization, originalGeneric, beforeAfterSpec),
                            SpecializeTypeReference(fieldRef.DeclaringType, specialization, originalGeneric, beforeAfterSpec));
                        newBody.Instructions.Add(Instruction.Create(i.OpCode, newref));
                        break;
                    }
                    default:
                        newBody.Instructions.Add(i);
                        break;

                }
            }

            // copy the exception handler blocks

            foreach (var eh in body.ExceptionHandlers)
            {
                var newEh = new ExceptionHandler(eh.HandlerType)
                {
                    CatchType = eh.CatchType
                };

                // we need to setup neh.Start and End; these are instructions; we need to locate it in the source by index
                if (eh.TryStart != null)
                {
                    var idx = body.Instructions.IndexOf(eh.TryStart);
                    newEh.TryStart = newBody.Instructions[idx];
                }

                if (eh.TryEnd != null)
                {
                    var idx = body.Instructions.IndexOf(eh.TryEnd);
                    newEh.TryEnd = newBody.Instructions[idx];
                }

                newBody.ExceptionHandlers.Add(newEh);
            }

            return targetMethod;
        }

        private FieldDefinition SpecializeField(FieldDefinition field, TypeReference specialization, 
            GenericParameter originalGeneric, IReadOnlyList<(TypeDefinition, TypeDefinition)> beforeAfterSpec)
        {
            return new FieldDefinition(field.Name, field.Attributes, 
                SpecializeTypeReference(field.FieldType, specialization, originalGeneric, beforeAfterSpec));
        }

        private TypeDefinition SpecializeType(TypeReference targetSpecialization, TypeDefinition typeToSpecialize,
            TypeReference baseType, TypeReference instanceType, bool isNested, 
            IReadOnlyList<(TypeDefinition, TypeDefinition)> beforeAfterSpec)
        {
            var verybasic = typeToSpecialize.BaseType;
            var specializedType = new TypeDefinition(typeToSpecialize.Namespace,
                isNested ? typeToSpecialize.Name : typeToSpecialize.Name + "$specialized$" + targetSpecialization.FullName,
                typeToSpecialize.Attributes,
                typeToSpecialize.BaseType);

            beforeAfterSpec = beforeAfterSpec?.Concat(new[] {(typeToSpecialize, specializedType)}).ToArray() ??
                              new[] {(typeToSpecialize, specializedType)};

            var originalGeneric = typeToSpecialize.GenericParameters[0];

            if (baseType != null)
            {
                var impl = new InterfaceImplementation(
                    baseType.MakeGenericInstanceType(ModuleDefinition.ImportReference(instanceType.Resolve())
                        .MakeGenericInstanceType(targetSpecialization)));
                specializedType.Interfaces.Add(impl);
            }

            foreach (var nested in typeToSpecialize.NestedTypes)
            {
                var newNested = SpecializeType(targetSpecialization, nested, null, null, true, beforeAfterSpec);
                specializedType.NestedTypes.Add(newNested);
            }
            
            foreach (var field in typeToSpecialize.Fields)
            {
                var newField = SpecializeField(field, targetSpecialization, originalGeneric, beforeAfterSpec);
                specializedType.Fields.Add(newField);
            }

            foreach (var meth in typeToSpecialize.Methods)
            {
                var newMeth = SpecializeMethod(meth, targetSpecialization, originalGeneric, beforeAfterSpec);
                specializedType.Methods.Add(newMeth);
            }
           
            return specializedType;
        }

        private TypeReference GenerateTypeSpecialization(TypeReference targetSpecialization, TypeclassDeclaration decl)
        {
            var specializedType = SpecializeType(targetSpecialization, decl.ImplementationType, decl.BaseType,
                decl.InstanceType, false, null);
            
            ModuleDefinition.Types.Add(specializedType);

            return specializedType;
        }

        private bool WeaveTypes()
        {
            foreach (var (rootType, type) in CollectAllTypes().ToArray())
            {
                if (_genericTypeclasses.Any(x => x.Key.ImplementationType == rootType)) continue;
                
                foreach (var method in type.Methods)
                {
                    if (!WeaveMethod(method)) return false;
                }
            }

            return true;
        }
        
        /// <inheritdoc />
        public override void Execute()
        {
            // Debugger.Launch();
            
            Initialize();
            
            if (!CollectTypeclasses())
            {
                Cancel();
                return;
            }

            if (!WeaveTypes())
            {
                Cancel();
            }
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
    
    [AttributeUsage(AttributeTargets.Interface)]
    public class TypeclassAttribute : Attribute {}
    
    public static class Implicitly
    {
        public static TTypeclass Resolve<TTypeclass>()
            => throw new Exception("Not weaved"); 
    }
}