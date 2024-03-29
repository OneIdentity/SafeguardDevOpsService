﻿using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal interface ICredentialManager
    {
        void Upsert(string credential, AccountMapping account, CredentialType credentialType);
        bool Matches(string credential, AccountMapping account, CredentialType credentialType);
        void Clear();
    }
}
