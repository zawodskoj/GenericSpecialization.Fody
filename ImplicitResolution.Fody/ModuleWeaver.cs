using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

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
    
    /// <inheritdoc />
    public class ModuleWeaver : BaseModuleWeaver
    {
        private MethodDefinition _resolveMethod;
        private TypeDefinition _typeclassAttributeType;

        private readonly List<TypeclassDeclaration> _typeclasses =
            new List<TypeclassDeclaration>();

        private void Initialize()
        {
            _resolveMethod = 
                ModuleDefinition.ImportReference(typeof(Implicitly)).Resolve().Methods.First(x => x.Name == "Resolve");

            _typeclassAttributeType = ModuleDefinition.ImportReference(typeof(TypeclassAttribute)).Resolve();
        }

        private IEnumerable<TypeDefinition> CollectAllTypes(IEnumerable<TypeDefinition> definitions = null)
        {
            foreach (var type in definitions ?? ModuleDefinition.Types)
            {
                yield return type;
                foreach (var inside in CollectAllTypes(type.NestedTypes))
                    yield return inside;
            }
        }

        private bool CollectTypeclasses()
        {
            foreach (var type in CollectAllTypes())
            {
                if (type.IsAbstract) continue;
                foreach (var typeclassInterface in type.Interfaces.Where(x =>
                    x.InterfaceType.Resolve().CustomAttributes
                        .Any(y => y.AttributeType.Resolve() == _typeclassAttributeType)))
                {
                    _typeclasses.Add(new TypeclassDeclaration(typeclassInterface.InterfaceType.Resolve(), type,
                        ((GenericInstanceType) typeclassInterface.InterfaceType).GenericArguments[0]));   
                }
            }

            return true;
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
                            bool CompareRefs(TypeReference ref1, TypeReference ref2, bool allowGenericOnRef2 = false)
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

                            var typeclassType = (GenericInstanceType) calledMethod.GenericArguments[0];
                            TypeReference typeclass = _typeclasses.Single(x => typeclassType.Resolve() == x.BaseType &&
                                                                     CompareRefs(typeclassType.GenericArguments[0], x.InstanceType, true)).ImplementationType;

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
                            ilproc.InsertAfter(instruction, Instruction.Create(OpCodes.Ldloca, vardef));
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

        private void WeaveTypes()
        {
            foreach (var type in CollectAllTypes())
            {
                foreach (var method in type.Methods)
                {
                    if (!WeaveMethod(method)) return;
                }
            }
        }
        
        /// <inheritdoc />
        public override void Execute()
        {
            Debugger.Launch();
            
            Initialize();
            if (!CollectTypeclasses()) return;
            WeaveTypes();
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