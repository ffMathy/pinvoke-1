﻿// Copyright (c) to owners found in https://github.com/AArnott/pinvoke/blob/master/COPYRIGHT.md. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

namespace PInvoke
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <content>
    /// Contains the <see cref="BCRYPT_KEY_LENGTHS_STRUCT"/> nested struct.
    /// </content>
    public partial class BCrypt
    {
        /// <summary>
        /// Defines the range of key sizes that are supported by the provider.
        /// This structure is used with the <see cref="PropertyNames.KeyLengths"/> property.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BCRYPT_KEY_LENGTHS_STRUCT
        {
            /// <summary>
            /// The minimum length, in bits, of a key.
            /// </summary>
            public int MinLength;

            /// <summary>
            /// The maximum length, in bits, of a key.
            /// </summary>
            public int MaxLength;

            /// <summary>
            /// The number of bits that the key size can be incremented between dwMinLength and dwMaxLength.
            /// </summary>
            public int Increment;

            /// <summary>
            /// Gets a sequence of allowed key sizes, from smallest to largest.
            /// </summary>
            public IEnumerable<int> KeySizes
            {
                get
                {
                    for (int keyLength = this.MinLength; keyLength <= this.MaxLength; keyLength += this.Increment)
                    {
                        yield return keyLength;
                    }
                }
            }
        }
    }
}
