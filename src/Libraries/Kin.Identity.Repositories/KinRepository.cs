/*
 * Copyright (c) Dominick Baier.  All rights reserved.
 * see license.txt
 */

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web.Security;
using Thinktecture.IdentityServer.Repositories;
using Kin.Identity.Repository.Interfaces;
using Thinktecture.IdentityServer;

namespace Kin.Identity.Repository
{
    public class KinRepository : IUserRepository
    {
        [Import]
        public IClientCertificatesRepository Repository { get; set; }

        [Import]
        public IKinUserRepository KinUserRepository { get; set; }
        private bool isAssertion;
        public KinRepository()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public virtual bool ValidateUser(string userName, string password)
        {
            if (userName.StartsWith("TT\\") && userName.Length > 3)
            {
                return Membership.ValidateUser(userName, password);
            }
            else
            {
                return KinUserRepository.ValidateUser(userName, password, isAssertion);
            }
        }

        public int? ValidateUserAndRetreiveIdentifer(string username, string password)
        {
            var u = KinUserRepository.GetUser(username, password, this.isAssertion);
            if (u == null)
                return null;
            else
                return u.UserId;
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
                if (userName.StartsWith("TT\\") && userName.Length > 3)
                {
                    var roles = Roles.GetRolesForUser(userName);
                    returnedRoles = roles.Where(role => role.StartsWith(Constants.Roles.InternalRolesPrefix)).ToList();
                }
                else
                {
                    var roles = KinUserRepository.GetUserRoles(userName);
                    returnedRoles = roles.ToList();
                }
            }

            return returnedRoles;
        }

        

        public bool IsAssertion
        {
            get
            {
                return isAssertion;
            }
            set
            {
                isAssertion = value;
            }
        }


        public bool CreateUser(string email, string name, string username, string location, string providerIdentifier, 
            string providerNameIdentifier, string assertionType)
        {
            return KinUserRepository.CreateUser(email, name, username, location, providerIdentifier, providerNameIdentifier, assertionType);
        }
    }
}