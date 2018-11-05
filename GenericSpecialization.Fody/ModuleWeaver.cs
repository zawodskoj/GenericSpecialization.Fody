using System.Collections.Generic;
using Fody;

namespace GenericSpecialization.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
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