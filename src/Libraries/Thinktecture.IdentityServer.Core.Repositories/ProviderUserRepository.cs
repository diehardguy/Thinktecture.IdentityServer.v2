/*
 * Copyright (c) Dominick Baier.  All rights reserved.
 * see license.txt
 */

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web.Security;

namespace Thinktecture.IdentityServer.Repositories
{
    public class ProviderUserRepository : IUserRepository
    {
        [Import]
        public IClientCertificatesRepository Repository { get; set; }

        public ProviderUserRepository()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public virtual bool ValidateUser(string userName, string password)
        {
            return Membership.ValidateUser(userName, password);
        }

        public virtual bool ValidateUser(X509Certificate2 clientCertificate, out string userName)
        {
            return Repository.TryGetUserNameFromThumbprint(clientCertificate, out userName);
        }

        public virtual IEnumerable<string> GetRoles(string userName)
        {
            var returnedRoles = new List<string>();

            if (Roles.Enabled)
            {
                var roles = Roles.GetRolesForUser(userName);
                returnedRoles = roles.Where(role => role.StartsWith(Constants.Roles.InternalRolesPrefix)).ToList();
            }

            return returnedRoles;
        }

        public bool IsAssertion
        {
            get
            {
                return false;
            }
            set
            {
                //
            }
        }


        public int? ValidateUserAndRetreiveIdentifer(string username, string password)
        {
            throw new System.NotImplementedException();
        }


        public bool CreateUser(string email, string name, string username, string location, string providerIdentifier, string providerNameIdentifier, string assertionType)
        {
            throw new System.NotImplementedException();
        }

        bool IUserRepository.ValidateUser(string username, string password)
        {
            throw new System.NotImplementedException();
        }

        int? IUserRepository.ValidateUserAndRetreiveIdentifer(string username, string password)
        {
            throw new System.NotImplementedException();
        }

        bool IUserRepository.ValidateUser(X509Certificate2 clientCertificate, out string userName)
        {
            throw new System.NotImplementedException();
        }

        IEnumerable<string> IUserRepository.GetRoles(string userName)
        {
            throw new System.NotImplementedException();
        }

        bool IUserRepository.IsAssertion
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
                throw new System.NotImplementedException();
            }
        }

        bool IUserRepository.CreateUser(string email, string name, string username, string location, string providerIdentifier, string providerNameIdentifier, string assertionType)
        {
            throw new System.NotImplementedException();
        }
    }
}