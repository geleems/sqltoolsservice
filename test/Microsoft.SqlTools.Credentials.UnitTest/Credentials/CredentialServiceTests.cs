//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.SqlTools.Credentials.Contracts;
using Microsoft.SqlTools.Credentials.Linux;
using Microsoft.SqlTools.Hosting.UnitTests.Common.RequestContextMocking;
using Xunit;

namespace Microsoft.SqlTools.Credentials.UnitTest.Credentials
{
    /// <summary>
    /// Credential Service tests that should pass on all platforms, regardless of backing store.
    /// These tests run E2E, storing values in the native credential store for whichever platform
    /// tests are being run on
    /// </summary>
    public class CredentialServiceTests : IDisposable
    {
        private static readonly StoreConfig Config = new StoreConfig
        {
            CredentialFolder = ".testsecrets", 
            CredentialFile = "sqltestsecrets.json", 
            IsRelativeToUserHomeDir = true
        };

        private const string CredentialId = "Microsoft_SqlToolsTest_TestId";
        private const string Password1 = "P@ssw0rd1";
        private const string Password2 = "2Pass2Furious";

        private const string OtherCredId = CredentialId + "2345";
        private const string OtherPassword = CredentialId + "2345";

        // Test-owned credential store used to clean up before/after tests to ensure code works as expected 
        // even if previous runs stopped midway through
        private readonly ICredentialStore credStore;
        private readonly CredentialService service;
        /// <summary>
        /// Constructor called once for every test
        /// </summary>
        public CredentialServiceTests()
        {
            credStore = CredentialService.GetStoreForOS(Config);
            service = new CredentialService(credStore, Config);
            DeleteDefaultCreds();
        }
        
        public void Dispose()
        {
            DeleteDefaultCreds();
        }

        private void DeleteDefaultCreds()
        {
            credStore.DeletePassword(CredentialId);
            credStore.DeletePassword(OtherCredId);

#if !WINDOWS_ONLY_BUILD
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string credsFolder = ((LinuxCredentialStore)credStore).CredentialFolderPath;
                if (Directory.Exists(credsFolder))
                {
                    Directory.Delete(credsFolder, true);
                }
            }
#endif
        }

        [Fact]
        public void SaveCredentialThrowsIfCredentialIdMissing()
        {
            // If: I request to save a credential
            var efv = new EventFlowValidator<bool>()
                .AddSimpleErrorValidation((msg, code) => Assert.Contains("ArgumentException", msg))
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(null), efv.Object);
            
            // Then: I should get an argument exception back
            efv.Validate();
        }

        [Fact]
        public void SaveCredentialThrowsIfPasswordMissing()
        {
            // If: I request to save a credential with a missing password
            var efv = new EventFlowValidator<bool>()
                .AddSimpleErrorValidation((msg, code) => Assert.Contains("Argument", msg))
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId), efv.Object);
            
            // Then: I should get an argument exception
            efv.Validate();
        }

        [Fact]
        public void SaveCredentialWorksForSingleCredential()
        {
            // If: I request to save a single credential
            var efv = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password1), efv.Object);
            
            // Then: It should be successful
            efv.Validate();
        }

        [Fact]
        public void SaveCredentialWorksForEmptyPassword()
        {
            // If: I request to save a single credential with a blank password
            var efv = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, string.Empty), efv.Object);
            
            // Then: It should be successful
            efv.Validate();
        }

        [Fact]
        public void SaveCredentialSupportsSavingCredentialMultipleTimes()
        {
            // If: I request to save a credential
            // Then: It should be successful
            var efv1 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password1), efv1.Object);
            efv1.Validate();
            
            // If: I request to save the credential a second time
            var efv2 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password2), efv2.Object);
            
            // Then: It should be successful
            efv1.Validate();
        }

        [Fact]
        public void ReadCredentialWorksForSingleCredential()
        {
            // If: I request to save a credential
            // Then: It should be successful
            var efv1 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password1), efv1.Object);
            efv1.Validate();
            
            // If: I request to read the credential I just saved
            var efv2 = new EventFlowValidator<Credential>()
                .AddResultValidation(cred => Assert.Equal(Password1, cred.Password))
                .Complete();
            service.HandleReadCredentialRequest(new Credential(CredentialId, null), efv2.Object);
            
            // Then: It should read back the stored password
            efv2.Validate();
        }

        [Fact]
        public void ReadCredentialWorksForMultipleCredentials()
        {
            // If: I save a password twice
            // Then: It should be successful
            var efv1 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password1), efv1.Object);
            var efv2 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(OtherCredId, OtherPassword), efv2.Object);
            
            // If: I read the credentials back
            var efv3 = new EventFlowValidator<Credential>()
                .AddResultValidation(cred => Assert.Equal(Password1, cred.Password))
                .Complete();
            service.HandleReadCredentialRequest(new Credential(CredentialId), efv3.Object);
            
            var efv4 = new EventFlowValidator<Credential>()
                .AddResultValidation(cred => Assert.Equal(OtherPassword, cred.Password))
                .Complete();
            service.HandleReadCredentialRequest(new Credential(OtherCredId), efv4.Object);
            
            // Then: All requests should be successful. The credentials should be returned
            efv1.Validate();
            efv2.Validate();
            efv3.Validate();
            efv4.Validate();
        }

        [Fact]
        public void ReadCredentialHandlesPasswordUpdate()
        {
            // If: I request to save a credential twice and then read it back
            var efv1 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password1), efv1.Object);
            efv1.Validate();
            
            var efv2 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password2), efv2.Object);

            var efv3 = new EventFlowValidator<Credential>()
                .AddResultValidation(cred => Assert.Equal(Password2, cred.Password))
                .Complete();
            service.HandleReadCredentialRequest(new Credential(CredentialId), efv3.Object);
            
            // Then: It should return the 
            efv1.Validate();
            efv2.Validate();
            efv3.Validate();
        }

        [Fact]
        public void ReadCredentialThrowsIfCredentialIsNull()
        {
            // If: I request to read a null credential
            var efv = new EventFlowValidator<Credential>()
                .AddSimpleErrorValidation((msg, code) => Assert.Contains("ArgumentNullException", msg))
                .Complete();
            service.HandleReadCredentialRequest(null, efv.Object);
            
            // Then: It should throw
            efv.Validate();
        }
        
        [Fact]
        public void ReadCredentialThrowsIfIdMissing()
        {
            // If: I request to read with a missing Id
            var efv = new EventFlowValidator<Credential>()
                .AddSimpleErrorValidation((msg, code) => Assert.Contains("ArgumentException", msg))
                .Complete();
            service.HandleReadCredentialRequest(new Credential(), efv.Object);
            
            // Then: It should throw
            efv.Validate();
        }

        [Fact]
        public void ReadCredentialReturnsNullPasswordForMissingCredential()
        {
            // Given: Credential whose password doesn't exist
            const string credWithNoPassword = "Microsoft_SqlTools_CredThatDoesNotExist";
            
            // If: I request to read with a credential that doesn't exist
            var efv = new EventFlowValidator<Credential>()
                .AddResultValidation(cred =>
                    {
                        Assert.NotNull(cred);
                        Assert.Equal(credWithNoPassword, cred.CredentialId);
                        Assert.Null(cred.Password);
                    })
                .Complete();
            service.HandleReadCredentialRequest(new Credential(credWithNoPassword), efv.Object);
            
            // Then: I should get back an empty credential
            efv.Validate();
        }
        
        [Fact]
        public void DeleteCredentialThrowsIfIdMissing()
        {
            // If: I delete a credential that is missing an ID
            var efv = new EventFlowValidator<bool>()
                .AddSimpleErrorValidation((msg, code) => Assert.Contains("ArgumentException", msg))
                .Complete();
            service.HandleDeleteCredentialRequest(new Credential(), efv.Object);
            
            // Then: I should get an exception
            efv.Validate();
        }

        [Fact]
        public void DeleteCredentialReturnsTrueOnlyIfCredentialExisted()
        {
            // Setup: Save a credential
            var efv1 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleSaveCredentialRequest(new Credential(CredentialId, Password1), efv1.Object);
            efv1.Validate();

            // If: I delete the credential
            // Then: It should be deleted
            var efv2 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.True)
                .Complete();
            service.HandleDeleteCredentialRequest(new Credential(CredentialId), efv2.Object);
            efv2.Validate();
            
            // If: I attempt to delete the credential again
            // Then: It should not exist
            var efv3 = new EventFlowValidator<bool>()
                .AddResultValidation(Assert.False)
                .Complete();
            service.HandleDeleteCredentialRequest(new Credential(CredentialId), efv3.Object);
            efv3.Validate();
        }

    }
}

