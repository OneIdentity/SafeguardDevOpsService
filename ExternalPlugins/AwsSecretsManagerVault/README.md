# AWS Secrets Manager Vault Plugin

The AWS Secrets Manager Vault plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into an AWS secrets vault.

## AWS Secrets Manager Vault plugin specific configuration

***Plugin Details***

* **Supported Credential Types** - Password, SSH key and API key
* **Supports Reverse Flow** - Yes
* **Access Key Id** - Access key id used to identify the AWS account that has access to the AWS secrets vault. The Access key id must be created for the AWS account that will be used to store the secrets. Once the access key is created, it will be associated with a secret that must be stored as an account password in SPP and account should be used as the vault account.
* **AWS Region** - Identifies the AWS region.
