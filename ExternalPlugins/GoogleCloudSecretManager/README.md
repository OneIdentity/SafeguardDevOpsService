# Google Cloud Secret Manager Plugin

The Google Cloud Secret Manager plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into a Google Cloud Secret Manager vault. It also supports the reverse direction of pulling a secret in Google Cloud and pushing it into Safeguard for Privileged Passwords.

## Google Secret Manager plugin specific configuration

***Plugin Details***

* **Supported Credential Types** - Password
* **Supports Reverse Flow** - Yes
* **Project Id** - The Google Cloud Project ID that contains your service account and secret.

***Google Cloud***
1. Log into your Google Cloud project at https://console.cloud.google.com/.
1. Create a new service account that will be added to Safeguard for Privileged Passwords and used to call the Google Secret Manager APIs.
1. Navigate to IAM & Admin -> Service Accounts. Or just search for *Service Accounts*.
   1. Choose to create a new service account.
   1. After the service account is created, go to the **Keys** tab and click the **Add Key** to create a new key pair.
   1. Choose **JSON** for the key type.
   1. The private key will be downloaded and saved to your local computer as a file.
1. Create a new asset and account in Safeguard for Privileged Passwords to hold this Google Cloud service account credentials.
   1. Manually set the password on this Safeguard service account to the file contents of the JSON file that was downloaded from Google.
      1. If needed, change the Password text box to *multi-line*. Then just copy and paste.
1. Back in the Google Cloud console portal, navigate to Security -> Secret Manager. Or just search for *Secret Manager*.
1. Create a new secret.
1. Edit the permissions of the secret by adding the service account created above.
1. Grant the service account the **Secret Manager Secret Version Adder** role in order to allow Safeguard for Privileged Passwords to update the secret's value.
1. Grant the service account the **Secret Manager Secret Accessor** role in order to allow Secrets Broker to pull the value from Google Cloud and update it in Safeguard for Privileged Passwords.