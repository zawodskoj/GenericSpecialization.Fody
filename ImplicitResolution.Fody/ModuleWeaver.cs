using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ImplicitResolution.Fody
{
    /// <inheritdoc />
    public class ModuleWeaver : BaseModuleWeaver
    {
        /// <inheritdoc />
        public override void Execute()
        {
            var resolveMethod = 
                ModuleDefinition.ImportReference(typeof(Implicitly)).Resolve().Methods.First(x => x.Name == "Resolve");

            var baseTypeclass = 
                ModuleDefinition.ImportReference(typeof(Typeclass<>)).Resolve();

            var objectType = ModuleDefinition.TypeSystem.Object;

            var typeclasses = new Dictionary<TypeReference, TypeDefinition>();

            foreach (var type in ModuleDefinition.Types)
            {
                if (type.IsAbstract) continue;

                var t = type.BaseType;
                TypeReference tp = type;
                while (t != objectType && t != null)
                {
                    if (t.Resolve() == baseTypeclass)
                    {
                        typeclasses.Add(tp, type);
                        break;
                    }

                    tp = t;
                    t = t.Resolve().BaseType;
                }
            }

            try
            {

                foreach (var type in ModuleDefinition.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.Body == null) continue;

                        var ilproc = method.Body.GetILProcessor();

                        while (true)
                        {
                            foreach (var instruction in method.Body.Instructions)
                            {
                                if (instruction.OpCode.Code == Code.Call &&
                                    instruction.Operand is GenericInstanceMethod calledMethod)
                                {
                                    var resolved = calledMethod.Resolve();
                                    if (resolved.GetBaseMethod() == resolveMethod)
                                    {
                                        bool CompareRefs(TypeReference ref1, TypeReference ref2)
                                        {
                                            if (!(ref1 is GenericInstanceType git1) ||
                                                !(ref2 is GenericInstanceType git2))
                                                return ref1.Resolve() == ref2.Resolve();

                                            return git1.Resolve() == git2.Resolve() &&
                                                   git1.GenericArguments.Zip(git2.GenericArguments,
                                                       CompareRefs).All(x => x);
                                        }
                                        
                                        var typeclassType = (GenericInstanceType) calledMethod.GenericArguments[1];
                                        var typeclass = typeclasses.Single(x => CompareRefs(x.Key, typeclassType)).Value;
                                        var instanceType = typeclassType.GenericArguments[0];
                                        var ctor = typeclass.GetConstructors()
                                            .Single(x => x.Parameters.Count == 1 && CompareRefs(x.Parameters[0].ParameterType, instanceType));
                                        
                                        ilproc.Replace(instruction, Instruction.Create(OpCodes.Newobj, ctor));

                                        goto cont;
                                    }
                                }
                            }
                            
                            break;

                            cont: ;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                
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

    public abstract class Typeclass<T>
    {
        public T That { get; }
        
        public Typeclass(T that)
        {
            That = that;
        }
    }

    public static class Implicitly
    {
        public static TTypeclass Resolve<T, TTypeclass>(T instance) where TTypeclass : Typeclass<T>
            => throw new Exception("Not weaved"); 
    }
}