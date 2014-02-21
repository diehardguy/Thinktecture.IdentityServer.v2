/*
 * Copyright (c) Dominick Baier.  All rights reserved.
 * see license.txt
 */

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Thinktecture.IdentityServer.Repositories
{
    public interface IUserRepository
    {
        bool ValidateUser(string username, string password);
        int? ValidateUserAndRetreiveIdentifer(string username, string password);
        bool ValidateUser(X509Certificate2 clientCertificate, out string userName);
        IEnumerable<string> GetRoles(string userName);
        bool IsAssertion { get; set; }
        bool CreateUser(string email, string name, string username, string location,
            string providerIdentifier, string providerNameIdentifier, string assertionType);
    }
}