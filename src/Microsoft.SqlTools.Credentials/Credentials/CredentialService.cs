//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Runtime.InteropServices;
using Microsoft.SqlTools.Credentials.Contracts;
using Microsoft.SqlTools.Credentials.Linux;
using Microsoft.SqlTools.Credentials.OSX;
using Microsoft.SqlTools.Credentials.Win32;
using Microsoft.SqlTools.Hosting;
using Microsoft.SqlTools.Hosting.Protocol;
using Microsoft.SqlTools.Hosting.Utility;

namespace Microsoft.SqlTools.Credentials
{
    /// <summary>
    /// Service responsible for securing credentials in a platform-neutral manner. This provides
    /// a generic API for read, save and delete credentials
    /// </summary>
    public class CredentialService
    {
        private const string DefaultSecretsFolder = ".sqlsecrets";
        private const string DefaultSecretsFile = "sqlsecrets.json";
        

        /// <summary>
        /// Singleton service instance
        /// </summary>
        private static readonly Lazy<CredentialService> instance 
            = new Lazy<CredentialService>(() => new CredentialService());

        /// <summary>
        /// Gets the singleton service instance
        /// </summary>
        public static CredentialService Instance => instance.Value;

        private ICredentialStore credStore;

        /// <summary>
        /// Default constructor is private since it's a singleton class
        /// </summary>
        private CredentialService()
            : this(null, new StoreConfig
                { CredentialFolder = DefaultSecretsFolder, CredentialFile = DefaultSecretsFile, IsRelativeToUserHomeDir = true})
        {
        }
        
        /// <summary>
        /// Internal for testing purposes only
        /// </summary>
        internal CredentialService(ICredentialStore store, StoreConfig config)
        {
            credStore = store ?? GetStoreForOS(config);
        }
        
        public void InitializeService(IServiceHost serviceHost)
        {
            // Register request and event handlers with the Service Host
            serviceHost.SetRequestHandler(ReadCredentialRequest.Type, HandleReadCredentialRequest);
            serviceHost.SetRequestHandler(SaveCredentialRequest.Type, HandleSaveCredentialRequest);
            serviceHost.SetRequestHandler(DeleteCredentialRequest.Type, HandleDeleteCredentialRequest);
        }

        /// <summary>
        /// Internal for testing purposes only
        /// </summary>
        internal static ICredentialStore GetStoreForOS(StoreConfig config)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new Win32CredentialStore();
            }
#if !WINDOWS_ONLY_BUILD
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new OSXCredentialStore();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxCredentialStore(config);
            }
#endif
            throw new InvalidOperationException("Platform not currently supported");
        }

        #region Request Handlers
        
        internal void HandleReadCredentialRequest(Credential credential, RequestContext<Credential> requestContext)
        {
            HandleRequest(() => ReadCredential(credential), requestContext, "HandleReadCredentialRequest");
        }

        internal void HandleSaveCredentialRequest(Credential credential, RequestContext<bool> requestContext)
        {
            HandleRequest(() => SaveCredential(credential), requestContext, "HandleSaveCredentialRequest");
        }

        internal void HandleDeleteCredentialRequest(Credential credential, RequestContext<bool> requestContext)
        {
            HandleRequest(() => DeleteCredential(credential), requestContext, "HandleDeleteCredentialRequest");
        }
        
        #endregion

        #region Private Helpers
        
        private bool DeleteCredential(Credential credential)
        {
            Credential.ValidateForLookup(credential);
            return credStore.DeletePassword(credential.CredentialId);
        }
        
        private Credential ReadCredential(Credential credential)
        {
            Credential.ValidateForLookup(credential);

            Credential result = Credential.Copy(credential);
            string password;
            if (credStore.TryGetPassword(credential.CredentialId, out password))
            {
                result.Password = password;
            }
            return result;
        }
        
        private bool SaveCredential(Credential credential)
        {
            Credential.ValidateForSave(credential);
            return credStore.Save(credential);
        }
        
        private void HandleRequest<T>(Func<T> handler, RequestContext<T> requestContext, string requestType)
        {
            Logger.Write(LogLevel.Verbose, requestType);

            try
            {
                T result = handler();
                requestContext.SendResult(result);
            }
            catch (Exception ex)
            {
                requestContext.SendError(ex.ToString());
            }
        }
        
        #endregion

    }
}
