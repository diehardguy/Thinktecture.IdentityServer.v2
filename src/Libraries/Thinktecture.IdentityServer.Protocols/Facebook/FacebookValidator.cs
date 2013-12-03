using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Facebook;
using System.Security.Claims;
using System.Security.Principal;
using Thinktecture.IdentityServer.Protocols.Facebook;
using Thinktecture.IdentityModel.Constants;

namespace Thinktecture.IdentityServer.Protocols.Facebook
{
    public class FacebookValidator
    {
        private string _accessToken;
        public string AccessToken
        {
            get
            {
                return _accessToken;
            }
            set
            {
                _accessToken = value;
            }
        }

        public FacebookValidator(string accessToken)
        {
            _accessToken = accessToken;
        }

        public FacebookUser ValidateAccessToken()
        {
            var encoding = new UTF8Encoding();
            byte[] keyBytes = encoding.GetBytes("dbbdab63d577eff46042b45cfb3b0c5b");
            byte[] dataBytes = encoding.GetBytes(_accessToken);
            HMACSHA256 hmac = new HMACSHA256(keyBytes);
            byte[] hmacBytes = hmac.ComputeHash(dataBytes);

            //Create Facebook Client
            FacebookClient client = new FacebookClient(_accessToken);

            //Adding appsecret_Proof to Facebook request
            var parameters = new Dictionary<string, object>();
            string hexAppSecretProof = System.BitConverter.ToString(hmacBytes).Replace("-", "").ToLower();
            parameters["appsecret_proof"] = hexAppSecretProof.ToLower();

            //Get the users data from facebook
            try
            {
                dynamic me = client.Get("me?fields=id,email,first_name,last_name,about,location,website", parameters);
                return new FacebookUser() {
                    Id = me.id,
                    Email = me.email,
                    FirstName = me.first_name,
                    LastName = me.last_name,
                    About = me.about,
                    Location = me.location,
                    Website = me.website
                };

            }
            catch (FacebookApiException ex)
            {
                throw ex;
            }
        }
    }
}
