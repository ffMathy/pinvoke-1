﻿// Copyright (c) to owners found in https://github.com/AArnott/pinvoke/blob/master/COPYRIGHT.md. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using PInvoke;
using Xunit;
using static PInvoke.NCrypt;

public class NCrypt
{
    [Fact]
    public void OpenStorageProvider()
    {
        using (var provider = NCryptOpenStorageProvider(KeyStorageProviders.MS_KEY_STORAGE_PROVIDER))
        {
        }
    }

    [Fact(Skip = "fails, and I don't know how to fix it")]
    public void CreatePersistedKey()
    {
        using (var provider = NCryptOpenStorageProvider(KeyStorageProviders.MS_KEY_STORAGE_PROVIDER))
        {
            using (var key = NCryptCreatePersistedKey(provider, BCrypt.AlgorithmIdentifiers.BCRYPT_ECDSA_P256_ALGORITHM))
            {
                NCryptFinalizeKey(key).ThrowOnError();
            }
        }
    }

    [Fact]
    public void GetPropertyOfT()
    {
        using (var provider = NCryptOpenStorageProvider(KeyStorageProviders.MS_KEY_STORAGE_PROVIDER))
        {
            var actual = (KeyStorageImplementationType)NCryptGetProperty<int>(provider, KeyStoragePropertyIdentifiers.NCRYPT_IMPL_TYPE_PROPERTY);
            Assert.Equal(KeyStorageImplementationType.NCRYPT_IMPL_SOFTWARE_FLAG, actual);
        }
    }
}
