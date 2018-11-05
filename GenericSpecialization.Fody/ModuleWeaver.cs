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
    public partial class ModuleWeaver : BaseModuleWeaver
    {
        public override void Execute()
        {
            var specializer = new Specializer(ModuleDefinition);
            var injector = new Injector(ModuleDefinition);
            injector.Inject(specializer.Specialize());
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
        }
    }
}