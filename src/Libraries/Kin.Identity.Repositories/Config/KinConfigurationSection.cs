using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kin.Identity.Repository.Config
{
    public partial class KinConfigurationSection : 
        global::System.Configuration.ConfigurationSection
    {
        public const string SectionName = "kin.identity.repositories";

        /// <summary>
        /// The XML name of the <see cref="ConfigurationProvider"/> property.
        /// </summary>
        internal const global::System.String KinUserRepositoryName = "kinUserManagement";

        /// <summary>
        /// Gets or sets type of the class that provides custom user validation
        /// </summary>
        [global::System.Configuration.ConfigurationProperty(KinUserRepositoryName, IsRequired = false, 
            IsKey=false, IsDefaultCollection=false,
            DefaultValue = "Kin.Identity.Repository.KinUserRepository, Kin.Identity.Repository")]
        public global::System.String KinUserRepository
        {
            get {
                return (global::System.String)base[KinUserRepositoryName];
            }
            set
            {
                base[KinUserRepositoryName] = value;
            }
        }

    }
}
