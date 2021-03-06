﻿/*
 * Copyright (c) Dominick Baier, Brock Allen.  All rights reserved.
 * see license.txt
 */

using System;
using System.ComponentModel.Composition;
using System.IdentityModel.Protocols.WSTrust;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using Thinktecture.IdentityModel.Authorization;
using Thinktecture.IdentityModel.Constants;
using Thinktecture.IdentityServer.Models;
using Thinktecture.IdentityServer.Repositories;
using Thinktecture.IdentityServer.Protocols.Facebook;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Thinktecture.IdentityServer.Protocols.OAuth2
{
    public class OAuth2TokenController : ApiController
    {
        [Import]
        public IUserRepository UserRepository { get; set; }

        [Import]
        public IConfigurationRepository ConfigurationRepository { get; set; }

        [Import]
        public IClientsRepository ClientsRepository { get; set; }

        [Import]
        public ICodeTokenRepository CodeTokenRepository { get; set; }

        public OAuth2TokenController()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public OAuth2TokenController(IUserRepository userRepository, IConfigurationRepository configurationRepository, IClientsRepository clientsRepository, ICodeTokenRepository codeTokenRepository)
        {
            UserRepository = userRepository;
            ConfigurationRepository = configurationRepository;
            ClientsRepository = clientsRepository;
            CodeTokenRepository = codeTokenRepository;
        }

        public HttpResponseMessage Post([FromBody] TokenRequest tokenRequest)
        {
            Tracing.Information("OAuth2 endpoint called.");

            Client client = null;
            var error = ValidateRequest(tokenRequest, out client);
            if (error != null) return error;

            Tracing.Information("Client: " + client.Name);

            // read token type from configuration (typically JWT)
            var tokenType = ConfigurationRepository.Global.DefaultHttpTokenType;

            // switch over the grant type
            if (tokenRequest.Grant_Type.Equals(OAuth2Constants.GrantTypes.Password))
            {
                return ProcessResourceOwnerCredentialRequest(tokenRequest, tokenType, client);
            }
            else if (tokenRequest.Grant_Type.Equals(OAuth2Constants.GrantTypes.AuthorizationCode))
            {
                return ProcessAuthorizationCodeRequest(client, tokenRequest.Code, tokenType);
            }
            else if (string.Equals(tokenRequest.Grant_Type, OAuth2Constants.GrantTypes.RefreshToken, System.StringComparison.Ordinal))
            {
                return ProcessRefreshTokenRequest(client, tokenRequest.Refresh_Token, tokenType);
            }
            else if (string.Equals(tokenRequest.Grant_Type, OAuth2Constants.GrantTypes.Assertion, System.StringComparison.Ordinal))
            {
                return ProcessResourceOwnerCredentialRequest(tokenRequest, tokenType, client);
            }

            Tracing.Error("invalid grant type: " + tokenRequest.Grant_Type);
            return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
        }

        private HttpResponseMessage ProcessResourceOwnerCredentialRequest(TokenRequest request, string tokenType, Client client)
        {
            Tracing.Information("Starting resource owner password credential flow for client: " + client.Name);
            var appliesTo = new EndpointReference(request.Scope);

            if (request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Password))
            {
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
                {
                    Tracing.Error("Invalid resource owner credentials for: " + appliesTo.Uri.AbsoluteUri);
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
                }

                if (UserRepository.ValidateUser(request.Assertion_Type, request.UserName))
                {
                    return CreateTokenResponse(request.UserName, client, appliesTo, tokenType, string.Empty, string.Empty, includeRefreshToken: client.AllowRefreshToken);
                }
                else
                {
                    Tracing.Error("Resource owner credential validation failed: " + request.UserName);
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
                }
            }
            else if (request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Assertion)) {
                if (string.IsNullOrWhiteSpace(request.Assertion) || string.IsNullOrWhiteSpace(request.Assertion_Type))
                {
                    Tracing.Error("Invalid resource owner credentials for: " + appliesTo.Uri.AbsoluteUri);
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
                }

                // Look at the assertion type now and apply the approapriate check
                UserRepository.IsAssertion = true;
                if (request.Assertion_Type.Equals("https://graph.facebook.com/me"))
                {
                    //Validate user token with Facebook
                    FacebookUser user;
                    try
                    {
                        user = new FacebookValidator(request.Assertion).ValidateAccessToken();
                    }
                    catch (Exception ex)
                    {
                        Tracing.Error("Invalid resource owner credentials for: " + appliesTo.Uri.AbsoluteUri);
                        return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
                    }

                    if (user == null)
                    {
                        Tracing.Error("Resource owner credential validation failed: " + request.UserName);
                        return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
                    }

                    //Validate the user in the users store and return the users actual system id for the claims
                    int? userID = UserRepository.ValidateUserAndRetreiveIdentifer(request.Assertion_Type, user.Id);
                    if (!userID.HasValue)
                    {
                        //We need to register this user and then issue a token!
                        if (UserRepository.CreateUser(user.Email, user.Name, 
                            user.Email, user.Location, user.Id, user.UserName, request.Assertion_Type))
                        {
                            userID = UserRepository.ValidateUserAndRetreiveIdentifer(request.Assertion_Type, user.Id);

                            //Send the user onto the profiling queue
                            string connectionString = "Endpoint=sb://spotrbus.servicebus.windows.net/;SharedSecretIssuer=owner;SharedSecretValue=Df96DmLDWtkBKf5gpscsrvP2RDOF0oulzy03isjqIA4=";

                            BrokeredMessage message = new BrokeredMessage(request.Assertion);
                            message.Label = userID.ToString();
                            QueueClient queueClient = QueueClient.CreateFromConnectionString(connectionString, "facebook-profiling-queue");
                            queueClient.Send(message);

                            return CreateTokenResponse(userID.Value.ToString(), client, appliesTo, tokenType,
                            request.Assertion_Type, request.Assertion, includeRefreshToken: client.AllowRefreshToken);
                        }
                        else
                        {
                            Tracing.Error("Resource owner credential validation failed: " + request.UserName);
                            return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
                        }
                    }

                    return CreateTokenResponse(userID.Value.ToString(), client, appliesTo, tokenType,
                            request.Assertion_Type, request.Assertion, includeRefreshToken: client.AllowRefreshToken);
                }

            } 
           
            return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
            
        }


        private HttpResponseMessage ProcessAuthorizationCodeRequest(Client client, string code, string tokenType)
        {
            Tracing.Information("Processing authorization code token request for client: " + client.Name);
            return ProcessCodeTokenRequest(client, code, tokenType);
        }
        
        private HttpResponseMessage ProcessRefreshTokenRequest(Client client, string refreshToken, string tokenType)
        {
            Tracing.Information("Processing refresh token request for client: " + client.Name);
            return ProcessCodeTokenRequest(client, refreshToken, tokenType);
        }

        private HttpResponseMessage ProcessCodeTokenRequest(Client client, string codeToken, string tokenType)
        {
            // 1. get code token from DB - if not exists: error
            CodeToken token;
            if (CodeTokenRepository.TryGetCode(codeToken, out token))
            {
                CodeTokenRepository.DeleteCode(token.Code);

                // 2. make sure the client is the same - if not: error
                if (token.ClientId == client.ID)
                {
                    // 3. call STS 
                    return CreateTokenResponse(token.UserName, client, new EndpointReference(token.Scope), tokenType, string.Empty, string.Empty, 
                        includeRefreshToken: client.AllowRefreshToken);
                }

                Tracing.Error("Invalid client for refresh token. " + client.Name + " / " + codeToken);
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
            }

            Tracing.Error("Refresh token not found. " + client.Name + " / " + codeToken);
            return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
        }

        private HttpResponseMessage CreateTokenResponse(string userName, Client client, EndpointReference scope, string tokenType, 
            string assertionType, string assertion, bool includeRefreshToken)
        {
            var auth = new AuthenticationHelper();
            Claim[] claims = new Claim[] {
                            new Claim(Constants.Claims.Client, client.Name),
                            new Claim(Constants.Claims.Scope, scope.Uri.AbsoluteUri)
                        };

            if (!string.IsNullOrEmpty(assertion) || !string.IsNullOrEmpty(assertionType) 
                || !string.IsNullOrWhiteSpace(assertion) || !string.IsNullOrWhiteSpace(assertionType)) {
                Claim[] assertionClaims = new Claim[] {
                    new Claim(Constants.Claims.AssertionType, assertionType),
                    new Claim(Constants.Claims.Assertion, assertion)
                };
                Array.Resize(ref claims, claims.Length + assertionClaims.Length);
                assertionClaims.CopyTo(claims, claims.Length - assertionClaims.Length);
            }

            var principal = auth.CreatePrincipal(userName, "OAuth2", claims);

            if (!ClaimsAuthorization.CheckAccess(principal, Constants.Actions.Issue, Constants.Resources.OAuth2))
            {
                Tracing.Error("OAuth2 endpoint authorization failed for user: " + userName);
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidGrant);
            }

            var sts = new STS();
            TokenResponse tokenResponse;
            if (sts.TryIssueToken(scope, principal, tokenType, out tokenResponse))
            {
                if (includeRefreshToken)
                {
                    tokenResponse.RefreshToken = CodeTokenRepository.AddCode(CodeTokenType.RefreshTokenIdentifier, client.ID, userName, scope.Uri.AbsoluteUri);
                }

                var resp = Request.CreateResponse<TokenResponse>(HttpStatusCode.OK, tokenResponse);
                return resp;
            }
            else
            {
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidRequest);
            }
        }

        private HttpResponseMessage OAuthErrorResponseMessage(string error)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                string.Format("{{ \"{0}\": \"{1}\" }}", OAuth2Constants.Errors.Error, error));
        }

        private HttpResponseMessage ValidateRequest(TokenRequest request, out Client client)
        {
            client = null;

            if (request == null)
            {
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidRequest);
            }

            // grant type is required
            if (string.IsNullOrWhiteSpace(request.Grant_Type))
            {
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
            }

            // check supported grant types
            if (!request.Grant_Type.Equals(OAuth2Constants.GrantTypes.AuthorizationCode) &&
                !request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Password) &&
                !request.Grant_Type.Equals(OAuth2Constants.GrantTypes.RefreshToken) &&
                !request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Assertion))
            {
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
            }

            // resource owner password flow requires a well-formed scope
            if (request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Password))
            {
                Uri appliesTo;
                if (!Uri.TryCreate(request.Scope, UriKind.Absolute, out appliesTo))
                {
                    Tracing.Error("Malformed scope: " + request.Scope);
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidScope);
                }
                
                Tracing.Information("OAuth2 endpoint called for scope: " + request.Scope);
            }

            if (!ValidateClient(out client))
            {
                Tracing.Error("Invalid client: " + ClaimsPrincipal.Current.Identity.Name);
                return OAuthErrorResponseMessage(OAuth2Constants.Errors.InvalidClient);
            }

            // validate grant types against global and client configuration
            if (request.Grant_Type.Equals(OAuth2Constants.GrantTypes.AuthorizationCode))
            {
                if (!ConfigurationRepository.OAuth2.EnableCodeFlow ||
                    !client.AllowCodeFlow)
                {
                    Tracing.Error("Code flow not allowed for client");
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
                }
            }

            if (request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Password) || request.Grant_Type.Equals(OAuth2Constants.GrantTypes.Assertion))
            {
                if (!ConfigurationRepository.OAuth2.EnableResourceOwnerFlow ||
                    !client.AllowResourceOwnerFlow)
                {
                    Tracing.Error("Resource owner password flow not allowed for client");
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
                }
            }

            if (request.Grant_Type.Equals(OAuth2Constants.GrantTypes.RefreshToken))
            {
                if (!client.AllowRefreshToken)
                {
                    Tracing.Error("Refresh tokens not allowed for client");
                    return OAuthErrorResponseMessage(OAuth2Constants.Errors.UnsupportedGrantType);
                }
            }

            return null;
        }

        private bool ValidateClient(out Client client)
        {
            client = null;

            if (!ClaimsPrincipal.Current.Identity.IsAuthenticated)
            {
                Tracing.Error("Anonymous client.");
                return false;
            }

            var passwordClaim = ClaimsPrincipal.Current.FindFirst("password");
            if (passwordClaim == null)
            {
                Tracing.Error("No client secret provided.");
                return false;
            }

            return ClientsRepository.ValidateAndGetClient(
                ClaimsPrincipal.Current.Identity.Name,
                passwordClaim.Value,
                out client);
        }
    }
}
