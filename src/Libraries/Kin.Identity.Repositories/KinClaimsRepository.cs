/*
 * Copyright (c) Dominick Baier.  All rights reserved.
 * see license.txt
 */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Web.Profile;
using System.Web.Security;
using Thinktecture.IdentityServer;
using Thinktecture.IdentityServer.TokenService;
using Thinktecture.IdentityServer.Core.Repositories;
using Thinktecture.IdentityServer.Repositories;
using Kin.Identity.Repository.Interfaces;
using System.ComponentModel.Composition;


namespace Kin.Identity.Repository
{
    public class KinClaimsRepository : IClaimsRepository
    {
        [Import]
        public IKinUserRepository KinUserRepository { get; set; }

        private const string ProfileClaimPrefix = "http://identityserver.thinktecture.com/claims/profileclaims/";

        public KinClaimsRepository()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public virtual IEnumerable<Claim> GetClaims(ClaimsPrincipal principal, RequestDetails requestDetails)
        {
            var userName = principal.Identity.Name;
            var claims = new List<Claim>(from c in principal.Claims select c);

            if (userName.StartsWith("TT\\") && userName.Length > 3)
            {
                // email address
                var membership = Membership.FindUsersByName(userName)[userName];
                if (membership != null)
                {
                    string email = membership.Email;
                    if (!String.IsNullOrEmpty(email))
                    {
                        claims.Add(new Claim(ClaimTypes.Email, email));
                    }
                }
            }
            else 
            {
                // We are using the users ID in here as they may not have specified username yet etc
                // These will not be TT users
                int uid = 0;
                if (int.TryParse(userName.ToString(), out uid)) {
                    if (uid > 0) {
                        var user = KinUserRepository.GetUser(uid);
                        if (user != null)
                        {
                            if (!String.IsNullOrEmpty(user.Email))
                            {
                                claims.Add(new Claim(ClaimTypes.Email, user.Email));
                            }
                        }
                    }
                }
            }

            // roles
            GetRolesForToken(userName).ToList().ForEach(role => claims.Add(new Claim(ClaimTypes.Role, role)));

            // profile claims
            claims.AddRange(GetProfileClaims(userName));

            return claims;
        }

        protected virtual IEnumerable<Claim> GetProfileClaims(string userName)
        {
            var claims = new List<Claim>();

            if (ProfileManager.Enabled)
            {
                var profile = ProfileBase.Create(userName, true);
                if (profile != null)
                {
                    foreach (SettingsProperty prop in ProfileBase.Properties)
                    {
                        object value = profile.GetPropertyValue(prop.Name);
                        if (value != null)
                        {
                            if (!string.IsNullOrWhiteSpace(value.ToString()))
                            {
                                claims.Add(new Claim(GetProfileClaimType(prop.Name.ToLowerInvariant()), value.ToString()));
                            }
                        }
                    }
                }
            }

            return claims;
        }

        protected virtual string GetProfileClaimType(string propertyName)
        {
            if (StandardClaimTypes.Mappings.ContainsKey(propertyName))
            {
                return StandardClaimTypes.Mappings[propertyName];
            }
            else
            {
                return string.Format("{0}{1}", ProfileClaimPrefix, propertyName);
            }
        }

        public virtual IEnumerable<string> GetSupportedClaimTypes()
        {
            var claimTypes = new List<string>
            {
                ClaimTypes.Name,
                ClaimTypes.Email,
                ClaimTypes.Role
            };

            if (ProfileManager.Enabled)
            {
                foreach (SettingsProperty prop in ProfileBase.Properties)
                {
                    claimTypes.Add(GetProfileClaimType(prop.Name.ToLowerInvariant()));
                }
            }

            return claimTypes;
        }

        protected virtual IEnumerable<string> GetRolesForToken(string userName)
        {
            var returnedRoles = new List<string>();
            if (userName.StartsWith("TT\\") && userName.Length > 3)
            {
                if (Roles.Enabled)
                {
                    var roles = Roles.GetRolesForUser(userName);
                    returnedRoles = roles.Where(role => !(role.StartsWith(Constants.Roles.InternalRolesPrefix))).ToList();
                }
            }
            else
            {
                int uid = 0;
                if (int.TryParse(userName.ToString(), out uid)) {
                    if (uid > 0) {
                        var roles = KinUserRepository.GetUserRoles(uid);
                    }
                }
            }
            return returnedRoles;
        }
    }
}