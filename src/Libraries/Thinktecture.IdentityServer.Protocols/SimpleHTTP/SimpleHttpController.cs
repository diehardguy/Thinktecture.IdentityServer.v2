﻿/*
 * Copyright (c) Dominick Baier, Brock Allen.  All rights reserved.
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

namespace Thinktecture.IdentityServer.Protocols.SimpleHTTP
{
    [ClaimsAuthorize(Constants.Actions.Issue, Constants.Resources.SimpleHttp)]
    public class SimpleHttpController : ApiController
    {
        [Import]
        public IConfigurationRepository ConfigurationRepository { get; set; }

        public SimpleHttpController()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public SimpleHttpController(IConfigurationRepository configurationRepository)
        {
            ConfigurationRepository = configurationRepository;
        }

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            Tracing.Information("Simple HTTP endpoint called.");

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
