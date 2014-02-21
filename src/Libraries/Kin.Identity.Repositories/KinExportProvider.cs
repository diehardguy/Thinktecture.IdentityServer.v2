using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kin.Identity.Repository.Config;
using Kin.Identity.Repository.Interfaces;
using Thinktecture.IdentityServer.Configuration;

namespace Kin.Identity.Repository
{
    public class KinExportProvider : ExportProvider
    {
        private Dictionary<string, string> _mappings;

        public KinExportProvider()
        {
            var section = 
                ConfigurationManager.GetSection(KinConfigurationSection.SectionName) 
                as KinConfigurationSection;

            _mappings = new Dictionary<string, string>
            {
                {typeof(IKinUserRepository).FullName, section.KinUserRepository}
            };
        }

        protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
        {
            var exports = new List<Export>();

            string implementingType;
            if (_mappings.TryGetValue(definition.ContractName, out implementingType))
            {
                var t = Type.GetType(implementingType);
                if (t == null)
                {
                    throw new InvalidOperationException("Type not found for interface: " + definition.ContractName);
                }

                var instance = t.GetConstructor(Type.EmptyTypes).Invoke(null);
                var exportDefinition = new ExportDefinition(definition.ContractName,
                    new Dictionary<string, object>());
                var toAdd = new Export(exportDefinition, () => instance);
                exports.Add(toAdd);
            }
            return exports;
        }
    }
}
