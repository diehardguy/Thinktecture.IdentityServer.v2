using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kin.Identity.Repository.Domain;

namespace Kin.Identity.Repository.Interfaces
{
    public interface IKinUserRepository
    {
        IEnumerable<string> GetRoles();
        bool ValidateUser(string identifier, string secret, bool isAssertion);
        User GetUser(string identifier, string secret, bool isAssertion);
        User GetUser(int userID);
        IEnumerable<string> GetUserRoles(string userName);
        IEnumerable<string> GetUserRoles(int userID);
        bool CreateUser(string email, string name, string username, string location, string providerIdentifier,
            string providerNameIdentifier, string assertionType);
    }
}
