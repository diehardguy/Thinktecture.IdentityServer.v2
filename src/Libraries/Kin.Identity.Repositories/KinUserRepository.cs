using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Security;
using Kin.Identity.Repository.Interfaces;
using Kin.Identity.Repository.Domain;


namespace Kin.Identity.Repository
{
    public class KinUserRepository : IKinUserRepository
    {
        public IEnumerable<string> GetRoles()
        {
            using (var context = new KinIdentityEntities())
            {
                return context.Roles.Select(p => p.Name).ToList();
            }
        }

        public bool ValidateUser(string identifier, string secret, bool isAssertion)
        {

            User u = GetKinUser(identifier, secret, isAssertion);

            if (u == null)
            {
                return false;
            }

            return true;


            /*var sha256 = SHA256.Create();
               
            using (var ms = new MemoryStream(System.Text.Encoding.Unicode.GetBytes(secret)))
            {
                var bytes = sha256.ComputeHash(ms);
                if (user.Password.SequenceEqual(bytes))
                {
                    return true;
                }
            }*/
        }

        public IEnumerable<string> GetUserRoles(string userName)
        {
            using (var context = new KinIdentityEntities())
            {
                return (from p in context.Users
                        join r in context.UserRoles on p.UserId equals r.UserId
                        join x in context.Roles on r.RoleId equals x.RoleId
                        where p.UserProfile.Username == userName
                        select x.Name).ToList();
            }
        }

        public IEnumerable<string> GetUserRoles(int userID)
        {
            using (var context = new KinIdentityEntities())
            {
                return (from p in context.Users
                        join r in context.UserRoles on p.UserId equals r.UserId
                        join x in context.Roles on r.RoleId equals x.RoleId
                        where p.UserId == userID
                        select x.Name).ToList();
            }
        }


        public User GetUser(string identifier, string secret, bool isAssertion)
        {
            User u = GetKinUser(identifier, secret, isAssertion);

            if (u == null)
            {
                return null;
            }

            return u;
        }

        public User GetUser(int userID)
        {
            using (var context = new KinIdentityEntities())
            {
                var user = new User();
                //Check Users Social Connections
                user = context.Users.FirstOrDefault(u =>
                    u.UserId.Equals(userID) &&
                    u.IsApproved == true &&
                    u.IsLockedOut == false);

                if (user == null)
                {
                    return null;
                }
                return user;
            }
        }

        private User GetKinUser(string identifier, string secret, bool isAssertion)
        {
            using (var context = new KinIdentityEntities())
            {
                var user = new User();
                if (isAssertion)
                {
                    //Check Users Social Connections
                    user = context.Users.FirstOrDefault(u =>
                        u.UserProfile.UserSocialConnections.Any(usc =>
                            usc.IsIdentity == true &&
                            usc.ProviderIdentifier.Equals(secret) &&
                            usc.Active == true &&
                            usc.Deleted == false &&
                            usc.SocialProvider.AssertionType.Equals(identifier) &&
                            usc.SocialProvider.IsIdentityProvider == true));
                }
                else
                {
                    user = context.Users.FirstOrDefault(u => u.Email == identifier &&
                        u.Password == secret);
                }

                if (user == null)
                {
                    return null;
                }
                return user;
            }
        }


        public bool CreateUser(string email, string name, string username, string location, string providerIdentifier, 
            string providerNameIdentifier, string assertionType)
        {
            using (var context = new KinIdentityEntities())
            {
                //Check that the assertion type is valid
                var socialProvider = 
                    context.SocialProviders.Where(p => 
                        p.AssertionType.Equals(assertionType) && p.IsIdentityProvider==true).FirstOrDefault();

                if (socialProvider != null)
                {
                    UserSocialConnection connection = new UserSocialConnection
                    {
                        Active=true,
                        Deleted = false,
                        IsIdentity = true,
                        ProviderIdentifier = providerIdentifier,
                        ProviderNameIdentifier = providerNameIdentifier,
                        SocialProviderId = socialProvider.SocialProviderId,
                        DateCreated = DateTime.Now
                    };

                    User u = new User
                    {
                        Email = email,
                        IsApproved = true,
                        IsLockedOut = false,
                        UserProfile = new UserProfile
                        {
                            Name = name,
                            Location = location,
                            Active = true,
                            Deleted = false,
                            IsPrivate = false,
                            DateCreated = DateTime.Now
                        },
                        DateCreated = DateTime.Now
                    };

                    u.UserProfile.UserSocialConnections.Add(connection);
                    context.Users.Add(u);

                    try
                    {
                        context.SaveChanges();
                        return true;
                    }
                    catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
