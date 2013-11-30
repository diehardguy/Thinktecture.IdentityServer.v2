/*
 * Copyright (c) Damian Young.  All rights reserved.
 * see license.txt
 */

using System.ComponentModel.Composition;
using System.IdentityModel.Protocols.WSTrust;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using Thinktecture.IdentityModel.Authorization.WebApi;
using Thinktecture.IdentityModel.Constants;
using Thinktecture.IdentityServer.Repositories;

namespace Thinktecture.IdentityServer.Protocols.FacebookAccessToken
{
    [ClaimsAuthorize(Constants.Actions.Issue, Constants.Resources.FacebookAccessToken)]
    public class FacebookAccessTokenController : ApiController
    {
                public IConfigurationRepository ConfigurationRepository { get; set; }

        public FacebookAccessTokenController()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public FacebookAccessTokenController(IConfigurationRepository configurationRepository)
        {
            ConfigurationRepository = configurationRepository;
        }

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            Tracing.Information("Facebook HTTP endpoint called.");

            var query = request.Headers;
            var realm = query.FirstOrDefault(h => h.Key.Equals("Realm", System.StringComparison.OrdinalIgnoreCase)).Value.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(realm))
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "invalid request.");
            }

            EndpointReference ep = new EndpointReference(realm);
           
            TokenResponse tokenResponse;
            var sts = new STS();
            if (sts.TryIssueToken(ep, ClaimsPrincipal.Current, TokenTypes.JsonWebToken, out tokenResponse))
            {
                var resp = request.CreateResponse<TokenResponse>(HttpStatusCode.OK, tokenResponse);
                return resp;
            }
            else
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "invalid request.");
            }
        }

    }
}
